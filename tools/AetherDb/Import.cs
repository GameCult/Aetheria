using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// One-shot import of the legacy GameData msgpack files into GameData/Aetheria.cc. Cut 10 deletes it.
public static class Import
{
    // The legacy DatabaseEntry union's catalog tags. Any other tag is not catalog state and throws.
    private static readonly (int Tag, Type Type)[] Tags =
    {
        (0, typeof(SimpleCommodityData)), (1, typeof(CompoundCommodityData)), (2, typeof(GearData)), (3, typeof(HullData)),
        (9, typeof(NameFile)), (13, typeof(Faction)), (17, typeof(PersonalityAttribute)), (29, typeof(CargoBayData)),
        (30, typeof(DockingBayData)), (31, typeof(WeaponItemData)), (32, typeof(FactionProductData))
    };

    private static string GameData => Path.Combine(AetherDb.FindRoot(), "GameData");
    private static string[] NameFiles => Directory.GetFiles(Path.Combine(GameData, "NameFile"), "*.msgpack");
    private static string[] Layouts => Directory.GetFiles(Path.Combine(GameData, "KeyboardLayouts"), "*.msgpack");

    public static int LegacyCensus()
    {
        var records = Read(Path.Combine(GameData, "AetherDB.msgpack"), true);
        Console.WriteLine("tags " + string.Join(", ", records.GroupBy(r => r.Tag).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine($"{NameFiles.Length} name files, {Layouts.Length} layouts");
        return 0;
    }

    public static int Run()
    {
        var path = Path.Combine(GameData, "Aetheria.cc");
        if (File.Exists(path))
        {
            Console.WriteLine($"{path} exists. The import is one-shot and never merges.");
            return 1;
        }

        // Every Guid-slotted type, which is every tagged one; InputLayout is imported bare and keeps its Key(0).
        var keyed = Tags.Select(t => t.Type).FirstOrDefault(type => Members(type).ContainsKey(0));
        if (keyed != null) throw new InvalidOperationException($"{keyed.Name} declares [Key(0)], the slot the legacy Guid occupies.");

        var records = Read(Path.Combine(GameData, "AetherDB.msgpack"), true).Concat(NameFiles.SelectMany(file => Read(file, false))).ToArray();
        var context = new Context();
        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var documents = new List<(Type Type, object Document, CultRecordKey Key)>();
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                context.Key = null;
                try
                {
                    var type = Tags.FirstOrDefault(t => t.Tag == record.Tag).Type ?? throw new InvalidOperationException("not a catalog tag");
                    var buffer = new ArrayBufferWriter<byte>();
                    var writer = new MessagePackWriter(buffer);
                    var reader = new MessagePackReader(record.Payload);
                    Rewrite(ref reader, ref writer, type, context, document: true);
                    writer.Flush();
                    documents.Add((type, CultDocumentMessagePackSerialization.DeserializeUntyped(type, buffer.WrittenSpan.ToArray(), cache.Registry), new CultRecordKey(context.Key)));
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"tag {record.Tag}, record {index} ({record.Source}), legacy key {context.Key ?? "unread"}: {e.Message}", e);
                }
            }

            foreach (var layout in Layouts)
                documents.Add((typeof(InputLayout), CultDocumentMessagePackSerialization.DeserializeUntyped(typeof(InputLayout), File.ReadAllBytes(layout), cache.Registry),
                    new CultRecordKey(Path.GetFileNameWithoutExtension(layout))));

            if (!cache.Commit(batch =>
                {
                    foreach (var (type, document, key) in documents) batch.Upsert(type, document, key);
                }))
                throw new InvalidOperationException("The catalog commit was refused.");
        }

        // Reopened read-only, so the catalog-global check runs against what landed.
        using (var cache = AetheriaStores.Open(path))
        {
            var order = Tags.Select(t => t.Type).Append(typeof(InputLayout)).ToList();
            var stored = cache.AllStoredDocuments.ToArray();
            Console.WriteLine(string.Join(", ", stored.GroupBy(s => s.Document.GetType()).OrderBy(g => order.IndexOf(g.Key))
                .Select(g => $"{g.Key.Name} {g.Count()}")) + $", {stored.Length} records in all");

            var dangling = context.Refs.Where(r => !r.Target.IsInstanceOfType(cache.Get(new CultRecordKey(r.Key)))).ToArray();
            Console.WriteLine($"{dangling.Length} refs resolve to nothing:");
            foreach (var r in dangling)
            {
                var owner = cache.Get(new CultRecordKey(r.Record));
                Console.WriteLine($"  {owner?.GetType().GetField("Name")?.GetValue(owner)} {r.Member} -> {r.Key}");
            }
        }

