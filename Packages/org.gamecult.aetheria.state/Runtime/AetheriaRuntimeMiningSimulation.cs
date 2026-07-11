using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeMiningSimulation
    {
        public const double MiningDifficulty = 500;

        public static void Step(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonIntentState intents,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId,
            double simulationTimeSeconds,
            double deltaSeconds)
        {
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            foreach (var asteroid in body?.Asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>())
                asteroid.RespawnTimer = Math.Max(0, asteroid.RespawnTimer - deltaSeconds);

            foreach (var intent in intents?.Behaviors ?? Enumerable.Empty<AetheriaRuntimeDaemonBehaviorIntent>())
            {
                if (!intent.Active || !TryEntityIndex(intent.ActorEntityKey, out var minerIndex))
                    continue;
                var miner = entities.FirstOrDefault(entity => entity.EntityIndex == minerIndex && entity.IsActive);
                var tool = AetheriaRuntimeEquippedBehaviorQueries.Find(miner, catalog, "MiningTool")
                    .FirstOrDefault(candidate => candidate.EquipmentIndex == intent.EquipmentIndex && candidate.BehaviorIndex == intent.BehaviorIndex);
                var body = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                    .FirstOrDefault(candidate => candidate != null && string.Equals(candidate.BodyKey, intent.TargetBodyKey, StringComparison.Ordinal));
                if (miner == null || tool == null || body == null || intent.TargetAsteroidIndex < 0 || intent.TargetAsteroidIndex >= body.Asteroids.Count)
                    continue;
                var asteroid = body.Asteroids[intent.TargetAsteroidIndex];
                if (asteroid.RespawnTimer > 0)
                    continue;
                var pose = AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(zone, body.BodyKey, simulationTimeSeconds)
                    .FirstOrDefault(candidate => candidate.AsteroidIndex == intent.TargetAsteroidIndex);
                var range = Math.Max(0, tool.EvaluateStat(4));
                if (Math.Pow(pose.PositionX - miner.PositionX, 2) + Math.Pow(pose.PositionZ - miner.PositionZ, 2) > range * range)
                    continue;

                var damage = Math.Max(0, tool.EvaluateStat(1)) * deltaSeconds;
                var efficiency = Math.Max(0, tool.EvaluateStat(2));
                var penetration = Math.Max(0.001, tool.EvaluateStat(3));
                asteroid.Damage += damage;
                var accumulators = (asteroid.MiningAccumulators ?? Array.Empty<AetheriaRuntimeAsteroidMiningAccumulatorCommit>()).ToList();
                var accumulator = accumulators.FirstOrDefault(value => value.MinerEntityIndex == minerIndex);
                if (accumulator == null)
                {
                    accumulator = new AetheriaRuntimeAsteroidMiningAccumulatorCommit { MinerEntityIndex = minerIndex };
                    accumulators.Add(accumulator);
                }
                accumulator.Amount += damage;
                asteroid.MiningAccumulators = accumulators;

                var resources = (body.Resources ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>()).Where(resource => resource.Amount > 0).ToArray();
                if (resources.Length > 0)
                {
                    var resource = resources.OrderByDescending(value => Math.Pow(value.Amount, 1.0 / penetration) * Roll(run.GenerationSeed, frameId, minerIndex, intent.TargetAsteroidIndex, value.ItemKey)).First();
                    if (efficiency * Roll(run.GenerationSeed, frameId, minerIndex, intent.TargetAsteroidIndex, "yield") * accumulator.Amount * resources.Length / MiningDifficulty > 1)
                    {
                        accumulator.Amount = 0;
                        AddCargo(miner, resource.ItemKey);
                    }
                }

                if (asteroid.Damage >= Hitpoints(asteroid.Size))
                {
                    asteroid.Damage = 0;
                    asteroid.MiningAccumulators = Array.Empty<AetheriaRuntimeAsteroidMiningAccumulatorCommit>();
                    asteroid.RespawnTimer = RespawnTime(asteroid.Size);
                }
            }
        }

        private static void AddCargo(AetheriaRuntimeEntitySnapshotCommit entity, string itemKey)
        {
            var bays = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToList();
            if (bays.Count == 0) bays.Add(new AetheriaRuntimeCargoBayLoadoutCommit());
            var slots = (bays[0].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var existing = slots.FirstOrDefault(slot => string.Equals(slot?.Item?.ItemKey, itemKey, StringComparison.Ordinal));
            if (existing != null) existing.Item.Quantity++;
            else slots.Add(new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = itemKey, Quantity = 1 } });
            bays[0].Items = slots;
            entity.CargoContents = bays;
        }

        private static double Hitpoints(double size) => 10 + 190 * Math.Pow(Clamp((size - 3) / 3), 2);
        private static double RespawnTime(double size) => 10 + 90 * Math.Pow(Clamp((size - 3) / 3), 1.5);
        private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
        private static double Roll(uint seed, long frame, int miner, int asteroid, string salt)
        {
            unchecked
            {
                uint hash = seed ^ (uint)frame * 16777619u ^ (uint)miner * 2246822519u ^ (uint)asteroid * 3266489917u;
                foreach (var c in salt ?? "") hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
                return (hash + 1.0) / (uint.MaxValue + 2.0);
            }
        }
        private static bool TryEntityIndex(string key, out int value) => int.TryParse((key ?? "").Split('.').LastOrDefault(), out value);
    }
}
