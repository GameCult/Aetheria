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

        public static void Step(AetheriaRuntimeEntitySnapshotCommit entity, double deltaSeconds)
        {
            if (entity == null || deltaSeconds <= 0)
                return;

            EnsureState(entity);
            var grids = entity.StatGrids.ToList();
            var temperature = Find(grids, TemperatureGrid)!;
            var thermalMass = Find(grids, ThermalMassGrid)!;
            var conductivity = Find(grids, ConductivityGrid)!;
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
                Accumulate(x - 1, y);
                Accumulate(x + 1, y);
                Accumulate(x, y - 1);
                Accumulate(x, y + 1);
                next[index] = weightedTemperature / totalConductivity;

                if (IsBorderCell(x, y, temperature.Width, temperature.Height, thermalMass.Values))
                {
                    var emitted = Math.Pow(Math.Max(0, next[index]), RadiationExponent) * RadiationMultiplier;
                    next[index] = Math.Max(0, next[index] - emitted * deltaSeconds);
                    radiation += emitted;
                }

                void Accumulate(int neighborX, int neighborY)
                {
                    if (neighborX < 0 || neighborY < 0 || neighborX >= temperature.Width || neighborY >= temperature.Height)
                        return;
                    var neighbor = Index(neighborX, neighborY, temperature.Width);
                    if (thermalMass.Values[neighbor] <= 0)
                        return;
                    var transfer = conductivity.Values[index] * conductivity.Values[neighbor] *
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

        private static double PreviousThermalVisibility(AetheriaRuntimeEntitySnapshotCommit entity) =>
            Find(entity.StatGrids, "thermal-visibility")?.Values.FirstOrDefault() ?? 0;

        private static bool IsBorderCell(int x, int y, int width, int height, IReadOnlyList<double> mass) =>
            x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
            mass[Index(x - 1, y, width)] <= 0 || mass[Index(x + 1, y, width)] <= 0 ||
            mass[Index(x, y - 1, width)] <= 0 || mass[Index(x, y + 1, width)] <= 0;

        private static int Index(int x, int y, int width) => y * width + x;
        private static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Average();
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

        private static AetheriaRuntimeEntityStatGridCommit Grid(string name, int width, int height, double value) => new()
        {
            Name = name,
            Width = width,
            Height = height,
            Values = Enumerable.Repeat(value, width * height).ToArray()
        };
    }
}
