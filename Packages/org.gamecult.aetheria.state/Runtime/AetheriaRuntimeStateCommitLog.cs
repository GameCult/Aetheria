using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public enum AetheriaRuntimeCommitKind
    {
        PlayerSettings,
        LoadoutTemplate,
        RunCheckpoint
    }

    public sealed class AetheriaRuntimeCommitEnvelope
    {
        public AetheriaRuntimeCommitEnvelope(
            string schema,
            AetheriaRuntimeCommitKind kind,
            string commandId,
            string createdAtUtc,
            string path)
        {
            Schema = schema;
            Kind = kind;
            CommandId = commandId;
            CreatedAtUtc = createdAtUtc;
            Path = path;
        }

        public string Schema { get; }
        public AetheriaRuntimeCommitKind Kind { get; }
        public string CommandId { get; }
        public string CreatedAtUtc { get; }
        public string Path { get; }
    }

    public sealed class AetheriaRuntimePlayerSettingsCommit
    {
        public string PlayerName { get; set; } = "";
        public bool TutorialPassed { get; set; }
        public IReadOnlyList<AetheriaRuntimeStoryFileHashCommit> StoryFileHashes { get; set; } = Array.Empty<AetheriaRuntimeStoryFileHashCommit>();
        public string TemperatureUnit { get; set; } = "Celsius";
        public int SignificantDigits { get; set; } = 3;
        public string NebulaQuality { get; set; } = "Normal";
        public bool ShowAsteroidsInMinimap { get; set; }
        public IReadOnlyList<AetheriaRuntimeInputBindingCommit> BindingOverrides { get; set; } = Array.Empty<AetheriaRuntimeInputBindingCommit>();
        public IReadOnlyList<string> ActionBarInputs { get; set; } = Array.Empty<string>();
    }

    public sealed class AetheriaRuntimeStoryFileHashCommit
    {
        public string StoryPath { get; set; } = "";
        public string Hash { get; set; } = "";
    }

    public sealed class AetheriaRuntimeInputBindingCommit
    {
        public string ActionName { get; set; } = "";
        public int BindingIndex { get; set; }
        public string BindingPath { get; set; } = "";
    }

    public sealed class AetheriaRuntimeLoadoutTemplateCommit
    {
        public string Name { get; set; } = "";
        public string OwnerPlayerKey { get; set; } = "";
        public AetheriaRuntimeEntityLoadoutCommit RootEntity { get; set; } = new AetheriaRuntimeEntityLoadoutCommit();
    }

    public sealed class AetheriaRuntimeEntityLoadoutCommit
    {
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string FactionLegacyId { get; set; } = "";
        public AetheriaRuntimeLoadoutItemCommit Hull { get; set; } = new AetheriaRuntimeLoadoutItemCommit();
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> Equipment { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> CargoBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> DockingBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> CargoContents { get; set; } = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
        public IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> DockingBayContents { get; set; } = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
        public IReadOnlyList<int> DockingBayAssignments { get; set; } = Array.Empty<int>();
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; set; } = Array.Empty<IReadOnlyList<int>>();
        public IReadOnlyList<AetheriaRuntimeEntityLoadoutCommit> Children { get; set; } = Array.Empty<AetheriaRuntimeEntityLoadoutCommit>();
    }

    public sealed class AetheriaRuntimeLoadoutItemCommit
    {
        public string ItemLegacyId { get; set; } = "";
        public double Quality { get; set; } = 1.0;
        public double Durability { get; set; } = 1.0;
        public int Quantity { get; set; } = 1;
    }

    public sealed class AetheriaRuntimeLoadoutItemSlotCommit
    {
        public int X { get; set; }
        public int Y { get; set; }
        public AetheriaRuntimeLoadoutItemCommit Item { get; set; } = new AetheriaRuntimeLoadoutItemCommit();
    }

    public sealed class AetheriaRuntimeCargoBayLoadoutCommit
    {
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> Items { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
    }

    public sealed class AetheriaRuntimeRunCheckpointCommit
    {
        public string RunId { get; set; } = "local";
        public bool IsTutorial { get; set; }
        public int EntranceZoneIndex { get; set; } = -1;
        public int ExitZoneIndex { get; set; } = -1;
        public int CurrentZoneIndex { get; set; } = -1;
        public int CurrentZoneEntityIndex { get; set; } = -1;
        public IReadOnlyList<int> DiscoveredZoneIndices { get; set; } = Array.Empty<int>();
        public IReadOnlyList<AetheriaRuntimeZoneSnapshotCommit> Zones { get; set; } = Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
    }

    public sealed class AetheriaRuntimeZoneSnapshotCommit
    {
        public int ZoneIndex { get; set; } = -1;
        public string Name { get; set; } = "";
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public IReadOnlyList<int> AdjacentZoneIndices { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> FactionIndices { get; set; } = Array.Empty<int>();
        public int OwnerFactionIndex { get; set; } = -1;
        public IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> Entities { get; set; } = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
    }

    public sealed class AetheriaRuntimeEntitySnapshotCommit
    {
        public int EntityIndex { get; set; } = -1;
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
        public string FactionLegacyId { get; set; } = "";
        public string HullItemLegacyId { get; set; } = "";
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> Equipment { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> CargoBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
        public IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> DockingBays { get; set; } = Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
        public IReadOnlyList<int> ChildEntityIndices { get; set; } = Array.Empty<int>();
        public IReadOnlyList<IReadOnlyList<int>> WeaponGroups { get; set; } = Array.Empty<IReadOnlyList<int>>();
    }

    public static class AetheriaRuntimeStateCommitLog
    {
        public const string CommitSchema = "gamecult.aetheria.runtime_commit.v1";

        private delegate void WritePayload(ref MessagePackWriter writer);

        public static string GetPendingDirectory(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            return stateFilePath + ".pending";
        }

        public static AetheriaRuntimeCommitEnvelope QueuePlayerSettings(
            string stateFilePath,
            AetheriaRuntimePlayerSettingsCommit settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return WriteCommit(stateFilePath, AetheriaRuntimeCommitKind.PlayerSettings, (ref MessagePackWriter writer) => WritePlayerSettings(ref writer, settings));
        }

        public static AetheriaRuntimeCommitEnvelope QueueLoadoutTemplate(
            string stateFilePath,
            AetheriaRuntimeLoadoutTemplateCommit loadout)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            return WriteCommit(stateFilePath, AetheriaRuntimeCommitKind.LoadoutTemplate, (ref MessagePackWriter writer) => WriteLoadoutTemplate(ref writer, loadout));
        }

        public static AetheriaRuntimeCommitEnvelope QueueRunCheckpoint(
            string stateFilePath,
            AetheriaRuntimeRunCheckpointCommit run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            return WriteCommit(stateFilePath, AetheriaRuntimeCommitKind.RunCheckpoint, (ref MessagePackWriter writer) => WriteRunCheckpoint(ref writer, run));
        }

        public static IReadOnlyList<AetheriaRuntimeCommitEnvelope> ReadPending(string stateFilePath)
        {
            var pendingDirectory = GetPendingDirectory(stateFilePath);
            if (!Directory.Exists(pendingDirectory))
                return Array.Empty<AetheriaRuntimeCommitEnvelope>();

            return Directory
                .EnumerateFiles(pendingDirectory, "*.cc")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(ReadEnvelope)
                .ToArray();
        }

        private static AetheriaRuntimeCommitEnvelope WriteCommit(
            string stateFilePath,
            AetheriaRuntimeCommitKind kind,
            WritePayload writePayload)
        {
            var commandId = Guid.NewGuid().ToString("N");
            var createdAtUtc = DateTime.UtcNow.ToString("O");
            var pendingDirectory = GetPendingDirectory(stateFilePath);
            Directory.CreateDirectory(pendingDirectory);

            var finalPath = Path.Combine(
                pendingDirectory,
                $"{createdAtUtc.Replace(':', '-')}.{KindToken(kind)}.{commandId}.cc");
            var tempPath = finalPath + ".tmp";

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(5);
            writer.Write(CommitSchema);
            writer.Write(KindToken(kind));
            writer.Write(commandId);
            writer.Write(createdAtUtc);
            writePayload(ref writer);
            writer.Flush();

            File.WriteAllBytes(tempPath, buffer.WrittenSpan.ToArray());
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            return new AetheriaRuntimeCommitEnvelope(CommitSchema, kind, commandId, createdAtUtc, finalPath);
        }

        private static AetheriaRuntimeCommitEnvelope ReadEnvelope(string path)
        {
            var reader = new MessagePackReader(File.ReadAllBytes(path));
            var fields = reader.ReadArrayHeader();
            var schema = fields > 0 ? ReadString(ref reader) : "";
            var kind = fields > 1 ? ParseKind(ReadString(ref reader)) : AetheriaRuntimeCommitKind.RunCheckpoint;
            var commandId = fields > 2 ? ReadString(ref reader) : "";
            var createdAtUtc = fields > 3 ? ReadString(ref reader) : "";
            for (var field = 4; field < fields; field++)
                reader.Skip();

            return new AetheriaRuntimeCommitEnvelope(schema, kind, commandId, createdAtUtc, path);
        }

        private static void WritePlayerSettings(ref MessagePackWriter writer, AetheriaRuntimePlayerSettingsCommit settings)
        {
            writer.WriteArrayHeader(9);
            writer.Write(settings.PlayerName ?? "");
            writer.Write(settings.TutorialPassed);
            WriteStoryHashes(ref writer, settings.StoryFileHashes);
            writer.Write(settings.TemperatureUnit ?? "");
            writer.Write(settings.SignificantDigits);
            writer.Write(settings.NebulaQuality ?? "");
            writer.Write(settings.ShowAsteroidsInMinimap);
            WriteBindings(ref writer, settings.BindingOverrides);
            WriteStrings(ref writer, settings.ActionBarInputs);
        }

        private static void WriteStoryHashes(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeStoryFileHashCommit>? hashes)
        {
            hashes ??= Array.Empty<AetheriaRuntimeStoryFileHashCommit>();
            writer.WriteArrayHeader(hashes.Count);
            foreach (var hash in hashes)
            {
                writer.WriteArrayHeader(2);
                writer.Write(hash.StoryPath ?? "");
                writer.Write(hash.Hash ?? "");
            }
        }

        private static void WriteBindings(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeInputBindingCommit>? bindings)
        {
            bindings ??= Array.Empty<AetheriaRuntimeInputBindingCommit>();
            writer.WriteArrayHeader(bindings.Count);
            foreach (var binding in bindings)
            {
                writer.WriteArrayHeader(3);
                writer.Write(binding.ActionName ?? "");
                writer.Write(binding.BindingIndex);
                writer.Write(binding.BindingPath ?? "");
            }
        }

        private static void WriteLoadoutTemplate(ref MessagePackWriter writer, AetheriaRuntimeLoadoutTemplateCommit loadout)
        {
            writer.WriteArrayHeader(3);
            writer.Write(loadout.Name ?? "");
            writer.Write(loadout.OwnerPlayerKey ?? "");
            WriteEntityLoadout(ref writer, loadout.RootEntity);
        }

        private static void WriteEntityLoadout(ref MessagePackWriter writer, AetheriaRuntimeEntityLoadoutCommit entity)
        {
            writer.WriteArrayHeader(12);
            writer.Write(entity.Name ?? "");
            writer.Write(entity.Kind ?? "");
            writer.Write(entity.FactionLegacyId ?? "");
            WriteLoadoutItem(ref writer, entity.Hull);
            WriteItemSlots(ref writer, entity.Equipment);
            WriteItemSlots(ref writer, entity.CargoBays);
            WriteItemSlots(ref writer, entity.DockingBays);
            WriteCargoBays(ref writer, entity.CargoContents);
            WriteCargoBays(ref writer, entity.DockingBayContents);
            WriteInts(ref writer, entity.DockingBayAssignments);
            WriteIntLists(ref writer, entity.WeaponGroups);
            WriteChildren(ref writer, entity.Children);
        }

        private static void WriteLoadoutItem(ref MessagePackWriter writer, AetheriaRuntimeLoadoutItemCommit item)
        {
            writer.WriteArrayHeader(4);
            writer.Write(item.ItemLegacyId ?? "");
            writer.Write(item.Quality);
            writer.Write(item.Durability);
            writer.Write(item.Quantity);
        }

        private static void WriteItemSlots(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
        {
            slots ??= Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            writer.WriteArrayHeader(slots.Count);
            foreach (var slot in slots)
            {
                writer.WriteArrayHeader(3);
                writer.Write(slot.X);
                writer.Write(slot.Y);
                WriteLoadoutItem(ref writer, slot.Item);
            }
        }

        private static void WriteCargoBays(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? cargoBays)
        {
            cargoBays ??= Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            writer.WriteArrayHeader(cargoBays.Count);
            foreach (var bay in cargoBays)
                WriteItemSlots(ref writer, bay.Items);
        }

        private static void WriteChildren(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeEntityLoadoutCommit>? children)
        {
            children ??= Array.Empty<AetheriaRuntimeEntityLoadoutCommit>();
            writer.WriteArrayHeader(children.Count);
            foreach (var child in children)
                WriteEntityLoadout(ref writer, child);
        }

        private static void WriteRunCheckpoint(ref MessagePackWriter writer, AetheriaRuntimeRunCheckpointCommit run)
        {
            writer.WriteArrayHeader(8);
            writer.Write(run.RunId ?? "");
            writer.Write(run.IsTutorial);
            writer.Write(run.EntranceZoneIndex);
            writer.Write(run.ExitZoneIndex);
            writer.Write(run.CurrentZoneIndex);
            writer.Write(run.CurrentZoneEntityIndex);
            WriteInts(ref writer, run.DiscoveredZoneIndices);
            WriteZoneSnapshots(ref writer, run.Zones);
        }

        private static void WriteZoneSnapshots(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeZoneSnapshotCommit>? zones)
        {
            zones ??= Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
            writer.WriteArrayHeader(zones.Count);
            foreach (var zone in zones)
            {
                writer.WriteArrayHeader(8);
                writer.Write(zone.ZoneIndex);
                writer.Write(zone.Name ?? "");
                writer.Write(zone.PositionX);
                writer.Write(zone.PositionY);
                WriteInts(ref writer, zone.AdjacentZoneIndices);
                WriteInts(ref writer, zone.FactionIndices);
                writer.Write(zone.OwnerFactionIndex);
                WriteEntitySnapshots(ref writer, zone.Entities);
            }
        }

        private static void WriteEntitySnapshots(ref MessagePackWriter writer, IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit>? entities)
        {
            entities ??= Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            writer.WriteArrayHeader(entities.Count);
            foreach (var entity in entities)
            {
                writer.WriteArrayHeader(15);
                writer.Write(entity.EntityIndex);
                writer.Write(entity.Name ?? "");
                writer.Write(entity.Kind ?? "");
                writer.Write(entity.PositionX);
                writer.Write(entity.PositionY);
                writer.Write(entity.PositionZ);
                writer.Write(entity.DirectionX);
                writer.Write(entity.DirectionY);
                writer.Write(entity.FactionLegacyId ?? "");
                writer.Write(entity.HullItemLegacyId ?? "");
                WriteItemSlots(ref writer, entity.Equipment);
                WriteItemSlots(ref writer, entity.CargoBays);
                WriteItemSlots(ref writer, entity.DockingBays);
                WriteInts(ref writer, entity.ChildEntityIndices);
                WriteIntLists(ref writer, entity.WeaponGroups);
            }
        }

        private static void WriteStrings(ref MessagePackWriter writer, IReadOnlyList<string>? values)
        {
            values ??= Array.Empty<string>();
            writer.WriteArrayHeader(values.Count);
            foreach (var value in values)
                writer.Write(value ?? "");
        }

        private static void WriteInts(ref MessagePackWriter writer, IReadOnlyList<int>? values)
        {
            values ??= Array.Empty<int>();
            writer.WriteArrayHeader(values.Count);
            foreach (var value in values)
                writer.Write(value);
        }

        private static void WriteIntLists(ref MessagePackWriter writer, IReadOnlyList<IReadOnlyList<int>>? values)
        {
            values ??= Array.Empty<IReadOnlyList<int>>();
            writer.WriteArrayHeader(values.Count);
            foreach (var value in values)
                WriteInts(ref writer, value);
        }

        private static string KindToken(AetheriaRuntimeCommitKind kind)
        {
            return kind switch
            {
                AetheriaRuntimeCommitKind.PlayerSettings => "player_settings",
                AetheriaRuntimeCommitKind.LoadoutTemplate => "loadout_template",
                AetheriaRuntimeCommitKind.RunCheckpoint => "run_checkpoint",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private static AetheriaRuntimeCommitKind ParseKind(string token)
        {
            return token switch
            {
                "player_settings" => AetheriaRuntimeCommitKind.PlayerSettings,
                "loadout_template" => AetheriaRuntimeCommitKind.LoadoutTemplate,
                "run_checkpoint" => AetheriaRuntimeCommitKind.RunCheckpoint,
                _ => throw new InvalidDataException($"Unknown Aetheria runtime commit kind '{token}'.")
            };
        }

        private static string ReadString(ref MessagePackReader reader)
        {
            return reader.ReadString() ?? "";
        }
    }
}
