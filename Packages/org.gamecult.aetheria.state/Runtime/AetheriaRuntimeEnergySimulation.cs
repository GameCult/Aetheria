using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeEnergySimulation
    {
        public static void BeginTick(AetheriaRuntimeEntitySnapshotCommit entity, AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (entity == null) return;
            var previousRadiatorVisibility = RadiatorVisibility(entity);
            entity.Visibility = Math.Max(0, entity.Visibility - previousRadiatorVisibility);
            SetRadiatorVisibility(entity, 0);
            foreach (var capacitor in AetheriaRuntimeEquippedBehaviorQueries.Find(entity, catalog, "Capacitor"))
            {
                capacitor.State.CapacitorCapacity = Math.Max(0, capacitor.EvaluateStat(1));
                capacitor.State.CapacitorEfficiency = Clamp01(capacitor.EvaluateStat(2));
                capacitor.State.CapacitorCharge = Math.Max(0,
                    Math.Min(capacitor.State.CapacitorCapacity, capacitor.State.CapacitorCharge));
            }
            foreach (var reactor in AetheriaRuntimeEquippedBehaviorQueries.Find(entity, catalog, "Reactor"))
                reactor.State.ReactorDraw = 0;
        }

        public static bool CanSupply(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double demand)
        {
            if (demand < 0.01) return true;
            var capacitors = Online(entity, catalog, "Capacitor");
            var charge = capacitors.Sum(value => Math.Max(0, value.State.CapacitorCharge));
            return charge > demand || Online(entity, catalog, "Reactor").Count > 0;
        }

        public static bool TryConsume(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double demand)
        {
            if (demand < 0.01) return true;
            if (!CanSupply(entity, catalog, demand)) return false;
            var capacitors = Online(entity, catalog, "Capacitor");
            while (demand > 0.01)
            {
                var charged = capacitors.Where(value => value.State.CapacitorCharge > 0.01).ToArray();
                if (charged.Length == 0) break;
                var chargeToRemove = demand;
                foreach (var capacitor in charged)
                {
                    var removed = Math.Min(chargeToRemove / charged.Length, capacitor.State.CapacitorCharge);
                    AddCapacitorCharge(entity, catalog, capacitor, -removed);
                    demand -= removed;
                }
            }
            if (demand < 0.01) return true;
            var reactors = Online(entity, catalog, "Reactor");
            if (reactors.Count == 0) return false;
            foreach (var reactor in reactors) reactor.State.ReactorDraw += demand / reactors.Count;
            return true;
        }

        public static void StepRadiators(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double deltaSeconds)
        {
            if (entity == null || catalog == null || deltaSeconds <= 0 || !entity.HeatsinksEnabled) return;
            foreach (var radiator in Online(entity, catalog, "Radiator"))
            {
                var itemTemperature = AetheriaRuntimeThermalSimulation.EquipmentTemperature(
                    entity, catalog, radiator.EquipmentIndex);
                if (radiator.State.RadiatorTemperature <= 0)
                    radiator.State.RadiatorTemperature = itemTemperature;
                radiator.State.PumpedHeat = Math.Max(0, radiator.EvaluateStat(2));
                radiator.State.WasteHeat = Math.Max(0, radiator.EvaluateStat(4));
                radiator.State.EnergyUsage = Math.Max(0, radiator.EvaluateStat(5));
                var tempRatio = Math.Max(radiator.State.RadiatorTemperature / Math.Max(0.000001, itemTemperature), 1);
                if (radiator.State.WasteHeat > 0 &&
                    tempRatio > radiator.State.PumpedHeat / radiator.State.WasteHeat)
                    continue;
                if (!TryConsume(entity, catalog, radiator.State.EnergyUsage * tempRatio * deltaSeconds))
                    continue;
                var temperatureFloor = ReadNumber(radiator.Payload, 3);
                var pumped = radiator.State.PumpedHeat * Math.Max(itemTemperature - temperatureFloor, 0);
                if (pumped >= 0.01)
                {
                    var waste = radiator.State.WasteHeat * tempRatio;
                    AetheriaRuntimeThermalSimulation.AddHeatToEquipment(
                        entity, catalog, radiator.EquipmentIndex, (waste - pumped) * deltaSeconds);
                    var thermalMass = Math.Max(0.000001, radiator.EvaluateStat(6));
                    radiator.State.RadiatorTemperature += pumped / thermalMass * deltaSeconds;
                }
                radiator.State.Emissivity = Math.Max(0, radiator.EvaluateStat(1));
                var radiation = Math.Pow(Math.Max(0, radiator.State.RadiatorTemperature),
                    AetheriaRuntimeThermalSimulation.RadiationExponent) *
                    AetheriaRuntimeThermalSimulation.RadiationMultiplier * radiator.State.Emissivity;
                radiator.State.RadiatorTemperature = Math.Max(0,
                    radiator.State.RadiatorTemperature - radiation * deltaSeconds);
                AddRadiatorVisibility(entity, radiation);
            }
        }

        public static void SettleReactors(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, double deltaSeconds)
        {
            if (entity == null || catalog == null || deltaSeconds <= 0) return;
            var capacitors = Online(entity, catalog, "Capacitor");
            foreach (var reactor in Online(entity, catalog, "Reactor"))
            {
                var charge = Math.Max(0, reactor.EvaluateStat(1)) * deltaSeconds;
                var efficiency = Math.Max(0.000001, reactor.EvaluateStat(2));
                reactor.State.ReactorDraw -= charge;
                var heat = charge / efficiency;
                if (reactor.State.ReactorDraw > 0.01)
                {
                    reactor.State.ReactorLoadRatio = (reactor.State.ReactorDraw + charge) / Math.Max(charge, 0.01);
                    heat += reactor.State.ReactorDraw / Math.Max(0.000001, reactor.EvaluateStat(3));
                    reactor.State.ReactorDraw = 0;
                }
                if (reactor.State.ReactorDraw < -0.01)
                {
                    while (reactor.State.ReactorDraw < -0.01)
                    {
                        var available = capacitors.Where(value =>
                            value.State.CapacitorCharge < value.State.CapacitorCapacity - 0.01).ToArray();
                        if (available.Length == 0) break;
                        var chargeToAdd = -reactor.State.ReactorDraw;
                        foreach (var capacitor in available)
                        {
                            var added = Math.Min(chargeToAdd / available.Length,
                                capacitor.State.CapacitorCapacity - capacitor.State.CapacitorCharge);
                            AddCapacitorCharge(entity, catalog, capacitor, added);
                            reactor.State.ReactorDraw += added;
                        }
                    }
                }
                if (reactor.State.ReactorDraw < -0.01)
                {
                    reactor.State.ReactorLoadRatio = (reactor.State.ReactorDraw + charge) / Math.Max(charge, 0.01);
                    heat -= reactor.State.ReactorDraw / efficiency *
                        (1 - 1 / Math.Max(0.000001, reactor.EvaluateStat(4)));
                    reactor.State.ReactorDraw = 0;
                }
                else reactor.State.ReactorLoadRatio = 1;
                AetheriaRuntimeThermalSimulation.AddHeatToEquipment(
                    entity, catalog, reactor.EquipmentIndex, heat);
            }
        }

        private static IReadOnlyList<AetheriaRuntimeEquippedBehavior> Online(
            AetheriaRuntimeEntitySnapshotCommit entity, AetheriaRuntimeCatalogSnapshot? catalog, string kind) =>
            AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, kind);

        private static void AddCapacitorCharge(AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog, AetheriaRuntimeEquippedBehavior capacitor, double delta)
        {
            capacitor.State.CapacitorCharge = Math.Max(0,
                Math.Min(capacitor.State.CapacitorCapacity, capacitor.State.CapacitorCharge + delta));
            AetheriaRuntimeThermalSimulation.AddHeatToEquipment(entity, catalog, capacitor.EquipmentIndex,
                Math.Abs(delta) * (1 - capacitor.State.CapacitorEfficiency));
        }

        private static void AddRadiatorVisibility(AetheriaRuntimeEntitySnapshotCommit entity, double radiation)
        {
            radiation = Math.Max(0, radiation);
            SetRadiatorVisibility(entity, RadiatorVisibility(entity) + radiation);
            entity.Visibility += radiation;
        }

        private static double RadiatorVisibility(AetheriaRuntimeEntitySnapshotCommit entity) =>
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .FirstOrDefault(value => string.Equals(value.Name, "radiator-visibility", StringComparison.Ordinal))?
            .Values.FirstOrDefault() ?? 0;

        private static void SetRadiatorVisibility(AetheriaRuntimeEntitySnapshotCommit entity, double radiation)
        {
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var grid = grids.FirstOrDefault(value => string.Equals(value.Name, "radiator-visibility", StringComparison.Ordinal));
            if (grid == null)
            {
                grid = new AetheriaRuntimeEntityStatGridCommit { Name = "radiator-visibility", Width = 1, Height = 1 };
                grids.Add(grid);
            }
            grid.Values = new[] { Math.Max(0, radiation) };
            entity.StatGrids = grids;
        }

        private static double ReadNumber(AetheriaRuntimeBehaviorPayload payload, int key) =>
            (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
            .FirstOrDefault(field => field != null && field.Key == key)?.Value?.NumberValue ?? 0;

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
