using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeLoadoutSnapshotProjector
    {
        public static Task<AetheriaRuntimeLoadoutTemplateCommit> ProjectLoadoutTemplateAsync(
            AetheriaClientState state,
            string entityKey)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            using var frame = state.Reactive<AetheriaRuntimeDaemonFrameDocument>();
            return Task.FromResult(ProjectLoadoutTemplate(
                frame.Current?.Run ?? new AetheriaRuntimeRunCheckpointCommit(),
                entityKey ?? ""));
        }

        public static AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey)
        {
            if (run == null ||
                !TryParseEntityKey(entityKey, out var zoneIndex, out var entityIndex))
            {
                return new AetheriaRuntimeLoadoutTemplateCommit();
            }

            return ProjectLoadoutTemplate(run, zoneIndex, entityIndex);
        }

        public static AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int entityIndex)
        {
            var zone = (run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            var entities = zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var entity = entities.FirstOrDefault(candidate => candidate != null && candidate.EntityIndex == entityIndex);
            if (entity == null)
                return new AetheriaRuntimeLoadoutTemplateCommit();

            return new AetheriaRuntimeLoadoutTemplateCommit
            {
                Name = entity.Name ?? "",
                OwnerPlayerKey = "global:aetheria.player_settings.v1",
                RootEntity = ProjectEntityLoadout(entity, entities)
            };
        }

        public static string AppendToZone(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            string parentEntityKey,
            AetheriaRuntimeLoadoutTemplateCommit template)
        {
            if (run == null || template?.RootEntity == null)
                return "";

            var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).ToArray();
            var zone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return "";

            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            var parentIndex = TryParseEntityKey(parentEntityKey, out var parentZoneIndex, out var parsedParentIndex) &&
                              parentZoneIndex == zoneIndex
                ? parsedParentIndex
                : -1;
            var rootIndex = AppendEntity(entities, template.RootEntity, template.Name);

            if (parentIndex >= 0 && parentIndex < entities.Count)
            {
                var children = (entities[parentIndex].ChildEntityIndices ?? Array.Empty<int>()).ToList();
                if (!children.Contains(rootIndex))
                    children.Add(rootIndex);
                entities[parentIndex].ChildEntityIndices = children.ToArray();
            }

            zone.Entities = entities.ToArray();
            run.Zones = zones;
            return EntityKey(run.RunId, zoneIndex, rootIndex);
        }

        private static AetheriaRuntimeEntityLoadoutCommit ProjectEntityLoadout(
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> zoneEntities)
        {
            var childIndices = (entity.ChildEntityIndices ?? Array.Empty<int>())
                .Where(index => index >= 0)
                .ToArray();
            var children = childIndices
                .Select(index => zoneEntities.FirstOrDefault(candidate => candidate != null && candidate.EntityIndex == index))
                .Where(child => child != null)
                .Select(child => ProjectEntityLoadout(child!, zoneEntities))
                .ToArray();
            var childIndexByEntityIndex = childIndices
                .Select((entityIndex, childIndex) => new { entityIndex, childIndex })
                .ToDictionary(pair => pair.entityIndex, pair => pair.childIndex);

            return new AetheriaRuntimeEntityLoadoutCommit
            {
                Name = entity.Name ?? "",
                Kind = string.IsNullOrWhiteSpace(entity.Kind) ? "ship" : entity.Kind,
                FactionKey = entity.FactionKey ?? "",
                Hull = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = entity.HullItemKey ?? "",
                    Quality = 1.0,
                    Durability = 1.0,
                    Quantity = 1,
                    Enabled = true
                },
                Equipment = CloneSlots(entity.Equipment),
                CargoBays = CloneSlots(entity.CargoBays),
                DockingBays = CloneSlots(entity.DockingBays),
                CargoContents = CloneCargo(entity.CargoContents),
                DockingBayContents = CloneCargo(entity.DockingBayContents),
                DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>())
                    .Select(index => childIndexByEntityIndex.TryGetValue(index, out var childIndex) ? childIndex : -1)
                    .ToArray(),
                WeaponGroups = (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                    .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                    .ToArray(),
                Children = children
            };
        }

        private static int AppendEntity(
            List<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeEntityLoadoutCommit loadout,
            string templateName)
        {
            var entityIndex = entities.Count;
            var entity = new AetheriaRuntimeEntitySnapshotCommit
            {
                EntityIndex = entityIndex,
                Name = string.IsNullOrWhiteSpace(loadout.Name) ? templateName ?? "" : loadout.Name,
                Kind = string.IsNullOrWhiteSpace(loadout.Kind) ? "ship" : loadout.Kind,
                DirectionX = 0,
                DirectionY = 1,
                IsActive = true,
                HullItemKey = loadout.Hull?.ItemKey ?? "",
                FactionKey = loadout.FactionKey ?? "",
                Equipment = CloneSlots(loadout.Equipment),
                CargoBays = CloneSlots(loadout.CargoBays),
                DockingBays = CloneSlots(loadout.DockingBays),
                CargoContents = CloneCargo(loadout.CargoContents),
                DockingBayContents = CloneCargo(loadout.DockingBayContents),
                DockingBayAssignments = (loadout.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
                WeaponGroups = (loadout.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                    .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                    .ToArray(),
                TargetEntityIndex = -1,
                ShutdownPerformance = 0.25
            };
            entities.Add(entity);

            var childIndices = new List<int>();
            foreach (var child in loadout.Children ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>())
            {
                if (child == null)
                    continue;
                childIndices.Add(AppendEntity(entities, child, ""));
            }

            entity.ChildEntityIndices = childIndices.ToArray();
            return entityIndex;
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit[] CloneSlots(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
        {
            return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot != null)
                .Select(CloneSlot)
                .ToArray();
        }

        private static AetheriaRuntimeCargoBayLoadoutCommit[] CloneCargo(
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? cargo)
        {
            return (cargo ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .Select(bay => new AetheriaRuntimeCargoBayLoadoutCommit
                {
                    Items = CloneSlots(bay?.Items)
                })
                .ToArray();
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit CloneSlot(AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            return new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.X,
                Y = slot.Y,
                Item = CloneItem(slot.Item)
            };
        }

        private static AetheriaRuntimeLoadoutItemCommit CloneItem(AetheriaRuntimeLoadoutItemCommit? item)
        {
            return new AetheriaRuntimeLoadoutItemCommit
            {
                ItemKey = item?.ItemKey ?? "",
                Quality = item?.Quality ?? 1.0,
                Durability = item?.Durability ?? 1.0,
                Quantity = item?.Quantity ?? 1,
                Enabled = item?.Enabled ?? true,
                OverrideShutdown = item?.OverrideShutdown ?? false,
                Temperature = item?.Temperature ?? 0
            };
        }

        private static string EntityKey(string runId, int zoneIndex, int entityIndex)
        {
            return $"global:aetheria.run_state.{(string.IsNullOrWhiteSpace(runId) ? "local" : runId)}.zone.{zoneIndex}.entity.{entityIndex}.v1";
        }

        private static bool TryParseEntityKey(string entityKey, out int zoneIndex, out int entityIndex)
        {
            zoneIndex = -1;
            entityIndex = -1;
            if (string.IsNullOrWhiteSpace(entityKey))
                return false;

            var parts = entityKey.Split('.');
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], "zone", StringComparison.Ordinal) &&
                    int.TryParse(parts[i + 1], out zoneIndex))
                {
                    continue;
                }

                if (string.Equals(parts[i], "entity", StringComparison.Ordinal) &&
                    int.TryParse(parts[i + 1], out entityIndex))
                {
                    continue;
                }
            }

            return zoneIndex >= 0 && entityIndex >= 0;
        }
    }

}
