/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using float2 = Unity.Mathematics.float2;
using float4 = Unity.Mathematics.float4;

public sealed class AetheriaUnityObservedZoneContextFactory
{
    private readonly Dictionary<int, Zone> _observedZoneContextsByDaemonIndex = new Dictionary<int, Zone>();
    private readonly ItemManager _itemManager;
    private readonly PlanetSettings _planetSettings;
    private readonly Func<Galaxy> _resolveObservedGalaxy;
    private readonly Action<string> _logWarning;
    private readonly Action<MusicType> _playMusic;
    private Galaxy _observedGalaxy;

    public AetheriaUnityObservedZoneContextFactory(
        ItemManager itemManager,
        PlanetSettings planetSettings,
        Func<Galaxy> resolveObservedGalaxy,
        Action<string> logWarning,
        Action<MusicType> playMusic)
    {
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _planetSettings = planetSettings ?? throw new ArgumentNullException(nameof(planetSettings));
        _resolveObservedGalaxy = resolveObservedGalaxy ?? (() => null);
        _logWarning = logWarning ?? (_ => { });
        _playMusic = playMusic ?? (_ => { });
    }

    public Zone ResolveContext(
        GalaxyZone galaxyZone,
        AetheriaRuntimeZoneRenderDocument render)
    {
        if (galaxyZone == null)
            throw new ArgumentNullException(nameof(galaxyZone));

        if (render == null)
        {
            _logWarning($"Daemon-authoritative zone population suppressed for {galaxyZone.Name}; no zone-render document was provided.");
            return null;
        }

        var zoneIndex = render.ZoneIndex;
        if (!_observedZoneContextsByDaemonIndex.TryGetValue(zoneIndex, out var observedZoneContext))
        {
            _observedGalaxy = _resolveObservedGalaxy();
            if (_observedGalaxy == null)
            {
                _logWarning($"Daemon-authoritative zone population suppressed for {galaxyZone.Name}; no observed sector projection is available.");
                return null;
            }

            var constructionBlueprint = CreateZoneConstructionBlueprint(render);
            observedZoneContext = new Zone(_itemManager, _planetSettings, constructionBlueprint, galaxyZone, _observedGalaxy);
            _observedZoneContextsByDaemonIndex[zoneIndex] = observedZoneContext;
        }

        _playMusic(MusicType.Overworld);
        observedZoneContext.Log = s => Debug.Log($"Zone: {s}");
        return observedZoneContext;
    }

