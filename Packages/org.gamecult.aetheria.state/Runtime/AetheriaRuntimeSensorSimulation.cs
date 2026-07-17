using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeSensorSimulation
    {
        public static void StepZone(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            IReadOnlyList<AetheriaRuntimeDaemonSensorPingIntent>? pingIntents,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double deltaSeconds,
            double targetInfoDecay,
            double detectionThreshold,
            long frameId)
        {
            if (catalog == null || deltaSeconds <= 0)
                return;

            foreach (var observer in entities)
            {
                if (observer == null || !observer.IsActive)
                    continue;

                var contacts = (observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Where(contact => contact != null)
                    .ToDictionary(contact => contact.TargetEntityIndex);
                var previouslyVisible = contacts.Values
                    .Where(contact => contact.Visible)
                    .Select(contact => contact.TargetEntityIndex)
                    .ToHashSet();
                var sensors = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(
                    observer, catalog, AetheriaRuntimeBehaviorKinds.Sensor);
                var pingRequested = (pingIntents ?? Array.Empty<AetheriaRuntimeDaemonSensorPingIntent>())
                    .Any(intent => intent != null && ActorMatches(
                        intent.ActorEntityKey, zone.ZoneIndex, observer.EntityIndex));
                if (pingRequested && sensors.Count > 0)
                    TryStartPing(run, zone, observer, sensors[sensors.Count - 1], catalog, frameId);

                foreach (var sensor in sensors)
                    StepSensor(run, zone, observer, sensor, entities, contacts, catalog,
                        deltaSeconds, targetInfoDecay, frameId);

                observer.Contacts = entities
                    .Where(target => target != null && target.EntityIndex != observer.EntityIndex)
                    .Select(target =>
                    {
                        contacts.TryGetValue(target.EntityIndex, out var current);
                        var information = Clamp01(current?.InfoGathered ?? 0);
                        return new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = target.EntityIndex,
                            InfoGathered = information,
                            Hostile = Hostile(observer, target),
                            Visible = information > detectionThreshold
                        };
                    })
                    .OrderBy(contact => contact.TargetEntityIndex)
                    .ToArray();
                if (observer.TargetEntityIndex >= 0 &&
                    previouslyVisible.Contains(observer.TargetEntityIndex) &&
                    !observer.Contacts.Any(contact =>
                        contact.TargetEntityIndex == observer.TargetEntityIndex && contact.Visible))
                    observer.TargetEntityIndex = -1;
            }
        }

        private static void TryStartPing(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEquippedBehavior sensor,
            AetheriaRuntimeCatalogSnapshot catalog,
            long frameId)
        {
            if (sensor.State.PingCooldown >= 0)
                return;
            var thermal = ThermalPerformance(observer, sensor.EquipmentIndex);
            if (!AetheriaRuntimeEnergySimulation.TryConsume(
                    observer, catalog, Math.Max(0, sensor.EvaluateStat(6, thermal))))
                return;

            sensor.State.Pinging = true;
            sensor.State.PingCooldown = 1;
            sensor.State.PingLerp = 0;
            sensor.State.PingRadius = 0;
            sensor.State.PingedEntityIndices = Array.Empty<int>();
            sensor.State.PingedEntityCount = 0;
            AetheriaRuntimeVisibilitySimulation.SetTransientSource(
                observer,
                $"sensor:{sensor.EquipmentIndex}:{sensor.BehaviorIndex}",
                sensor.EvaluateStat(7, thermal));
            AppendPingEvent(run, zone, observer, sensor, frameId, "sensor.ping.started");
        }

        private static void StepSensor(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEquippedBehavior sensor,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            IDictionary<int, AetheriaRuntimeEntityContactCommit> contacts,
            AetheriaRuntimeCatalogSnapshot catalog,
            double deltaSeconds,
            double targetInfoDecay,
            long frameId)
        {
            var thermal = ThermalPerformance(observer, sensor.EquipmentIndex);
            if (sensor.State.Pinging)
            {
                var duration = Math.Max(0.000001, ReadNumber(sensor, 10, 2));
                var exponent = ReadNumber(sensor, 11, 0.5);
                sensor.State.PingLerp += deltaSeconds / duration;
                sensor.State.PingRadius = Math.Max(0, sensor.EvaluateStat(8, thermal)) *
                    Math.Pow(Math.Max(0, sensor.State.PingLerp), exponent);
                if (sensor.State.PingLerp > 1)
                {
                    sensor.State.Pinging = false;
                    AppendPingEvent(run, zone, observer, sensor, frameId, "sensor.ping.ended");
                }
            }

            var cooldownDuration = Math.Max(0.000001, sensor.EvaluateStat(9, thermal));
            sensor.State.PingCooldown -= deltaSeconds / cooldownDuration;
            var pinged = new HashSet<int>(sensor.State.PingedEntityIndices ?? Array.Empty<int>());
            var forward = SensorDirection(observer, sensor);
            var sensitivity = Math.Max(0, sensor.EvaluateStat(3, thermal));
            var pingBoost = Math.Max(0, sensor.EvaluateStat(5, thermal));
            var curve = ReadCurveKeys(sensor, 4);
            foreach (var target in entities)
            {
                if (target == null || target.EntityIndex == observer.EntityIndex)
                    continue;

                var dx = target.PositionX - observer.PositionX;
                var dz = target.PositionZ - observer.PositionZ;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                var previous = contacts.TryGetValue(target.EntityIndex, out var contact)
                    ? contact.InfoGathered
                    : 0;
                double next;
                if (!pinged.Contains(target.EntityIndex) && distance < sensor.State.PingRadius)
                {
                    pinged.Add(target.EntityIndex);
                    next = Clamp01(previous + target.Visibility * sensitivity * pingBoost * distance);
                }
                else
                {
                    var direction = Normalize(dx, dz, forward.X, forward.Y);
                    var angle = Math.Acos(Clamp(forward.X * direction.X + forward.Y * direction.Y, -1, 1));
                    var angularSensitivity = curve.Count == 0
                        ? 0
                        : AetheriaRuntimeDaemonItemStatQueries.SampleCurve(curve, angle / Math.PI);
                    next = Clamp01(previous + target.Visibility * sensitivity * angularSensitivity *
                        deltaSeconds / Math.Max(0.000001, distance));
                }
                next *= 1 - Math.Max(0, targetInfoDecay) * deltaSeconds;
                contacts[target.EntityIndex] = new AetheriaRuntimeEntityContactCommit
                {
                    TargetEntityIndex = target.EntityIndex,
                    InfoGathered = next,
                    Hostile = Hostile(observer, target)
                };
            }
            sensor.State.PingedEntityIndices = pinged.OrderBy(index => index).ToArray();
            sensor.State.PingedEntityCount = pinged.Count;
        }

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadCurveKeys(
            AetheriaRuntimeEquippedBehavior behavior,
            int key)
        {
            var value = (behavior.Payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value;
            return ExtractCurveKeys(value).ToArray();
        }

        private static IEnumerable<AetheriaRuntimeCurveKey> ExtractCurveKeys(
            AetheriaRuntimeBehaviorValue? value)
        {
            var children = value?.Children ?? Array.Empty<AetheriaRuntimeBehaviorValue>();
            if (children.Count >= 4 && children.Take(4).All(child =>
                    child != null && string.Equals(child.Kind, "number", StringComparison.OrdinalIgnoreCase)))
            {
                yield return new AetheriaRuntimeCurveKey(
                    children[0].NumberValue,
                    children[1].NumberValue,
                    children[2].NumberValue,
                    children[3].NumberValue);
                yield break;
            }
            foreach (var child in children)
            foreach (var key in ExtractCurveKeys(child))
                yield return key;
        }

        private static (double X, double Y) SensorDirection(
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEquippedBehavior sensor)
        {
            var direction = Normalize(observer.DirectionX, observer.DirectionY, 0, 1);
            return AetheriaRuntimeEquipmentRotation.RotateQuarter(
                direction.X,
                direction.Y,
                AetheriaRuntimeEquipmentRotation.QuarterTurns(sensor.Slot.Rotation));
        }

        private static double ReadNumber(
            AetheriaRuntimeEquippedBehavior behavior,
            int key,
            double fallback)
        {
            var value = (behavior.Payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value;
            return value == null || !double.IsFinite(value.NumberValue) ? fallback : value.NumberValue;
        }

        private static void AppendPingEvent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEquippedBehavior sensor,
            long frameId,
            string kind)
        {
            AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit
            {
                EventId = $"{kind}:{zone.ZoneIndex}:{observer.EntityIndex}:{sensor.EquipmentIndex}:{frameId}",
                Kind = kind,
                FrameId = frameId,
                ZoneIndex = zone.ZoneIndex,
                SourceEntityIndex = observer.EntityIndex,
                SubjectKey = sensor.Item.ItemKey
            });
        }

        private static bool ActorMatches(string actorEntityKey, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(
                actorEntityKey, out var actorZoneIndex, out var actorEntityIndex) &&
            actorZoneIndex == zoneIndex && actorEntityIndex == entityIndex;

        private static double ThermalPerformance(AetheriaRuntimeEntitySnapshotCommit entity, int equipmentIndex) =>
            (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
            .FirstOrDefault(state => state != null && state.EquipmentIndex == equipmentIndex)?
            .ThermalPerformance ?? 1;

        private static bool Hostile(
            AetheriaRuntimeEntitySnapshotCommit left,
            AetheriaRuntimeEntitySnapshotCommit right) =>
            string.Equals(left.FactionKey, "player", StringComparison.OrdinalIgnoreCase) !=
            string.Equals(right.FactionKey, "player", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(left.FactionKey, "raider", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(right.FactionKey, "raider", StringComparison.OrdinalIgnoreCase));

        private static (double X, double Y) Normalize(
            double x, double y, double fallbackX, double fallbackY)
        {
            var length = Math.Sqrt(x * x + y * y);
            return length <= 0.000001 ? (fallbackX, fallbackY) : (x / length, y / length);
        }

        private static double Clamp01(double value) => Clamp(value, 0, 1);
        private static double Clamp(double value, double minimum, double maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
