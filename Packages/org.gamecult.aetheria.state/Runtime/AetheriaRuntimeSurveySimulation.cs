using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeSurveySimulation
    {
        public static void Step(AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities, AetheriaRuntimeDaemonIntentState intents,
            AetheriaRuntimeCatalogSnapshot? catalog, long frameId, double simulationTimeSeconds, double deltaSeconds)
        {
            foreach (var intent in intents?.Behaviors ?? Enumerable.Empty<AetheriaRuntimeDaemonBehaviorIntent>())
            {
                if (!intent.Active || !int.TryParse((intent.ActorEntityKey ?? "").Split('.').LastOrDefault(), out var index)) continue;
                var entity = entities.FirstOrDefault(value => value.EntityIndex == index && value.IsActive);
                var scanner = AetheriaRuntimeEquippedBehaviorQueries.Find(entity, catalog, "ResourceScanner")
                    .FirstOrDefault(value => value.EquipmentIndex == intent.EquipmentIndex && value.BehaviorIndex == intent.BehaviorIndex);
                if (entity == null || scanner == null) continue;
                var body = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>()).FirstOrDefault(value => value != null && string.Equals(value.BodyKey, intent.TargetBodyKey, StringComparison.Ordinal));
                if (body == null) continue;
                double x, z;
                if (intent.TargetAsteroidIndex >= 0)
                {
                    var pose = AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(zone, body.BodyKey, simulationTimeSeconds).FirstOrDefault(value => value.AsteroidIndex == intent.TargetAsteroidIndex);
                    x = pose.PositionX; z = pose.PositionZ;
                }
                else
                {
                    var pose = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone).FirstOrDefault(value => string.Equals(value.BodyKey, body.BodyKey, StringComparison.Ordinal));
                    x = pose.CenterX; z = pose.CenterZ;
                }
                var range = Math.Max(0, scanner.EvaluateStat(1));
                if (Math.Pow(x - entity.PositionX, 2) + Math.Pow(z - entity.PositionZ, 2) >= range * range) continue;
                var state = scanner.State;
                if (!string.Equals(state.ResourceScannerTargetBodyKey, body.BodyKey, StringComparison.Ordinal)) state.ResourceScannerScanTime = 0;
                state.ResourceScannerTargetBodyKey = body.BodyKey; state.ResourceScannerAsteroidIndex = intent.TargetAsteroidIndex;
                state.ResourceScannerRange = range; state.ResourceScannerMinimumDensity = scanner.EvaluateStat(2); state.ResourceScannerScanDuration = scanner.EvaluateStat(3);
                state.ResourceScannerScanTime += deltaSeconds;
                if (state.ResourceScannerScanTime <= state.ResourceScannerScanDuration) continue;
                var surveys = (run.CorporationSurveys ?? Array.Empty<AetheriaRuntimeCorporationSurveyCommit>()).ToList();
                var survey = surveys.FirstOrDefault(value => string.Equals(value.CorporationKey, entity.FactionKey, StringComparison.Ordinal) && string.Equals(value.BodyKey, body.BodyKey, StringComparison.Ordinal));
                if (survey == null) { survey = new AetheriaRuntimeCorporationSurveyCommit { CorporationKey = entity.FactionKey, BodyKey = body.BodyKey }; surveys.Add(survey); }
                survey.DensityFloor = state.ResourceScannerMinimumDensity; survey.CompletedFrameId = frameId; state.ResourceScannerScanTime = 0; run.CorporationSurveys = surveys;
            }
        }
    }
}
