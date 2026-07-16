using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeFlightSimulation
    {
        public static void Step(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit>? entities,
            IReadOnlyList<AetheriaRuntimeDaemonMovementIntent>? movements,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double deltaSeconds,
            double aetherTorqueMultiplier,
            double aetherHeatMultiplier,
            double torqueFloor,
            double torqueMultiplier)
        {
            if (catalog == null || deltaSeconds <= 0)
                return;

            var active = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null && entity.IsActive)
                .ToArray();
            var byIndex = active.ToDictionary(entity => entity.EntityIndex);
            foreach (var movement in movements ?? Array.Empty<AetheriaRuntimeDaemonMovementIntent>())
            {
                if (movement == null ||
                    !TryParseEntityIndex(movement.ActorEntityKey, out var entityIndex) ||
                    !byIndex.TryGetValue(entityIndex, out var entity))
                {
                    continue;
                }

                SetHelmAxes(entity, movement);
            }

            foreach (var entity in active.Where(entity =>
                         string.Equals(entity.Kind, "ship", StringComparison.OrdinalIgnoreCase)))
            {
                var mass = ResolveMass(entity, byIndex, catalog, new HashSet<int>());
                var deltaRotation = ResolveTurnAxis(entity, deltaSeconds);
                var thrusters = Online(entity, catalog, AetheriaRuntimeBehaviorKinds.Thruster);
                ConfigureThrusterAxes(entity, thrusters, catalog, deltaRotation, torqueFloor);
                var activeThrusters = new List<AetheriaRuntimeEquippedBehavior>();
                foreach (var thruster in thrusters)
                    if (StepThruster(entity, thruster, mass, deltaSeconds, catalog, torqueMultiplier))
                        activeThrusters.Add(thruster);
                ApplyThrusterVisibility(entity, activeThrusters);
                SetDriveAxes(entity, catalog, deltaRotation);
                foreach (var drive in Online(entity, catalog, AetheriaRuntimeBehaviorKinds.AetherDrive))
                    StepAetherDrive(entity, drive, mass, deltaSeconds, catalog,
                        aetherTorqueMultiplier, aetherHeatMultiplier);
                ApplyHullDrag(entity, catalog, deltaSeconds);
            }
        }

        private static double ResolveTurnAxis(
            AetheriaRuntimeEntitySnapshotCommit entity,
            double deltaSeconds)
        {
            var direction = Normalize(entity.DirectionX, entity.DirectionY, 0, 1);
            var look = Normalize(entity.LookDirectionX, entity.LookDirectionY, direction.X, direction.Y);
            var right = new Vector2(direction.Y, -direction.X);
            var deltaRotation = look.X * right.X + look.Y * right.Y;
            if (Math.Abs(deltaRotation) < 0.01)
            {
                var blend = Math.Min(deltaSeconds, 1);
                direction = Normalize(
                    direction.X + (look.X - direction.X) * blend,
                    direction.Y + (look.Y - direction.Y) * blend,
                    direction.X,
                    direction.Y);
                entity.DirectionX = direction.X;
                entity.DirectionY = direction.Y;
                deltaRotation = 0;
            }
            else
            {
                deltaRotation = Math.Sqrt(Math.Abs(deltaRotation)) * Sign(deltaRotation);
            }

            return deltaRotation;
        }

        private static void SetHelmAxes(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonMovementIntent movement)
        {
            var magnitude = Clamp01(movement.Magnitude);
            var length = Math.Sqrt(
                movement.DirectionX * movement.DirectionX +
                movement.DirectionY * movement.DirectionY);
            var strafe = length <= 0.000001 ? 0 : movement.DirectionX / length * magnitude;
            var forward = length <= 0.000001 ? 0 : movement.DirectionY / length * magnitude;
            entity.HelmStrafe = strafe;
            entity.HelmForward = forward;
        }

        private static void SetDriveAxes(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            double deltaRotation)
        {
            foreach (var drive in AetheriaRuntimeEquippedBehaviorQueries.Find(
                         entity, catalog, AetheriaRuntimeBehaviorKinds.AetherDrive))
            {
                drive.State.AetherDriveAxisX = Clamp(entity.HelmForward, -1, 1);
                drive.State.AetherDriveAxisY = Clamp(entity.HelmStrafe, -1, 1);
                drive.State.AetherDriveAxisZ = Clamp(deltaRotation, -1, 1);
            }
        }

        private static void ConfigureThrusterAxes(
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyList<AetheriaRuntimeEquippedBehavior> thrusters,
            AetheriaRuntimeCatalogSnapshot catalog,
            double deltaRotation,
            double torqueFloor)
        {
            foreach (var thruster in thrusters)
            {
                thruster.State.ThrusterAxis = 0;
                thruster.State.ThrusterThrust = Math.Max(0,
                    thruster.EvaluateStat(1, ThermalPerformance(entity, thruster.EquipmentIndex)));
                thruster.State.ThrusterTorque = ResolveThrusterTorque(entity, thruster, catalog);
            }

            var right = thrusters.Where(value => Rotation(value) == 1).ToArray();
            var left = thrusters.Where(value => Rotation(value) == 3).ToArray();
            ApplyStrafeBank(right, entity.HelmStrafe, entity.HelmStrafe);
            ApplyStrafeBank(left, -entity.HelmStrafe, entity.HelmStrafe);
            foreach (var thruster in thrusters.Where(value => Rotation(value) == 2))
                thruster.State.ThrusterAxis += entity.HelmForward;
            foreach (var thruster in thrusters.Where(value => Rotation(value) == 0))
                thruster.State.ThrusterAxis -= entity.HelmForward;
            foreach (var thruster in thrusters.Where(value => value.State.ThrusterTorque > torqueFloor))
                thruster.State.ThrusterAxis += deltaRotation;
            foreach (var thruster in thrusters.Where(value => value.State.ThrusterTorque < -torqueFloor))
                thruster.State.ThrusterAxis -= deltaRotation;
            foreach (var thruster in thrusters)
                thruster.State.ThrusterAxis = Clamp01(thruster.State.ThrusterAxis);
        }

        private static void ApplyStrafeBank(
            IReadOnlyList<AetheriaRuntimeEquippedBehavior> bank,
            double baseAxis,
            double strafeAxis)
        {
            var totalTorque = bank.Sum(value => value.State.ThrusterTorque * value.State.ThrusterThrust);
            var sign = Sign(totalTorque);
            var compensation = bank.Where(value => Sign(value.State.ThrusterTorque) == sign).ToArray();
            var compensationPerThruster = compensation.Length == 0
                ? 0
                : Math.Abs(totalTorque) / compensation.Length;
            foreach (var thruster in bank)
            {
                thruster.State.ThrusterAxis += baseAxis;
                if (!compensation.Contains(thruster))
                    continue;
                var denominator = Math.Abs(thruster.State.ThrusterTorque) * thruster.State.ThrusterThrust;
                if (denominator <= 0.000001)
                    continue;
                var correction = strafeAxis * compensationPerThruster / denominator;
                thruster.State.ThrusterAxis += Rotation(thruster) == 1 ? -correction : correction;
            }
        }

        private static bool StepThruster(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeEquippedBehavior thruster,
            double entityMass,
            double deltaSeconds,
            AetheriaRuntimeCatalogSnapshot catalog,
            double torqueMultiplier)
        {
            var axis = Clamp01(thruster.State.ThrusterAxis);
            if (axis <= 0.01 || !AetheriaRuntimeEnergySimulation.TryConsume(
                    entity, catalog,
                    axis * Math.Max(0, thruster.EvaluateStat(4,
                        ThermalPerformance(entity, thruster.EquipmentIndex)))))
                return false;

            var direction = RotateQuarter(
                Normalize(entity.DirectionX, entity.DirectionY, 0, 1),
                Rotation(thruster));
            var thrust = Math.Max(0, thruster.EvaluateStat(1,
                ThermalPerformance(entity, thruster.EquipmentIndex)));
            thruster.State.ThrusterThrust = thrust;
            entity.VelocityX -= direction.X * axis * thrust / entityMass * deltaSeconds;
            entity.VelocityY -= direction.Y * axis * thrust / entityMass * deltaSeconds;
            var rotation = axis * thruster.State.ThrusterTorque * thrust *
                Math.Max(0, torqueMultiplier) / entityMass * deltaSeconds;
            var rotated = RotateRadians(
                Normalize(entity.DirectionX, entity.DirectionY, 0, 1), rotation);
            entity.DirectionX = rotated.X;
            entity.DirectionY = rotated.Y;
            AetheriaRuntimeThermalSimulation.AddHeatToEquipment(
                entity,
                catalog,
                thruster.EquipmentIndex,
                axis * Math.Max(0, thruster.EvaluateStat(3,
                    ThermalPerformance(entity, thruster.EquipmentIndex))) * deltaSeconds);
            return true;
        }

        private static void ApplyThrusterVisibility(
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyList<AetheriaRuntimeEquippedBehavior> thrusters)
        {
            const string gridName = "thruster-visibility";
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var grid = grids.FirstOrDefault(value => string.Equals(value.Name, gridName, StringComparison.Ordinal));
            var previous = grid?.Values.FirstOrDefault() ?? 0;
            var visibility = thrusters
                .Where(value => value.State.ThrusterAxis > 0.01)
                .Select(value => value.State.ThrusterAxis * Math.Max(0,
                    value.EvaluateStat(2, ThermalPerformance(entity, value.EquipmentIndex))))
                .DefaultIfEmpty(0)
                .Max();
            entity.Visibility = Math.Max(0, entity.Visibility - previous) + visibility;
            if (grid == null)
            {
                grid = new AetheriaRuntimeEntityStatGridCommit { Name = gridName, Width = 1, Height = 1 };
                grids.Add(grid);
                entity.StatGrids = grids;
            }
            grid.Values = new[] { visibility };
        }

        private static double ResolveThrusterTorque(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeEquippedBehavior thruster,
            AetheriaRuntimeCatalogSnapshot catalog)
        {
            var hull = catalog.FindItem(entity.HullItemKey ?? "");
            var slot = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .ElementAtOrDefault(thruster.EquipmentIndex);
            if (hull == null || slot == null)
                return 0;
            var hullCenter = ShapeCenter(hull.ShapeCells, 0, 0, 0, hull.ShapeWidth, hull.ShapeHeight);
            var itemCenter = ShapeCenter(
                thruster.CatalogItem.ShapeCells,
                slot.X,
                slot.Y,
                Rotation(thruster),
                thruster.CatalogItem.ShapeWidth,
                thruster.CatalogItem.ShapeHeight);
            var toCenter = Normalize(
                hullCenter.X - itemCenter.X,
                hullCenter.Y - itemCenter.Y,
                0,
                0);
            if (Math.Abs(toCenter.X) <= 0.000001 && Math.Abs(toCenter.Y) <= 0.000001)
                return 0;
            var itemDirection = RotateQuarter(new Vector2(1, 0), Rotation(thruster));
            return -(toCenter.X * itemDirection.X + toCenter.Y * itemDirection.Y);
        }

        private static Vector2 ShapeCenter(
            IReadOnlyList<AetheriaRuntimeShapeCell>? cells,
            int offsetX,
            int offsetY,
            int rotation,
            int width,
            int height)
        {
            var values = (cells ?? Array.Empty<AetheriaRuntimeShapeCell>()).ToArray();
            if (values.Length == 0)
                return new Vector2(offsetX, offsetY);
            double sumX = 0;
            double sumY = 0;
            foreach (var cell in values)
            {
                var x = cell.X;
                var y = cell.Y;
                switch (rotation)
                {
                    case 1: x = height - 1 - cell.Y; y = cell.X; break;
                    case 2: x = width - 1 - cell.X; y = height - 1 - cell.Y; break;
                    case 3: x = cell.Y; y = width - 1 - cell.X; break;
                }
                sumX += x + offsetX;
                sumY += y + offsetY;
            }
            return new Vector2(sumX / values.Length, sumY / values.Length);
        }

        private static int Rotation(AetheriaRuntimeEquippedBehavior behavior)
        {
            return AetheriaRuntimeEquipmentRotation.QuarterTurns(behavior.Slot?.Rotation);
        }

        private static void StepAetherDrive(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeEquippedBehavior drive,
            double entityMass,
            double deltaSeconds,
            AetheriaRuntimeCatalogSnapshot catalog,
            double aetherTorqueMultiplier,
            double aetherHeatMultiplier)
        {
            var axis = new Vector3(
                Clamp(drive.State.AetherDriveAxisX, -1, 1),
                Clamp(drive.State.AetherDriveAxisY, -1, 1),
                Clamp(drive.State.AetherDriveAxisZ, -1, 1));
            var rotorDiameter = ReadVector3(drive.Payload, 1);
            var rotorMass = ReadVector3(drive.Payload, 2);
            var couplingLambda = ReadVector3(drive.Payload, 4);
            var rpm = new Vector3(
                drive.State.AetherDriveRpmX,
                drive.State.AetherDriveRpmY,
                drive.State.AetherDriveRpmZ);
            var maximumRpm = Math.Max(0, drive.EvaluateStat(3, ThermalPerformance(entity, drive.EquipmentIndex)));
            var lambdaMultiplier = Math.Max(0, drive.EvaluateStat(5, ThermalPerformance(entity, drive.EquipmentIndex)));
            var couplingEfficiency = Clamp01(drive.EvaluateStat(6, ThermalPerformance(entity, drive.EquipmentIndex)));
            var passiveCoupling = Math.Max(0, drive.EvaluateStat(10, ThermalPerformance(entity, drive.EquipmentIndex)));

            var direction = Normalize(entity.DirectionX, entity.DirectionY, 0, 1);
            var right = new Vector2(direction.Y, -direction.X);
            var forwardSpeed = entity.VelocityX * direction.X + entity.VelocityY * direction.Y;
            var strafeSpeed = entity.VelocityX * right.X + entity.VelocityY * right.Y;
            var rotorSpeed = rotorDiameter * rpm / 100;
            var efficiency = new Vector3(
                Clamp01(1 - forwardSpeed / Math.Max(rotorSpeed.X, 1) * Sign(axis.X)) * couplingEfficiency,
                Clamp01(1 - strafeSpeed / Math.Max(rotorSpeed.Y, 1) * Sign(axis.Y)) * couplingEfficiency,
                1);

            var idleDecay = Decay(rpm, couplingLambda, deltaSeconds);
            var thrust = (rpm - idleDecay) * rotorMass * efficiency;
            var activeLambda = couplingLambda * lambdaMultiplier * Max(Abs(axis), passiveCoupling);
            var previousRpm = rpm;
            rpm = Decay(rpm, activeLambda, deltaSeconds);
            var rpmLoss = previousRpm - rpm;
            var force = rpmLoss * rotorMass * efficiency;
            var heat = rpmLoss * rotorMass * (1 - couplingEfficiency);
            AetheriaRuntimeThermalSimulation.AddHeatToEquipment(
                entity,
                catalog,
                drive.EquipmentIndex,
                (heat.X + heat.Y + heat.Z) * Math.Max(0, aetherHeatMultiplier));

            var thrustDirection = direction * (axis.X * force.X / entityMass) +
                right * (axis.Y * force.Y / entityMass);
            entity.VelocityX += thrustDirection.X;
            entity.VelocityY += thrustDirection.Y;
            var rotation = force.Z * axis.Z * Math.Max(0, aetherTorqueMultiplier) / entityMass;
            var rotated = RotateRadians(direction, rotation);
            entity.DirectionX = rotated.X;
            entity.DirectionY = rotated.Y;

            var torqueProfile = ReadCurveKeys(drive.Payload, 8);
            var potentialTorque = Math.Max(0, drive.EvaluateStat(7, ThermalPerformance(entity, drive.EquipmentIndex))) *
                new Vector3(
                    AetheriaRuntimeDaemonItemStatQueries.SampleCurve(torqueProfile, Ratio(rpm.X, maximumRpm)),
                    AetheriaRuntimeDaemonItemStatQueries.SampleCurve(torqueProfile, Ratio(rpm.Y, maximumRpm)),
                    AetheriaRuntimeDaemonItemStatQueries.SampleCurve(torqueProfile, Ratio(rpm.Z, maximumRpm)));
            var rotorMassLength = Math.Max(0.000001, rotorMass.Length);
            var potentialRpmDelta = potentialTorque / rotorMassLength * deltaSeconds;
            var actualRpmDelta = Min(new Vector3(maximumRpm, maximumRpm, maximumRpm) - rpm, potentialRpmDelta);
            var torqueRatio = DivideOrZero(actualRpmDelta, potentialRpmDelta);
            var energyDraw = Math.Max(0, drive.EvaluateStat(9, ThermalPerformance(entity, drive.EquipmentIndex)));
            var draw = (torqueRatio.X + torqueRatio.Y + torqueRatio.Z) * energyDraw / 3 * deltaSeconds;
            if (AetheriaRuntimeEnergySimulation.TryConsume(entity, catalog, draw))
                rpm += actualRpmDelta;

            drive.State.AetherDriveThrustX = thrust.X;
            drive.State.AetherDriveThrustY = thrust.Y;
            drive.State.AetherDriveThrustZ = thrust.Z;
            drive.State.AetherDriveRpmX = rpm.X;
            drive.State.AetherDriveRpmY = rpm.Y;
            drive.State.AetherDriveRpmZ = rpm.Z;
            drive.State.AetherDriveMaximumRpm = maximumRpm;
            drive.State.AetherDriveThrustDirectionX = thrustDirection.X;
            drive.State.AetherDriveThrustDirectionY = thrustDirection.Y;
        }

        private static void ApplyHullDrag(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            double deltaSeconds)
        {
            var drag = Math.Max(0, catalog.FindItem(entity.HullItemKey ?? "")?.HullDrag ?? 0);
            var speed = Math.Sqrt(entity.VelocityX * entity.VelocityX + entity.VelocityY * entity.VelocityY);
            if (speed <= 0.01 || drag <= 0)
                return;
            var decayed = speed * Math.Exp(-drag * deltaSeconds);
            entity.VelocityX = entity.VelocityX / speed * decayed;
            entity.VelocityY = entity.VelocityY / speed * decayed;
        }

        private static double ResolveMass(
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyDictionary<int, AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeCatalogSnapshot catalog,
            HashSet<int> path)
        {
            if (!path.Add(entity.EntityIndex))
                throw new InvalidOperationException($"Entity mass cycle contains {entity.EntityIndex}.");
            var mass = Math.Max(0, catalog.FindItem(entity.HullItemKey ?? "")?.Mass ?? 0);
            mass += SlotsMass(entity.Equipment, catalog);
            mass += (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .Sum(bay => SlotsMass(bay?.Items, catalog));
            foreach (var childIndex in entity.ChildEntityIndices ?? Array.Empty<int>())
                if (entities.TryGetValue(childIndex, out var child))
                    mass += ResolveMass(child, entities, catalog, path);
            path.Remove(entity.EntityIndex);
            return Math.Max(0.000001, mass);
        }

        private static double SlotsMass(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots,
            AetheriaRuntimeCatalogSnapshot catalog) =>
            (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Where(slot => slot?.Item != null)
            .Sum(slot => Math.Max(0, catalog.FindItem(slot.Item.ItemKey ?? "")?.Mass ?? 0) *
                Math.Max(1, slot.Item.Quantity));

        private static IReadOnlyList<AetheriaRuntimeEquippedBehavior> Online(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot catalog,
            string kind) =>
            AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, kind);

        private static double ThermalPerformance(AetheriaRuntimeEntitySnapshotCommit entity, int equipmentIndex) =>
            (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
            .FirstOrDefault(state => state != null && state.EquipmentIndex == equipmentIndex)?
            .ThermalPerformance ?? 1;

        private static Vector3 ReadVector3(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            var value = Field(payload, key);
            return new Vector3(ChildNumber(value, 0), ChildNumber(value, 1), ChildNumber(value, 2));
        }

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadCurveKeys(
            AetheriaRuntimeBehaviorPayload payload,
            int key)
        {
            var value = Field(payload, key);
            var serializedKeys = value?.Children != null && value.Children.Count > 0
                ? value.Children[0].Children
                : Array.Empty<AetheriaRuntimeBehaviorValue>();
            return serializedKeys
                .Select(curveKey => new AetheriaRuntimeCurveKey(
                    ChildNumber(curveKey, 0),
                    ChildNumber(curveKey, 1),
                    ChildNumber(curveKey, 2),
                    ChildNumber(curveKey, 3)))
                .ToArray();
        }

        private static AetheriaRuntimeBehaviorValue? Field(AetheriaRuntimeBehaviorPayload payload, int key) =>
            (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
            .FirstOrDefault(field => field != null && field.Key == key)?.Value;

        private static double ChildNumber(AetheriaRuntimeBehaviorValue? value, int index) =>
            value != null && value.Children != null && value.Children.Count > index
                ? value.Children[index].NumberValue
                : 0;

        private static double Ratio(double value, double divisor) => divisor <= 0.000001 ? 0 : value / divisor;
        private static double Sign(double value) => value < 0 ? -1 : value > 0 ? 1 : 0;
        private static double Clamp01(double value) => Clamp(value, 0, 1);
        private static double Clamp(double value, double minimum, double maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;

        private static Vector2 Normalize(double x, double y, double fallbackX, double fallbackY)
        {
            var length = Math.Sqrt(x * x + y * y);
            return length <= 0.000001 ? new Vector2(fallbackX, fallbackY) : new Vector2(x / length, y / length);
        }

        private static Vector2 RotateRadians(Vector2 value, double radians)
        {
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            return Normalize(
                value.X * cosine + value.Y * sine,
                -value.X * sine + value.Y * cosine,
                value.X,
                value.Y);
        }

        private static Vector2 RotateQuarter(Vector2 value, int rotation)
        {
            var rotated = AetheriaRuntimeEquipmentRotation.RotateQuarter(value.X, value.Y, rotation);
            return new Vector2(rotated.X, rotated.Y);
        }

        private static Vector3 Decay(Vector3 source, Vector3 lambda, double deltaSeconds) => new(
            source.X * Math.Exp(-lambda.X * deltaSeconds),
            source.Y * Math.Exp(-lambda.Y * deltaSeconds),
            source.Z * Math.Exp(-lambda.Z * deltaSeconds));

        private static Vector3 Abs(Vector3 value) => new(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z));
        private static Vector3 Max(Vector3 value, double scalar) => new(
            Math.Max(value.X, scalar), Math.Max(value.Y, scalar), Math.Max(value.Z, scalar));
        private static Vector3 Min(Vector3 left, Vector3 right) => new(
            Math.Min(left.X, right.X), Math.Min(left.Y, right.Y), Math.Min(left.Z, right.Z));
        private static Vector3 DivideOrZero(Vector3 value, Vector3 divisor) => new(
            Math.Abs(divisor.X) <= 0.000001 ? 0 : value.X / divisor.X,
            Math.Abs(divisor.Y) <= 0.000001 ? 0 : value.Y / divisor.Y,
            Math.Abs(divisor.Z) <= 0.000001 ? 0 : value.Z / divisor.Z);

        private static bool TryParseEntityIndex(string? entityKey, out int entityIndex)
        {
            entityIndex = -1;
            if (string.IsNullOrWhiteSpace(entityKey)) return false;
            var marker = entityKey.LastIndexOf(".entity.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return false;
            var start = marker + ".entity.".Length;
            var end = start;
            while (end < entityKey.Length && char.IsDigit(entityKey[end])) end++;
            return end > start && int.TryParse(entityKey.Substring(start, end - start), out entityIndex);
        }

        private readonly struct Vector2
        {
            public Vector2(double x, double y) { X = x; Y = y; }
            public double X { get; }
            public double Y { get; }
            public static Vector2 operator *(Vector2 value, double scalar) => new(value.X * scalar, value.Y * scalar);
            public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);
        }

        private readonly struct Vector3
        {
            public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
            public static Vector3 One => new(1, 1, 1);
            public static Vector3 operator +(Vector3 left, Vector3 right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
            public static Vector3 operator -(Vector3 left, Vector3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
            public static Vector3 operator *(Vector3 left, Vector3 right) => new(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
            public static Vector3 operator *(Vector3 value, double scalar) => new(value.X * scalar, value.Y * scalar, value.Z * scalar);
            public static Vector3 operator *(double scalar, Vector3 value) => value * scalar;
            public static Vector3 operator /(Vector3 value, double scalar) => new(value.X / scalar, value.Y / scalar, value.Z / scalar);
        }
    }
}
