/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using GameCult.Aetheria.State.Verse;

public static class AetheriaUnityRenderSettingsBridge
{
    public static AetheriaRuntimeDaemonRenderSettings Build(
        GameSettings settings,
        float targetSpottedBlinkFrequency,
        float targetSpottedBlinkOffset)
    {
        var emissionCurve = settings.GameplaySettings.TemperatureEmissionCurve;
        var bodyIconSizeCurve = settings.IconSize;
        var bodyRadiusCurve = settings.PlanetSettings.BodyRadius;
        var lightRadiusCurve = settings.PlanetSettings.LightRadius;
        var gravityWaveFrequencyCurve = settings.PlanetSettings.WaveFrequency;
        return new AetheriaRuntimeDaemonRenderSettings(
            new AetheriaRuntimeExponentialCurve(
                emissionCurve.Exponent,
                emissionCurve.Multiplier,
                emissionCurve.Constant),
            new AetheriaRuntimeExponentialLerp(
                settings.GameplaySettings.LockIndicatorFrequency.Exponent,
                settings.GameplaySettings.LockIndicatorFrequency.Minimum,
                settings.GameplaySettings.LockIndicatorFrequency.Maximum),
            new AetheriaRuntimeExponentialLerp(
                settings.GameplaySettings.LockSpinSpeed.Exponent,
                settings.GameplaySettings.LockSpinSpeed.Minimum,
                settings.GameplaySettings.LockSpinSpeed.Maximum),
            settings.GameplaySettings.ConvergenceMinimumDistance,
            settings.GameplaySettings.HypothermiaTemperature,
            settings.GameplaySettings.HeatstrokeTemperature,
            settings.GameplaySettings.SevereHeatstrokeRiskThreshold,
            settings.GameplaySettings.TargetDetectionInfoThreshold,
            settings.GameplaySettings.LockIndicatorNoiseAmplitude,
            settings.HeatstrokePhasingFloor,
            settings.HeatstrokePhasingFrequency,
            targetSpottedBlinkFrequency,
            targetSpottedBlinkOffset,
            settings.MinimapZoomLevels?.Select(level => (double)level).ToArray(),
            settings.DefaultMinimapZoom,
            settings.WormholeDistanceRatio,
            settings.DefaultViewDistance,
            settings.MinimapIconSize,
            settings.MinimapAsteroidSize,
            new AetheriaRuntimeExponentialCurve(
                bodyIconSizeCurve.Exponent,
                bodyIconSizeCurve.Multiplier,
                bodyIconSizeCurve.Constant),
            settings.MinimapZoneGravityRange,
            settings.PlanetSettings.AsteroidVerticalOffset,
            settings.PlanetRotationSpeed,
            settings.PlanetSettings.ZoneDepthExponent,
            settings.PlanetSettings.ZoneDepth + settings.PlanetSettings.ZoneBoundaryFog,
            settings.AsteroidMeshCount,
            new AetheriaRuntimeExponentialCurve(
                bodyRadiusCurve.Exponent,
                bodyRadiusCurve.Multiplier,
                bodyRadiusCurve.Constant),
            new AetheriaRuntimeExponentialCurve(
                lightRadiusCurve.Exponent,
                lightRadiusCurve.Multiplier,
                lightRadiusCurve.Constant),
            new AetheriaRuntimeExponentialCurve(
                gravityWaveFrequencyCurve.Exponent,
                gravityWaveFrequencyCurve.Multiplier,
                gravityWaveFrequencyCurve.Constant));
    }
}
