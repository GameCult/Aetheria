using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeThermalSimulation
    {
        public const string TemperatureGrid = "temperature";
        public const string ThermalMassGrid = "thermal-mass";
        public const string ConductivityGrid = "conductivity";
        public const string MeanTemperatureGrid = "heat";
        public const string MinimumTemperatureGrid = "minimum-temperature";
        public const string MaximumTemperatureGrid = "maximum-temperature";

        // Canonical values from Assets/Resources/Settings.asset in the fossil baseline.
        public const double ConductionMultiplier = 0.01;
        public const double RadiationExponent = 3.0;
        public const double RadiationMultiplier = 0.00000001;
        public const double InitialTemperature = 280.0;
        public const double ThermalWearExponent = 0.01;
        public const double DeltaTemperatureWearExponent = 0.01;
        public const double QualityWearExponent = 2.0;

        public static void EnsureState(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            if (entity == null)
                return;

            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var temperature = Find(grids, TemperatureGrid);
            if (!Valid(temperature))
            {
                temperature = Grid(TemperatureGrid, 1, 1, InitialTemperature);
                grids.Add(temperature);
            }

            EnsureShape(grids, ThermalMassGrid, temperature.Width, temperature.Height, 1.0);
            EnsureShape(grids, ConductivityGrid, temperature.Width, temperature.Height, 1.0);
            EnsureScalar(grids, MeanTemperatureGrid, Mean(temperature.Values));
            EnsureScalar(grids, MinimumTemperatureGrid, temperature.Values.Min());
            EnsureScalar(grids, MaximumTemperatureGrid, temperature.Values.Max());
            entity.StatGrids = grids;
        }

        public static void EnsureTopology(AetheriaRuntimeEntitySnapshotCommit entity, AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null || catalog == null) return;
            var hull = catalog.FindItem(entity.HullItemKey ?? "");
            if (hull == null || hull.ShapeWidth <= 0 || hull.ShapeHeight <= 0 ||
                hull.ShapeCells == null || hull.ShapeCells.Count == 0) return;
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var occupied = new HashSet<int>(hull.ShapeCells.Select(cell => Index(cell.X, cell.Y, hull.ShapeWidth)));
            var temperature = Find(grids, TemperatureGrid);
            if (temperature == null)
            {
                temperature = Grid(TemperatureGrid, hull.ShapeWidth, hull.ShapeHeight, 0);
                grids.Add(temperature);
            }
            if (temperature.Width != hull.ShapeWidth || temperature.Height != hull.ShapeHeight ||
                temperature.Values.Count != hull.ShapeWidth * hull.ShapeHeight)
            {
                temperature.Width = hull.ShapeWidth;
                temperature.Height = hull.ShapeHeight;
                temperature.Values = Enumerable.Range(0, hull.ShapeWidth * hull.ShapeHeight)
                    .Select(index => occupied.Contains(index) ? InitialTemperature : 0).ToArray();
            }
            else if (temperature.Values.All(value => value <= 0))
                temperature.Values = Enumerable.Range(0, hull.ShapeWidth * hull.ShapeHeight)
                    .Select(index => occupied.Contains(index) ? InitialTemperature : 0).ToArray();

            var cellCount = Math.Max(1, hull.ShapeCells.Count);
            var baseMass = Math.Max(0.000001, hull.Mass * hull.SpecificHeat / cellCount);
            var mass = Enumerable.Range(0, hull.ShapeWidth * hull.ShapeHeight)
                .Select(index => occupied.Contains(index) ? baseMass : 0).ToArray();
            var conductivity = Enumerable.Range(0, mass.Length)
                .Select(index => occupied.Contains(index) ? 1.0 : 0).ToArray();
            var installed = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Concat(entity.CargoBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Concat(entity.DockingBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .ToArray();
            for (var equipmentIndex = 0; equipmentIndex < installed.Length; equipmentIndex++)
            {
                var slot = installed[equipmentIndex];
                var item = catalog.FindItem(slot?.Item?.ItemKey ?? "");
                var cells = item == null
                    ? Array.Empty<(int X, int Y)>()
                    : AetheriaRuntimeEquipmentGridGeometry.RotatedCells(
                        item,
                        AetheriaRuntimeEquipmentGridGeometry.ParseRotation(slot?.Rotation));
                var itemCellCount = Math.Max(1, cells.Count);
                foreach (var cell in cells)
                {
                    var x = slot.X + cell.X;
                    var y = slot.Y + cell.Y;
                    if (x < 0 || y < 0 || x >= hull.ShapeWidth || y >= hull.ShapeHeight) continue;
                    var index = Index(x, y, hull.ShapeWidth);
                    if (!occupied.Contains(index)) continue;
                    mass[index] += Math.Max(0, item!.Mass * item.SpecificHeat / itemCellCount);
                    conductivity[index] = Math.Max(0.000001, item.Conductivity);
                }
            }
            SetGrid(grids, ThermalMassGrid, hull.ShapeWidth, hull.ShapeHeight, mass);
            SetGrid(grids, ConductivityGrid, hull.ShapeWidth, hull.ShapeHeight, conductivity);
            EnsureScalar(grids, MeanTemperatureGrid, Mean(temperature.Values.Where(value => value > 0).ToArray()));
            EnsureScalar(grids, MinimumTemperatureGrid, temperature.Values.Where(value => value > 0).DefaultIfEmpty(0).Min());
            EnsureScalar(grids, MaximumTemperatureGrid, temperature.Values.DefaultIfEmpty(0).Max());
            entity.StatGrids = grids;
        }

        public static void AddHeat(AetheriaRuntimeEntitySnapshotCommit entity, double energy)
        {
            if (entity == null || !double.IsFinite(energy) || Math.Abs(energy) < 0.0000001)
                return;

            EnsureState(entity);
            var grids = entity.StatGrids.ToList();
            var temperature = Find(grids, TemperatureGrid)!;
            var thermalMass = Find(grids, ThermalMassGrid)!;
            var values = temperature.Values.ToArray();
            var occupied = Enumerable.Range(0, values.Length)
                .Where(index => thermalMass.Values[index] > 0)
                .ToArray();
            if (occupied.Length == 0)
                return;

            var energyPerCell = energy / occupied.Length;
            foreach (var index in occupied)
                values[index] = Math.Max(0, values[index] + energyPerCell / thermalMass.Values[index]);
            temperature.Values = values;
            entity.StatGrids = grids;
        }

        public static void AddHeatToEquipment(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, int equipmentIndex, double energy)
        {
            if (entity == null || !double.IsFinite(energy) || Math.Abs(energy) < 0.0000001) return;
            EnsureState(entity);
            var cells = EquipmentCellIndices(entity, catalog, equipmentIndex).ToArray();
            if (cells.Length == 0) { AddHeat(entity, energy); return; }
            var grids = entity.StatGrids.ToList();
            var temperature = Find(grids, TemperatureGrid)!;
            var thermalMass = Find(grids, ThermalMassGrid)!;
            var values = temperature.Values.ToArray();
            var energyPerCell = energy / cells.Length;
            foreach (var index in cells)
                if (index >= 0 && index < values.Length && thermalMass.Values[index] > 0)
                    values[index] = Math.Max(0, values[index] + energyPerCell / thermalMass.Values[index]);
            temperature.Values = values;
            entity.StatGrids = grids;
        }

        public static double EquipmentTemperature(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, int equipmentIndex)
        {
            EnsureState(entity);
            var temperature = Find(entity.StatGrids, TemperatureGrid)!;
            var thermalMass = Find(entity.StatGrids, ThermalMassGrid)!;
            var cells = EquipmentCellIndices(entity, catalog, equipmentIndex)
                .Where(index => index >= 0 && index < thermalMass.Values.Count && thermalMass.Values[index] > 0)
                .ToArray();
            if (cells.Length > 0)
                return cells.Average(index => temperature.Values[index]);
            return Enumerable.Range(0, temperature.Values.Count)
                .Where(index => index < thermalMass.Values.Count && thermalMass.Values[index] > 0)
                .Select(index => temperature.Values[index])
                .DefaultIfEmpty(InitialTemperature)
                .Average();
        }

        public static void UpdateEquipmentStates(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double deltaSeconds)
        {
            if (entity == null || catalog == null) return;
            var existing = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .Where(value => value != null).ToDictionary(value => value.EquipmentIndex);
            var states = new List<AetheriaRuntimeEquipmentStateCommit>();
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var index = 0; index < equipment.Count; index++)
            {
                var item = equipment[index]?.Item;
                var typed = catalog.FindItem(item?.ItemKey ?? "");
                if (item == null || typed == null) continue;
                if (!existing.TryGetValue(index, out var state))
                    state = new AetheriaRuntimeEquipmentStateCommit
                    {
                        EquipmentIndex = index,
                        PreviousTemperature = EquipmentTemperature(entity, catalog, index)
                    };
                var temperature = EquipmentTemperature(entity, catalog, index);
                var thermalPerformance = typed.MaximumTemperature <= typed.MinimumTemperature ||
                    typed.ThermalPerformanceCurveKeys == null || typed.ThermalPerformanceCurveKeys.Count == 0
                    ? 1
                    : Clamp01(SampleCurve(typed.ThermalPerformanceCurveKeys,
                        (temperature - typed.MinimumTemperature) /
                        (typed.MaximumTemperature - typed.MinimumTemperature)));
                var maxDurability = Math.Max(0.000001, typed.Durability);
                var durabilityPerformance = Math.Max(0, item.Durability / maxDurability);
                var quality = Clamp01(item.Quality);
                var wear = (1 - Math.Pow(thermalPerformance,
                                (1 - Math.Pow(quality, QualityWearExponent)) * ThermalWearExponent) +
                            Math.Abs(temperature - state.PreviousTemperature) * DeltaTemperatureWearExponent) *
                           maxDurability / Math.Max(0.000001, typed.ThermalResilience);
                state.Temperature = temperature;
                state.ThermalPerformance = thermalPerformance;
                state.DurabilityPerformance = durabilityPerformance;
                state.Wear = Math.Max(0, wear);
                state.ThermalOnline = thermalPerformance > entity.ShutdownPerformance ||
                    entity.OverrideShutdown && item.OverrideShutdown;
                state.DurabilityOnline = item.Durability > 0.01;
                state.Online = item.Enabled && state.ThermalOnline && state.DurabilityOnline;
                state.PreviousTemperature = temperature;
                states.Add(state);

                var wearPayload = (typed.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
                    .FirstOrDefault(value => value != null && value.Kind == "Wear");
                if (state.Online && wearPayload != null)
                {
                    var perSecond = (wearPayload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                        .FirstOrDefault(field => field != null && field.Key == 1)?.Value?.BoolValue ?? true;
                    ApplyWear(entity, index, state.Wear * (perSecond ? deltaSeconds : 1));
                }
            }
            entity.EquipmentStates = states;
        }

        public static void ApplyWear(AetheriaRuntimeEntitySnapshotCommit entity, int equipmentIndex, double damage)
        {
            var equipment = entity?.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (equipmentIndex < 0 || equipmentIndex >= equipment.Count || damage <= 0) return;
            var item = equipment[equipmentIndex]?.Item;
            if (item != null) item.Durability = Math.Max(0, item.Durability - damage);
        }

        public static void Step(AetheriaRuntimeEntitySnapshotCommit entity, double deltaSeconds,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            if (entity == null || deltaSeconds <= 0)
                return;

            EnsureState(entity);
            var grids = entity.StatGrids.ToList();
            var temperature = Find(grids, TemperatureGrid)!;
            var thermalMass = Find(grids, ThermalMassGrid)!;
            var conductivity = Find(grids, ConductivityGrid)!;
            var hull = catalog?.FindItem(entity.HullItemKey ?? "");
            var hullConductivity = Math.Max(0.000001, hull?.Conductivity ?? 1);
            var facetX = Find(grids, "hull_conductivity_x");
            var facetY = Find(grids, "hull_conductivity_y");
            var interior = new HashSet<int>((hull?.InteriorShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
                .Select(cell => Index(cell.X, cell.Y, temperature.Width)));
            var current = temperature.Values.ToArray();
            var next = current.ToArray();
            var radiation = 0.0;

            for (var y = 0; y < temperature.Height; y++)
            for (var x = 0; x < temperature.Width; x++)
            {
                var index = Index(x, y, temperature.Width);
                if (thermalMass.Values[index] <= 0)
                    continue;

                var weightedTemperature = current[index] / ConductionMultiplier;
                var totalConductivity = 1.0 / ConductionMultiplier;
                Accumulate(x - 1, y, facetX, x - 1, y);
                Accumulate(x + 1, y, facetX, x, y);
                Accumulate(x, y - 1, facetY, x, y - 1);
                Accumulate(x, y + 1, facetY, x, y);
                next[index] = weightedTemperature / totalConductivity;

                if (hull != null ? !interior.Contains(index) :
                    IsBorderCell(x, y, temperature.Width, temperature.Height, thermalMass.Values))
                {
                    var emitted = Math.Pow(Math.Max(0, next[index]), RadiationExponent) * RadiationMultiplier;
                    next[index] = Math.Max(0, next[index] - emitted * deltaSeconds);
                    radiation += emitted;
                }

                void Accumulate(int neighborX, int neighborY,
                    AetheriaRuntimeEntityStatGridCommit? facet, int facetXIndex, int facetYIndex)
                {
                    if (neighborX < 0 || neighborY < 0 || neighborX >= temperature.Width || neighborY >= temperature.Height)
                        return;
                    var neighbor = Index(neighborX, neighborY, temperature.Width);
                    if (thermalMass.Values[neighbor] <= 0)
                        return;
                    var transfer = conductivity.Values[index] * conductivity.Values[neighbor] *
                                   FacetFactor(facet, facetXIndex, facetYIndex, hullConductivity) *
                                   thermalMass.Values[neighbor] / thermalMass.Values[index];
                    totalConductivity += transfer;
                    weightedTemperature += current[neighbor] * transfer;
                }
            }

            temperature.Values = next;
            SetScalar(grids, MeanTemperatureGrid, Mean(next));
            SetScalar(grids, MinimumTemperatureGrid, next.Min());
            SetScalar(grids, MaximumTemperatureGrid, next.Max());
            entity.Visibility = Math.Max(0, entity.Visibility - PreviousThermalVisibility(entity)) + radiation;
            SetScalar(grids, "thermal-visibility", radiation);
            entity.StatGrids = grids;
        }

        private static double FacetFactor(AetheriaRuntimeEntityStatGridCommit? grid,
            int x, int y, double hullConductivity)
        {
            if (grid == null || x < 0 || y < 0 || x >= grid.Width || y >= grid.Height) return 1 / hullConductivity;
            var index = Index(x, y, grid.Width);
            return index < grid.Values.Count && grid.Values[index] > 0.5 ? hullConductivity : 1 / hullConductivity;
        }

        private static double PreviousThermalVisibility(AetheriaRuntimeEntitySnapshotCommit entity) =>
            Find(entity.StatGrids, "thermal-visibility")?.Values.FirstOrDefault() ?? 0;

        private static IEnumerable<int> EquipmentCellIndices(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, int equipmentIndex)
        {
            var temperature = Find(entity.StatGrids, TemperatureGrid);
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (temperature == null || catalog == null || equipmentIndex < 0 || equipmentIndex >= equipment.Count)
                yield break;
            var slot = equipment[equipmentIndex];
            var item = catalog.FindItem(slot?.Item?.ItemKey ?? "");
            foreach (var cell in item?.ShapeCells ?? Array.Empty<AetheriaRuntimeShapeCell>())
            {
                var x = slot.X + cell.X;
                var y = slot.Y + cell.Y;
                if (x >= 0 && y >= 0 && x < temperature.Width && y < temperature.Height)
                    yield return Index(x, y, temperature.Width);
            }
        }

        private static bool IsBorderCell(int x, int y, int width, int height, IReadOnlyList<double> mass) =>
            x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
            mass[Index(x - 1, y, width)] <= 0 || mass[Index(x + 1, y, width)] <= 0 ||
            mass[Index(x, y - 1, width)] <= 0 || mass[Index(x, y + 1, width)] <= 0;

        private static int Index(int x, int y, int width) => y * width + x;
        private static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Average();
        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        private static double SampleCurve(IReadOnlyList<AetheriaRuntimeCurveKey> keys, double value)
        {
            var ordered = keys.OrderBy(key => key.Time).ToArray();
            if (value <= ordered[0].Time) return ordered[0].Value;
            for (var index = 1; index < ordered.Length; index++)
            {
                var next = ordered[index];
                var previous = ordered[index - 1];
                if (value > next.Time) continue;
                var span = next.Time - previous.Time;
                if (span <= double.Epsilon) return next.Value;
                var t = Clamp01((value - previous.Time) / span);
                var t2 = t * t;
                var t3 = t2 * t;
                return (2 * t3 - 3 * t2 + 1) * previous.Value +
                       (t3 - 2 * t2 + t) * previous.OutTangent * span +
                       (-2 * t3 + 3 * t2) * next.Value +
                       (t3 - t2) * next.InTangent * span;
            }
            return ordered[ordered.Length - 1].Value;
        }
        private static bool Valid(AetheriaRuntimeEntityStatGridCommit? grid) =>
            grid != null && grid.Width > 0 && grid.Height > 0 && grid.Values.Count == grid.Width * grid.Height;

        private static AetheriaRuntimeEntityStatGridCommit? Find(
            IEnumerable<AetheriaRuntimeEntityStatGridCommit> grids,
            string name) => grids.FirstOrDefault(grid => grid != null && string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase));

        private static void EnsureShape(List<AetheriaRuntimeEntityStatGridCommit> grids, string name, int width, int height, double value)
        {
            var grid = Find(grids, name);
            if (grid == null)
                grids.Add(Grid(name, width, height, value));
            else if (grid.Width != width || grid.Height != height || grid.Values.Count != width * height)
            {
                grid.Width = width;
                grid.Height = height;
                grid.Values = Enumerable.Repeat(value, width * height).ToArray();
            }
        }

        private static void EnsureScalar(List<AetheriaRuntimeEntityStatGridCommit> grids, string name, double value)
        {
            if (Find(grids, name) == null)
                grids.Add(Grid(name, 1, 1, value));
        }

        private static void SetScalar(List<AetheriaRuntimeEntityStatGridCommit> grids, string name, double value)
        {
            EnsureScalar(grids, name, value);
            Find(grids, name)!.Values = new[] { value };
        }

        private static void SetGrid(List<AetheriaRuntimeEntityStatGridCommit> grids,
            string name, int width, int height, IReadOnlyList<double> values)
        {
            var grid = Find(grids, name);
            if (grid == null)
            {
                grid = Grid(name, width, height, 0);
                grids.Add(grid);
            }
            grid.Width = width;
            grid.Height = height;
            grid.Values = values.ToArray();
        }

        private static AetheriaRuntimeEntityStatGridCommit Grid(string name, int width, int height, double value) => new()
        {
            Name = name,
            Width = width,
            Height = height,
            Values = Enumerable.Repeat(value, width * height).ToArray()
        };
    }
}
