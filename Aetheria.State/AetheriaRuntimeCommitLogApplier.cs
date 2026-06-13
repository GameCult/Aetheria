using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Caching;
using MessagePack;

namespace Aetheria.State;

public sealed class AetheriaRuntimeCommitApplyReport
{
    public int AppliedPlayerSettings { get; set; }
    public int AppliedLoadoutTemplates { get; set; }
    public int AppliedRunCheckpoints { get; set; }
    public string[] AppliedPaths { get; set; } = [];
}

public static class AetheriaRuntimeCommitLogApplier
{
    private const string CommitSchema = "gamecult.aetheria.runtime_commit.v1";

    public static async Task<AetheriaRuntimeCommitApplyReport> ApplyPendingAsync(
        AetheriaStateNode node,
        bool deleteApplied = true)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var pendingDirectory = node.StatePath + ".pending";
        var report = new AetheriaRuntimeCommitApplyReport();
        var applied = new List<string>();

        if (!Directory.Exists(pendingDirectory))
            return report;

        foreach (var path in Directory.EnumerateFiles(pendingDirectory, "*.cc").OrderBy(path => path, StringComparer.Ordinal))
        {
            var command = ReadCommand(path);
            switch (command.Kind)
            {
                case "player_settings":
                    await node.PutPlayerSettingsAsync(command.PlayerSettings ?? throw MissingPayload(command)).ConfigureAwait(false);
                    report.AppliedPlayerSettings++;
                    break;
                case "loadout_template":
                    var loadout = command.LoadoutTemplate ?? throw MissingPayload(command);
                    await node.PutLoadoutTemplateAsync(LoadoutKey(loadout.Name), loadout).ConfigureAwait(false);
                    report.AppliedLoadoutTemplates++;
                    break;
                case "run_checkpoint":
                    var run = command.RunState ?? throw MissingPayload(command);
                    await node.PutRunStateAsync(RunKey(run.RunId), run).ConfigureAwait(false);
                    report.AppliedRunCheckpoints++;
                    break;
                default:
                    throw new InvalidDataException($"Unknown Aetheria runtime commit kind '{command.Kind}' in {path}.");
            }

            applied.Add(path);
            if (deleteApplied)
                File.Delete(path);
        }