        var snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path));
        Console.WriteLine("schemas " + string.Join(", ", snapshot.Records
            .Select(record => snapshot.SchemaCatalog.Single(entry => entry.SchemaId == record.SchemaId).SchemaName).Distinct().OrderBy(name => name)));
        return 0;
    }

    private sealed class Context
    {
        public string Key;
        public string Member;
        public readonly List<(string Record, string Member, Type Target, string Key)> Refs = new List<(string, string, Type, string)>();
    }

    // Legacy records are [tag, payload] pairs: many in AetherDB.msgpack, one per name file.
    private static List<(int Tag, byte[] Payload, string Source)> Read(string path, bool many)
    {
        var records = new List<(int, byte[], string)>();
        var reader = new MessagePackReader(File.ReadAllBytes(path));
        var count = many ? reader.ReadArrayHeader() : 1;
        for (var i = 0; i < count; i++)
        {
            if (reader.ReadArrayHeader() != 2) throw new InvalidOperationException($"{path} record {i} is not [tag, payload].");
            var tag = reader.ReadInt32();
            records.Add((tag, reader.ReadRaw().ToArray(), $"{Path.GetFileName(path)}[{i}]"));
        }
        return records;
    }

    // Copies one legacy value, rewriting Guids into record keys by the target type's shape.
    private static void Rewrite(ref MessagePackReader reader, ref MessagePackWriter writer, Type type, Context context, bool document = false)
    {
        if (reader.TryReadNil())
        {
            writer.WriteNil();
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(CultRecordRef<>))
        {
            var key = ReadGuid(ref reader);
            if (key == null) writer.WriteNil();
            else
            {
                writer.Write(key);
                context.Refs.Add((context.Key, context.Member, type.GetGenericArguments()[0], key));
            }
            return;
        }

        var unions = type.GetCustomAttributes<UnionAttribute>(false).ToArray();
        if (unions.Length > 0)
        {
            if (reader.ReadArrayHeader() != 2) throw new InvalidOperationException($"{type.Name} is not [tag, payload].");
            var tag = reader.ReadInt32();
            var subtype = unions.FirstOrDefault(u => u.Key == tag)?.SubType ?? throw new InvalidOperationException($"{type.Name} has no union tag {tag}.");
            writer.WriteArrayHeader(2);
            writer.Write(tag);
            Rewrite(ref reader, ref writer, subtype, context);
            return;
        }

        if (document && (type.GetCustomAttribute<MessagePackObjectAttribute>(false) == null || reader.NextMessagePackType != MessagePackType.Array))
            throw new InvalidOperationException($"{type.Name} is not an integer-keyed MessagePack object.");

        if (type.GetCustomAttribute<MessagePackObjectAttribute>(false) != null && reader.NextMessagePackType == MessagePackType.Array)
        {
            var members = Members(type);
            var count = reader.ReadArrayHeader();
            writer.WriteArrayHeader(count);
            for (var slot = 0; slot < count; slot++)
            {
                if (document && slot == 0)
                {
                    context.Key = ReadGuid(ref reader) ?? throw new InvalidOperationException("slot 0 holds no Guid");
                    writer.WriteNil();
                }
                else if (!members.TryGetValue(slot, out var member)) writer.WriteRaw(reader.ReadRaw());
                else if (member.DeclaringType == typeof(StatModifierData) && member.Name == nameof(StatModifierData.RequireBehavior))
                {
                    // A legacy assembly-qualified type name; the game compares simple names.
                    var name = reader.ReadString();
                    var head = name?.Split(',')[0];
                    writer.Write(head?.Substring(head.LastIndexOf('.') + 1));
                }
                else
                {
                    var outer = context.Member;
                    if (document) context.Member = $"{type.Name}.{member.Name}";
                    Rewrite(ref reader, ref writer, member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType, context);
                    context.Member = outer;
                }
            }
            return;
        }

        var list = type.IsArray && type.GetArrayRank() == 1 && type != typeof(byte[]) ? type.GetElementType()
            : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : null;
        if (list != null && reader.NextMessagePackType == MessagePackType.Array)
        {
            var count = reader.ReadArrayHeader();
            writer.WriteArrayHeader(count);
            for (var i = 0; i < count; i++) Rewrite(ref reader, ref writer, list, context);
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && reader.NextMessagePackType == MessagePackType.Map)
        {
            var arguments = type.GetGenericArguments();
            var count = reader.ReadMapHeader();
            writer.WriteMapHeader(count);
            for (var i = 0; i < count; i++)
            {
                Rewrite(ref reader, ref writer, arguments[0], context);
                Rewrite(ref reader, ref writer, arguments[1], context);
            }
            return;
        }

        writer.WriteRaw(reader.ReadRaw());
    }

    // A legacy Guid: a 16-byte bin, a DatabaseLink [bin16], or nil. The all-zero Guid is unset, so it becomes nil.
    private static string ReadGuid(ref MessagePackReader reader)
    {
        if (reader.TryReadNil()) return null;
        if (reader.NextMessagePackType == MessagePackType.Array)
        {
            if (reader.ReadArrayHeader() != 1) throw new InvalidOperationException("a legacy DatabaseLink is not [guid]");
            return ReadGuid(ref reader);
        }
        var bytes = reader.ReadBytes()?.ToArray();
        if (bytes == null || bytes.Length != 16) throw new InvalidOperationException("a legacy Guid is not a 16-byte bin");
        var guid = new Guid(bytes);
        return guid == Guid.Empty ? null : guid.ToString("D");
    }

    private static Dictionary<int, MemberInfo> Members(Type type)
    {
        var members = new Dictionary<int, MemberInfo>();
        for (var t = type; t != null; t = t.BaseType)
            foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if ((member is FieldInfo || member is PropertyInfo) && member.GetCustomAttribute<KeyAttribute>()?.IntKey is int slot && !members.ContainsKey(slot))
                    members[slot] = member;
        return members;
    }
}