    private static ZoneConstructionBlueprint CreateZoneConstructionBlueprint(AetheriaRuntimeZoneRenderDocument render)
    {
        var blueprint = new ZoneConstructionBlueprint
        {
            Radius = 2000,
            Mass = 10000,
            Time = (float)render.SimulationTimeSeconds
        };

        foreach (var orbit in render.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
        {
            if (orbit == null)
                continue;

            blueprint.Orbits.Add(new OrbitConstructionData
            {
                OrbitKey = orbit.OrbitKey ?? "",
                ParentOrbitKey = orbit.ParentOrbitKey ?? "",
                Distance = (float)orbit.Distance,
                Phase = (float)orbit.Phase,
                FixedPosition = new float2((float)orbit.FixedPositionX, (float)orbit.FixedPositionY)
            });
        }

        foreach (var body in render.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
        {
            if (body == null)
                continue;

            blueprint.Bodies.Add(CreateBodyConstructionData(body));
        }

        return blueprint;
    }

    private static BodyConstructionData CreateBodyConstructionData(AetheriaRuntimeBodySnapshotCommit body)
    {
        BodyConstructionData data = (body.Kind ?? "").ToLowerInvariant() switch
        {
            "asteroid_belt" => new AsteroidBeltConstructionData
            {
                Asteroids = (body.Asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>())
                    .Where(asteroid => asteroid != null)
                    .Select(asteroid => new Asteroid
                    {
                        Distance = (float)asteroid.Distance,
                        Phase = (float)asteroid.Phase,
                        Size = (float)asteroid.Size,
                        RotationSpeed = (float)asteroid.RotationSpeed
                    })
                    .ToArray()
            },
            "sun" => CreateSunConstructionData(body),
            "gas_giant" => CreateGasGiantConstructionData(body),
            _ => new PlanetConstructionData()
        };

        PopulateBodyConstructionData(data, body);
        return data;
    }

    private static GasGiantConstructionData CreateGasGiantConstructionData(AetheriaRuntimeBodySnapshotCommit body)
    {
        var visual = body.GasGiantVisual ?? new AetheriaRuntimeGasGiantVisualCommit();
        return new GasGiantConstructionData
        {
            FirstOffsetDomainRotationSpeed = (float)visual.FirstOffsetDomainRotationSpeed,
            FirstOffsetRotationSpeed = (float)visual.FirstOffsetRotationSpeed,
            SecondOffsetDomainRotationSpeed = (float)visual.SecondOffsetDomainRotationSpeed,
            SecondOffsetRotationSpeed = (float)visual.SecondOffsetRotationSpeed,
            AlbedoRotationSpeed = (float)visual.AlbedoRotationSpeed,
            WaveRadiusMultiplier = (float)visual.WaveRadiusMultiplier,
            WaveDepthMultiplier = (float)visual.WaveDepthMultiplier,
            WaveDepthExponent = (float)visual.WaveDepthExponent,
            WaveSpeedMultiplier = (float)visual.WaveSpeedMultiplier,
            MaterialOverrides = (visual.MaterialOverrides ?? Array.Empty<string>()).ToList(),
            Colors = (visual.Colors ?? Array.Empty<AetheriaRuntimeColorCommit>())
                .Where(color => color != null)
                .Select(color => new float4((float)color.X, (float)color.Y, (float)color.Z, (float)color.W))
                .ToArray()
        };
    }

    private static SunConstructionData CreateSunConstructionData(AetheriaRuntimeBodySnapshotCommit body)
    {
        var gas = CreateGasGiantConstructionData(body);
        var visual = body.SunVisual ?? new AetheriaRuntimeSunVisualCommit();
        return new SunConstructionData
        {
            FirstOffsetDomainRotationSpeed = gas.FirstOffsetDomainRotationSpeed,
            FirstOffsetRotationSpeed = gas.FirstOffsetRotationSpeed,
            SecondOffsetDomainRotationSpeed = gas.SecondOffsetDomainRotationSpeed,
            SecondOffsetRotationSpeed = gas.SecondOffsetRotationSpeed,
            AlbedoRotationSpeed = gas.AlbedoRotationSpeed,
            WaveRadiusMultiplier = gas.WaveRadiusMultiplier,
            WaveDepthMultiplier = gas.WaveDepthMultiplier,
            WaveDepthExponent = gas.WaveDepthExponent,
            WaveSpeedMultiplier = gas.WaveSpeedMultiplier,
            MaterialOverrides = gas.MaterialOverrides,
            Colors = gas.Colors,
            LightColor = new CultMath.float3((float)visual.LightColorX, (float)visual.LightColorY, (float)visual.LightColorZ),
            FogTintColor = new CultMath.float3((float)visual.FogTintColorX, (float)visual.FogTintColorY, (float)visual.FogTintColorZ),
            LightRadiusMultiplier = (float)visual.LightRadiusMultiplier
        };
    }

    private static void PopulateBodyConstructionData(
        BodyConstructionData data,
        AetheriaRuntimeBodySnapshotCommit body)
    {
        data.BodyKey = body.BodyKey ?? "";
        data.Name = body.Name ?? "";
        data.OrbitKey = body.OrbitKey ?? "";
        data.Mass = (float)body.Mass;
        data.BodyRadiusMultiplier = (float)body.BodyRadiusMultiplier;
        data.GravityRadiusMultiplier = (float)body.GravityRadiusMultiplier;
        data.GravityDepthMultiplier = (float)body.GravityDepthMultiplier;
        data.GravityDepthExponent = (float)body.GravityDepthExponent;
        data.Resources = (body.Resources ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>())
            .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.ItemKey))
            .ToDictionary(resource => resource.ItemKey, resource => (float)resource.Amount, StringComparer.Ordinal);
    }
}