        await node.FlushAsync().ConfigureAwait(false);
        report.AppliedPaths = applied.ToArray();
        return report;
    }

    private static PendingCommand ReadCommand(string path)
    {
        var reader = new MessagePackReader(File.ReadAllBytes(path));
        var fields = reader.ReadArrayHeader();
        var schema = fields > 0 ? ReadString(ref reader) : "";
        if (!string.Equals(schema, CommitSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected Aetheria runtime commit schema '{schema}' in {path}.");

        var kind = fields > 1 ? ReadString(ref reader) : "";
        var commandId = fields > 2 ? ReadString(ref reader) : "";
        var createdAtUtc = fields > 3 ? ReadString(ref reader) : "";
        if (fields > 4)
        {
            var command = kind switch
            {
                "player_settings" => PendingCommand.ForPlayerSettings(
                    kind,
                    commandId,
                    createdAtUtc,
                    ReadPlayerSettings(ref reader, createdAtUtc),
                    path),
                "loadout_template" => PendingCommand.ForLoadoutTemplate(
                    kind,
                    commandId,
                    createdAtUtc,
                    ReadLoadoutTemplate(ref reader, createdAtUtc),
                    path),
                "run_checkpoint" => PendingCommand.ForRunState(
                    kind,
                    commandId,
                    createdAtUtc,
                    ReadRunState(ref reader, createdAtUtc),
                    path),
                _ => throw new InvalidDataException($"Unknown Aetheria runtime commit kind '{kind}' in {path}.")
            };

            for (var field = 5; field < fields; field++)
                reader.Skip();

            return command;
        }

        return PendingCommand.Empty(kind, commandId, createdAtUtc, path);
    }

    private static AetheriaPlayerSettings ReadPlayerSettings(ref MessagePackReader reader, string updatedAtUtc)
    {
        var fields = reader.ReadArrayHeader();
        return new AetheriaPlayerSettings
        {
            PlayerName = ReadFieldString(ref reader, fields, 0),
            TutorialPassed = ReadFieldBool(ref reader, fields, 1),
            StoryFileHashes = ReadStoryFileHashes(ref reader, fields, 2),
            Gameplay = new AetheriaPlayerGameplaySettings
            {
                TemperatureUnit = ReadFieldString(ref reader, fields, 3),
                SignificantDigits = ReadFieldInt32(ref reader, fields, 4)
            },
            Graphics = new AetheriaPlayerGraphicsSettings
            {
                NebulaQuality = ReadFieldString(ref reader, fields, 5),
                ShowAsteroidsInMinimap = ReadFieldBool(ref reader, fields, 6)
            },
            Input = new AetheriaPlayerInputSettings
            {
                BindingOverrides = ReadBindings(ref reader, fields, 7),
                ActionBarInputs = ReadStringArray(ref reader, fields, 8)
            },
            LastUpdatedAtUtc = updatedAtUtc
        };
    }

    private static AetheriaLoadoutTemplate ReadLoadoutTemplate(ref MessagePackReader reader, string updatedAtUtc)
    {
        var fields = reader.ReadArrayHeader();
        return new AetheriaLoadoutTemplate
        {
            Name = ReadFieldString(ref reader, fields, 0),
            OwnerPlayerKey = ReadFieldString(ref reader, fields, 1),
            RootEntity = fields > 2 ? ReadEntityLoadout(ref reader) : new AetheriaEntityLoadout(),
            CreatedAtUtc = updatedAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    private static AetheriaEntityLoadout ReadEntityLoadout(ref MessagePackReader reader)
    {
        var fields = reader.ReadArrayHeader();
        return new AetheriaEntityLoadout
        {
            Name = ReadFieldString(ref reader, fields, 0),
            Kind = ReadFieldString(ref reader, fields, 1),
            FactionKey = ReferenceKey("aetheria.corporation", ReadFieldString(ref reader, fields, 2)),
            Hull = fields > 3 ? ReadLoadoutItem(ref reader) : new AetheriaLoadoutItem(),
            Equipment = ReadItemSlots(ref reader, fields, 4),
            CargoBays = ReadItemSlots(ref reader, fields, 5),
            DockingBays = ReadItemSlots(ref reader, fields, 6),
            CargoContents = ReadCargoBays(ref reader, fields, 7),
            DockingBayContents = ReadCargoBays(ref reader, fields, 8),
            DockingBayAssignments = ReadIntArray(ref reader, fields, 9),
            WeaponGroups = ReadIntArrayArray(ref reader, fields, 10),
            Children = ReadChildren(ref reader, fields, 11)
        };
    }

    private static AetheriaLoadoutItem ReadLoadoutItem(ref MessagePackReader reader)
    {
        var fields = reader.ReadArrayHeader();
        return new AetheriaLoadoutItem
        {
            ItemKey = ReferenceKey("aetheria.item_definition", ReadFieldString(ref reader, fields, 0)),
            Quality = ReadFieldDouble(ref reader, fields, 1),
            Durability = ReadFieldDouble(ref reader, fields, 2),
            Quantity = ReadFieldInt32(ref reader, fields, 3)
        };
    }

    private static AetheriaRunState ReadRunState(ref MessagePackReader reader, string updatedAtUtc)
    {
        var fields = reader.ReadArrayHeader();
        return new AetheriaRunState
        {
            RunId = ReadFieldString(ref reader, fields, 0),
            IsTutorial = ReadFieldBool(ref reader, fields, 1),
            EntranceZoneIndex = ReadFieldInt32(ref reader, fields, 2),
            ExitZoneIndex = ReadFieldInt32(ref reader, fields, 3),
            CurrentZoneIndex = ReadFieldInt32(ref reader, fields, 4),
            CurrentZoneEntityIndex = ReadFieldInt32(ref reader, fields, 5),
            DiscoveredZoneIndices = ReadIntArray(ref reader, fields, 6),
            UpdatedAtUtc = updatedAtUtc
        };
    }

    private static AetheriaStoryFileHash[] ReadStoryFileHashes(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var hashes = new AetheriaStoryFileHash[count];
        for (var item = 0; item < count; item++)
        {
            var itemFields = reader.ReadArrayHeader();
            hashes[item] = new AetheriaStoryFileHash
            {
                StoryPath = ReadFieldString(ref reader, itemFields, 0),
                Hash = ReadFieldString(ref reader, itemFields, 1)
            };
        }

        return hashes;
    }

    private static AetheriaInputBindingOverride[] ReadBindings(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var bindings = new AetheriaInputBindingOverride[count];
        for (var item = 0; item < count; item++)
        {
            var itemFields = reader.ReadArrayHeader();
            bindings[item] = new AetheriaInputBindingOverride
            {
                ActionName = ReadFieldString(ref reader, itemFields, 0),
                BindingIndex = ReadFieldInt32(ref reader, itemFields, 1),
                BindingPath = ReadFieldString(ref reader, itemFields, 2)
            };
        }

        return bindings;
    }

    private static AetheriaLoadoutItemSlot[] ReadItemSlots(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var slots = new AetheriaLoadoutItemSlot[count];
        for (var item = 0; item < count; item++)
        {
            var itemFields = reader.ReadArrayHeader();
            slots[item] = new AetheriaLoadoutItemSlot
            {
                Position = new AetheriaGridCoord
                {
                    X = ReadFieldInt32(ref reader, itemFields, 0),
                    Y = ReadFieldInt32(ref reader, itemFields, 1)
                },
                Item = itemFields > 2 ? ReadLoadoutItem(ref reader) : new AetheriaLoadoutItem()
            };
        }

        return slots;
    }

    private static AetheriaCargoBayLoadout[] ReadCargoBays(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var bays = new AetheriaCargoBayLoadout[count];
        for (var item = 0; item < count; item++)
        {
            bays[item] = new AetheriaCargoBayLoadout
            {
                Items = ReadItemSlots(ref reader, 1, 0)
            };
        }

        return bays;
    }

    private static AetheriaEntityLoadout[] ReadChildren(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var children = new AetheriaEntityLoadout[count];
        for (var item = 0; item < count; item++)
            children[item] = ReadEntityLoadout(ref reader);
        return children;
    }

    private static int[][] ReadIntArrayArray(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var arrays = new int[count][];
        for (var item = 0; item < count; item++)
            arrays[item] = ReadIntArray(ref reader, 1, 0);
        return arrays;
    }

    private static string[] ReadStringArray(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var values = new string[count];
        for (var item = 0; item < count; item++)
            values[item] = ReadString(ref reader);
        return values;
    }

    private static int[] ReadIntArray(ref MessagePackReader reader, int fields, int index)
    {
        if (index >= fields) return [];
        var count = reader.ReadArrayHeader();
        var values = new int[count];
        for (var item = 0; item < count; item++)
            values[item] = reader.ReadInt32();
        return values;
    }

    private static string ReadFieldString(ref MessagePackReader reader, int fields, int index)
    {
        return index >= fields ? "" : ReadString(ref reader);
    }

    private static int ReadFieldInt32(ref MessagePackReader reader, int fields, int index)
    {
        return index >= fields ? 0 : reader.ReadInt32();
    }

    private static double ReadFieldDouble(ref MessagePackReader reader, int fields, int index)
    {
        return index >= fields ? 0 : reader.ReadDouble();
    }

    private static bool ReadFieldBool(ref MessagePackReader reader, int fields, int index)
    {
        return index < fields && reader.ReadBoolean();
    }

    private static string ReadString(ref MessagePackReader reader)
    {
        return reader.ReadString() ?? "";
    }

    private static CultRecordKey LoadoutKey(string name)
    {
        return new CultRecordKey($"global:aetheria.loadout_template.{StableToken(name)}.v1");
    }

    private static CultRecordKey RunKey(string runId)
    {
        return new CultRecordKey($"global:aetheria.run_state.{StableToken(runId)}.v1");
    }

    private static string ReferenceKey(string documentName, string legacyId)
    {
        return string.IsNullOrWhiteSpace(legacyId) ? "" : $"{documentName}:legacy:{legacyId}";
    }

    private static string StableToken(string value)
    {
        var chars = (string.IsNullOrWhiteSpace(value) ? "unnamed" : value.Trim().ToLowerInvariant())
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var token = new string(chars).Trim('-');
        while (token.Contains("--", StringComparison.Ordinal))
            token = token.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(token) ? "unnamed" : token;
    }

    private static InvalidDataException MissingPayload(PendingCommand command)
    {
        return new InvalidDataException($"Aetheria runtime commit '{command.CommandId}' has no {command.Kind} payload.");
    }

    private readonly struct PendingCommand
    {
        private PendingCommand(
            string kind,
            string commandId,
            string createdAtUtc,
            string path,
            AetheriaPlayerSettings? playerSettings,
            AetheriaLoadoutTemplate? loadoutTemplate,
            AetheriaRunState? runState)
        {
            Kind = kind;
            CommandId = commandId;
            CreatedAtUtc = createdAtUtc;
            Path = path;
            PlayerSettings = playerSettings;
            LoadoutTemplate = loadoutTemplate;
            RunState = runState;
        }

        public static PendingCommand Empty(string kind, string commandId, string createdAtUtc, string path)
        {
            return new PendingCommand(kind, commandId, createdAtUtc, path, null, null, null);
        }

        public static PendingCommand ForPlayerSettings(
            string kind,
            string commandId,
            string createdAtUtc,
            AetheriaPlayerSettings settings,
            string path)
        {
            return new PendingCommand(kind, commandId, createdAtUtc, path, settings, null, null);
        }

        public static PendingCommand ForLoadoutTemplate(
            string kind,
            string commandId,
            string createdAtUtc,
            AetheriaLoadoutTemplate loadout,
            string path)
        {
            return new PendingCommand(kind, commandId, createdAtUtc, path, null, loadout, null);
        }

        public static PendingCommand ForRunState(
            string kind,
            string commandId,
            string createdAtUtc,
            AetheriaRunState run,
            string path)
        {
            return new PendingCommand(kind, commandId, createdAtUtc, path, null, null, run);
        }

        public string Kind { get; }
        public string CommandId { get; }
        public string CreatedAtUtc { get; }
        public string Path { get; }
        public AetheriaPlayerSettings? PlayerSettings { get; }
        public AetheriaLoadoutTemplate? LoadoutTemplate { get; }
        public AetheriaRunState? RunState { get; }
    }
}
