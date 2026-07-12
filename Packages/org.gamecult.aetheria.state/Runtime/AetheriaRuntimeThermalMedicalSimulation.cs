using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeThermalMedicalSimulation
    {
        public static AetheriaRuntimeThermalMedicalResult Step(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if (entity == null || catalog == null || deltaSeconds <= 0 || !entity.IsActive)
                return default;

            var cockpit = AetheriaRuntimeEquippedBehaviorQueries.Find(entity, catalog, "Cockpit")
                .OrderBy(value => value.EquipmentIndex)
                .FirstOrDefault();
            if (cockpit == null)
                return default;

            var temperature = AetheriaRuntimeThermalSimulation.EquipmentTemperature(
                entity, catalog, cockpit.EquipmentIndex);
            SetScalar(entity, "cockpit-temperature", temperature);
            var previousHeatstroke = Clamp01(entity.Heatstroke);
            var previousHypothermia = Clamp01(entity.Hypothermia);

            entity.Heatstroke = temperature > settings.HeatstrokeTemperature
                ? Clamp01(previousHeatstroke + Math.Pow(
                    temperature - settings.HeatstrokeTemperature,
                    settings.HeatstrokeExponent) * settings.HeatstrokeMultiplier * deltaSeconds)
                : Clamp01(previousHeatstroke - settings.HeatstrokeRecoveryPerSecond * deltaSeconds);
            entity.Hypothermia = temperature < settings.HypothermiaTemperature
                ? Clamp01(previousHypothermia + Math.Pow(
                    settings.HypothermiaTemperature - temperature,
                    settings.HypothermiaExponent) * settings.HypothermiaMultiplier * deltaSeconds)
                : Clamp01(previousHypothermia - settings.HypothermiaRecoveryPerSecond * deltaSeconds);

            var heatstrokeRisk = Crossed(previousHeatstroke, entity.Heatstroke,
                settings.SevereThermalRiskThreshold);
            var hypothermiaRisk = Crossed(previousHypothermia, entity.Hypothermia,
                settings.SevereThermalRiskThreshold);
            var deathCause = entity.Heatstroke > 0.99 ? "heatstroke" :
                entity.Hypothermia > 0.99 ? "hypothermia" : "";
            return new AetheriaRuntimeThermalMedicalResult(
                cockpit.EquipmentIndex, temperature, heatstrokeRisk, hypothermiaRisk, deathCause);
        }

        private static bool Crossed(double previous, double current, double threshold) =>
            previous < threshold && current > threshold;

        private static void SetScalar(AetheriaRuntimeEntitySnapshotCommit entity, string name, double value)
        {
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var grid = grids.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
            if (grid == null)
            {
                grid = new AetheriaRuntimeEntityStatGridCommit { Name = name, Width = 1, Height = 1 };
                grids.Add(grid);
            }
            grid.Values = new[] { value };
            entity.StatGrids = grids;
        }

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }

    public readonly struct AetheriaRuntimeThermalMedicalResult
    {
        public AetheriaRuntimeThermalMedicalResult(int cockpitEquipmentIndex, double cockpitTemperature,
            bool heatstrokeRiskCrossed, bool hypothermiaRiskCrossed, string deathCause)
        {
            CockpitEquipmentIndex = cockpitEquipmentIndex;
            CockpitTemperature = cockpitTemperature;
            HeatstrokeRiskCrossed = heatstrokeRiskCrossed;
            HypothermiaRiskCrossed = hypothermiaRiskCrossed;
            DeathCause = deathCause ?? "";
        }

        public int CockpitEquipmentIndex { get; }
        public double CockpitTemperature { get; }
        public bool HeatstrokeRiskCrossed { get; }
        public bool HypothermiaRiskCrossed { get; }
        public string DeathCause { get; }
        public bool Died => !string.IsNullOrWhiteSpace(DeathCause);
    }
}
