/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MessagePack;
using UniRx;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using Random = CultMath.Random;

public static class ZoneGenerator
{
	private class Circle
	{
		public float2 Center;
		public float Radius;

		public float Area => PI * Radius * Radius;

		public Circle(float2 center, float radius)
		{
			Center = center;
			Radius = radius;
		}

		public float DistanceTo(float2 point) => length(point - Center) - Radius;
		public float DistanceTo(Circle other) => length(other.Center - Center) - Radius - other.Radius;
	}

	private const int MaximumPlacementSamples = 32;

	// ambient: false keeps the planets, orbits and stations but generates no ship and no turret, so nothing wanders
	// into a scenario's authored conditions (docs/scenarios-cut.md, 2.1). The rule is applied here, not by pruning.
	public static ZonePack GenerateZone(
		ItemManager itemManager,
		ZoneGenerationSettings zoneSettings,
		PlanetSettings planetSettings,
		Galaxy galaxy,
		GalaxyZone galaxyZone,
		bool isTutorial = false,
		bool ambient = true)
	{
		var pack = new ZonePack();
		var cache = itemManager.ItemData;

		var seed = galaxyZone.Name.StableHash() ^ (uint) pcg3d(galaxyZone.Position).x;
		var random = new Random(seed);

		var density = saturate(galaxy.Background.CloudDensity(galaxyZone.Position)/2);
		pack.Radius = zoneSettings.ZoneRadius.Evaluate(density);
		pack.Mass = zoneSettings.ZoneMass.Evaluate(density);
		var targetSubzoneCount = zoneSettings.SubZoneCount.Evaluate(density);

		//Debug.Log($"Generating zone at position {zone.Position} with radius {zoneRadius} and mass {zoneMass}");

		var planets = new List<GeneratorPlanet>();
		if (targetSubzoneCount > 1)
		{
			var zoneBoundary = new Circle(float2.zero, pack.Radius * zoneSettings.ZoneBoundaryRadius);
			float boundaryTangentRadius(float2 point) => -zoneBoundary.DistanceTo(point);

			var occupiedAreas = new List<Circle>();
			float tangentRadius(float2 point) => min(boundaryTangentRadius(point), occupiedAreas.Min(circle => circle.DistanceTo(point)));

			var startPosition = random.NextFloat(pack.Radius * .25f, pack.Radius * .5f) * random.NextFloat2Direction();
			occupiedAreas.Add(new Circle(startPosition, boundaryTangentRadius(startPosition)));

			int samples = 0;
			while (occupiedAreas.Count < targetSubzoneCount && samples < MaximumPlacementSamples)
			{
				samples = 0;
				for (int i = 0; i < MaximumPlacementSamples; i++)
				{
					var samplePos = random.NextFloat2(-pack.Radius, pack.Radius);
					var rad = tangentRadius(samplePos);
					if (rad > 0)
					{
						occupiedAreas.Add(new Circle(samplePos, rad));
						break;
					}

					samples++;
				}
			}

			var totalArea = occupiedAreas.Sum(c => c.Area);
			// Every area root is massive, so a root's reach is Reach x its nearest area's centre (WellTree.SubzoneReach)
			var centres = occupiedAreas.Select(c => c.Center).ToList();
			for (var k = 0; k < occupiedAreas.Count; k++)
			{
				var c = occupiedAreas[k];
				planets.AddRange(GenerateEntities(zoneSettings, planetSettings, ref random, c.Area / totalArea * pack.Mass, c.Radius, c.Center,
					WellTree.SubzoneReach(centres, k)));
			}
		}
		else
			planets.AddRange(GenerateEntities(zoneSettings, planetSettings, ref random, pack.Mass, pack.Radius, float2.zero, float.PositiveInfinity));

        // Every orbit is a record; parents link to records, so each orbit holds its key before any child names it
        var orbitMap = new Dictionary<GeneratorPlanet, OrbitData>();
        foreach (var planet in planets)
            cache.Upsert(orbitMap[planet] = new OrbitData
            {
	            FixedPosition = planet.FixedPosition,
                Distance = planet.Distance,
                Phase = planet.Phase
            });
        foreach (var planet in planets.Where(p => p.Parent != null))
        {
            orbitMap[planet].Parent = cache.RefOf(orbitMap[planet.Parent]);
            cache.Upsert(orbitMap[planet]);
        }
        pack.Orbits = planets.Select(planet => cache.RefOf(orbitMap[planet])).ToList();

        pack.Planets = planets.Where(p=>!p.Empty).Select(planet =>
        {
	        BodyType bodyType = planet.Belt ? BodyType.Asteroid :
		        planet.Mass > zoneSettings.SunMass ? BodyType.Sun :
		        planet.Mass > zoneSettings.GasGiantMass ? BodyType.GasGiant :
		        planet.Mass > zoneSettings.PlanetMass ? BodyType.Planet : BodyType.Planetoid;

	        BodyData planetData;
	        switch (bodyType)
	        {
		        case BodyType.Asteroid:
			        planetData = new AsteroidBeltData();
			        break;
		        case BodyType.Planetoid:
		        case BodyType.Planet:
			        planetData = new PlanetData();
			        break;
		        case BodyType.GasGiant:
			        planetData = new GasGiantData();
			        break;
		        case BodyType.Sun:
			        planetData = new SunData();
			        break;
		        default:
			        throw new ArgumentOutOfRangeException();
	        }

	        planetData.Mass = planet.Mass;
	        planetData.Orbit = cache.RefOf(orbitMap[planet]);
            if (planetData is AsteroidBeltData beltData)
            {
	            beltData.Asteroids =
		            Enumerable.Range(0, (int) zoneSettings.AsteroidCount.Evaluate(beltData.Mass * orbitMap[planet].Distance))
			            .Select(_ => new Asteroid
			            {
				            Distance = orbitMap[planet].Distance + random.NextFloat()*(random.NextFloat()-.5f)*2*planet.BeltHalfWidth,
				            Phase = random.NextFloat(),
				            Size = random.NextFloat(),
				            RotationSpeed = zoneSettings.AsteroidRotationSpeed.Evaluate(random.NextFloat())
			            })
			            //.OrderByDescending(a=>a.Size)
			            .ToArray();
            }
            else if (planetData is GasGiantData gas)
            {
	            if (gas is SunData sun)
	            {
		            var hueDraw = random.NextFloat();
		            float primary = ColorMath.HueOutsideBand(hueDraw, zoneSettings.SunHueExcludedStart, zoneSettings.SunHueExcludedEnd);
		            float secondary = ColorMath.SecondaryHueOutsideBand(hueDraw, random.NextFloat() > .5, zoneSettings.SunSecondaryColorDistance,
			            zoneSettings.SunHueExcludedStart, zoneSettings.SunHueExcludedEnd);
		            gas.Colors = new []
		            {
			            float4(ColorMath.HsvToRgb(float3(primary, zoneSettings.SunColorSaturation, .5f)), 0),
			            float4(ColorMath.HsvToRgb(float3(secondary, zoneSettings.SunColorSaturation, 1)), 1)
		            };
		            sun.FogTintColor = ColorMath.HsvToRgb(float3(primary, zoneSettings.SunFogTintSaturation, 1));
			        sun.LightColor = ColorMath.HsvToRgb(float3(primary, zoneSettings.SunLightSaturation, 1));
		            gas.FirstOffsetDomainRotationSpeed = 5;
	            }
	            else
	            {
		            // Define primary color and two adjacent colors
		            float primary = random.NextFloat();
		            float right = frac(primary + zoneSettings.GasGiantBandColorSeparation);
		            float left = frac(primary + 1 - zoneSettings.GasGiantBandColorSeparation);

		            // Create n time keys from 0 to 1
		            var bandCount = (int) (zoneSettings.GasGiantBandCount.Evaluate(random.NextFloat()) + .5f);
		            var times = Enumerable.Range(0, bandCount)
			            .Select(i => (float) i / (bandCount-1));

		            // Each band has a chance of being either the primary or one of the adjacent hues
		            // Saturation and Value are random with curves applied
		            gas.Colors = times
			            .Select(time => float4(ColorMath.HsvToRgb(float3(
				            random.NextFloat() > zoneSettings.GasGiantBandAltColorChance ? primary : (random.NextFloat() > .5f ? right : left),
				            zoneSettings.GasGiantBandSaturation.Evaluate(random.NextFloat()),
				            zoneSettings.GasGiantBandSaturation.Evaluate(random.NextFloat()))),time))
			            .ToArray();

		            gas.FirstOffsetDomainRotationSpeed = 0;
	            }
	            gas.AlbedoRotationSpeed = -3;
	            gas.FirstOffsetRotationSpeed = 5;
	            gas.SecondOffsetRotationSpeed = 10;
	            gas.SecondOffsetDomainRotationSpeed = -25;
            }

            // A body is named for the first eight characters of the key the cache mints for it
            var bodyKey = cache.Upsert(planetData).Key;
            planetData.Name = bodyKey.Value.Substring(0, 8);
            // Mining Cut 3 (Q16 B): a belt's field kind is chosen here, at generation, and saved with it.
            if (planetData is AsteroidBeltData generatedBelt) FieldKinds.Ensure(bodyKey, generatedBelt, cache);
            return cache.Upsert(planetData);
        }).ToList();

        // Trojan swarms (rulings trojans-first, trojans-ride-host-period-with-stations): a belt on a copy of its host's rail, its
        // rocks round both L4 and L5. Drawn from a stream of its own, so no other draw in the zone moves; not a GeneratorPlanet,
        // so it takes no slot and no station candidacy sees it.
        var swarmRandom = new Random(seed ^ TrojanStream);
        foreach (var host in planets.Where(p => p.Parent != null && !p.Parent.Empty && !p.Empty && !p.Belt && p.Distance > 0
                     && p.Mass > zoneSettings.PlanetMass && p.Mass <= zoneSettings.SunMass))
        {
            if (swarmRandom.NextFloat() >= zoneSettings.TrojanProbability) continue;
            var hostOrbit = orbitMap[host];
            var rail = new OrbitData { Parent = hostOrbit.Parent, Distance = hostOrbit.Distance, Phase = hostOrbit.Phase };
            pack.Orbits.Add(cache.Upsert(rail));
            var swarm = new AsteroidBeltData
            {
                Orbit = cache.RefOf(rail), RidesRail = true,
                Asteroids = TrojanRocks(zoneSettings, ref swarmRandom, rail.Distance, rail.Phase)
            };
            BodyData swarmBody = swarm;
            var swarmKey = cache.Upsert(swarmBody).Key;
            swarm.Name = swarmKey.Value.Substring(0, 8);
            FieldKinds.Ensure(swarmKey, swarm, cache);
            pack.Planets.Add(cache.Upsert(swarmBody));
        }

        // The generator's own well tree, for clearance only (the Zone builds its own): what a rail must stay out of is the body's
        // support, or its Roche margin if more.
        var wellNodes = planets.Select(p => new WellNode(cache.RefOf(orbitMap[p]).Key, p.Parent == null ? default : cache.RefOf(orbitMap[p.Parent]).Key,
            p.Distance, p.Phase, p.Empty || p.Belt ? 0f : p.Mass,
            p.Belt ? p.Distance + p.BeltHalfWidth : p.Distance, p.FixedPosition)).ToList();
        var wells = WellTree.Build(wellNodes, planetSettings, pack.Radius);
        float Extent(GeneratorPlanet p)
        {
            var key = cache.RefOf(orbitMap[p]).Key;
            var roche = p.Empty || p.Belt ? 0f : WellTree.Curves(planetSettings, p.Mass).Softening * RocheMargin;
            return Math.Max(wells.Support(key), roche);
        }
        bool Massive(GeneratorPlanet p) => wells.Reach(cache.RefOf(orbitMap[p]).Key) > 0f;

        var nearestFaction = galaxy.Factions.MinBy(f => galaxy.HomeZones[f].Distance[galaxyZone]);
        var nearestFactionHomeZone = galaxy.HomeZones[nearestFaction];
        var factionPresence = galaxy.InfluenceOf(nearestFaction) - nearestFactionHomeZone.Distance[galaxyZone] + 1;

        var storyStations = galaxyZone.Locations.Where(story => story.Type == LocationType.Station).ToArray();
        var stationCount = (int)(random.NextFloat() * (factionPresence + 1)) + storyStations.Length;
        // Enemy count below is based on this pre-override value, so the tutorial entrance doesn't gain an enemy
        // just because its station got forced.
        var baseStationCount = stationCount;
        // Operator ruling, 2026-09-25 (docs/locomotion-cut.md, "Tutorial station"): the entrance zone always
        // gets a generated faction station with a docking bay, deterministically. Galaxy.cs halves influence and
        // picks the lowest-influence unowned zone for Entrance, which otherwise rolls stationCount 0 on every
        // seed. galaxy.Entrance is reference-identity, not a name comparison.
        var isTutorialEntrance = isTutorial && galaxyZone == galaxy.Entrance;
        if (isTutorialEntrance && stationCount < 1) stationCount = 1;
        // A station's source is a well-bearing body alone on its ring whose rail clears every massive sibling's extent (the
        // sibling's support, or its Roche margin if more) and the largest reach of any station or turret hull: whatever hull
        // is built there, its Lagrange orbit then sits clear of any other body.
        var cellSize = itemManager.GameplaySettings.SchematicCellSize;
        var largestReach = cache.GetAll<HullData>().Where(h => h.HullType == HullType.Station || h.HullType == HullType.Turret)
	        .Select(h => h.Reach(cellSize)).DefaultIfEmpty(0f).Max();
        var candidates = planets
	        .Where(p => p.Parent != null && !p.Empty && !p.Belt
		        && p.Parent.Children.TrueForAll(c => c == p || (abs(c.Distance - p.Distance) >= WellTree.RingWindow
			        && (!Massive(c) || abs(c.Distance - p.Distance) > Extent(c) + largestReach))))
	        .OrderBy(p => p.Distance)
	        .ToArray();
        // (the rail, how far a body on it keeps others out, whether it is a station's own orbit rather than a base's)
        var sources = candidates.Skip(candidates.Length / 2).Take(stationCount)
	        .Select(p => (orbit: orbitMap[p], extent: Extent(p), direct: false)).ToArray();
        if (isTutorialEntrance && candidates.Length == 0)
        {
	        // The entrance gets a station whatever its bodies: it orbits a root node beyond its ring group (or, for a body, its
	        // whole family), never at a ring member's distance, and sits there, not at a Lagrange point.
	        var roots = planets.Where(p => p.Parent == null).ToList();
	        var rootNode = roots.FirstOrDefault(p => p.Children.Count > 0) ?? (roots.Count > 1 ? roots.OrderByDescending(p => p.Mass).First() : null);
	        if (rootNode == null)
		        throw new InvalidOperationException(
			        "The tutorial entrance zone must always get a station, but it has no candidate orbit at all -- every planet in the zone is a parentless root.");
	        var distance = rootNode.Empty
		        ? (1 + GravityField.RingReach) * GravityField.Beta * rootNode.Children.Where(Massive).Min(c => c.Distance)
		        : Extent(rootNode);
	        var direct = new OrbitData { Parent = cache.RefOf(orbitMap[rootNode]), Distance = distance, Phase = random.NextFloat() };
	        pack.Orbits.Add(cache.Upsert(direct));
	        sources = new[] { (orbit: direct, extent: float.NegativeInfinity, direct: true) };
        }
        var selectedStationOrbits = sources.Select(x => x.orbit).ToArray();
        var extentOf = sources.ToDictionary(x => x.orbit, x => x.extent);

        var loadoutGenerators = new Dictionary<Faction, LoadoutGenerator>();

        LoadoutGenerator GetLoadoutGenerator(Faction faction)
        {
	        if (!loadoutGenerators.ContainsKey(faction))
		        loadoutGenerators[faction] = new LoadoutGenerator(ref random, itemManager, galaxy, galaxyZone, faction, .5f);
	        return loadoutGenerators[faction];
        }

        // L4 or L5: 60 degrees (one sixth of a turn) ahead of or behind the planet on its orbit. Phase is in turns.
        OrbitData CreateLagrangeOrbit(OrbitData baseOrbit)
        {
	        if (sources.First(x => x.orbit == baseOrbit).direct) return baseOrbit;
	        var lagrangeOrbit = new OrbitData
	        {
		        Parent = baseOrbit.Parent,
		        Distance = baseOrbit.Distance,
		        Phase = baseOrbit.Phase + sign(random.NextFloat() - .5f) / 6f,
	        };
	        pack.Orbits.Add(cache.Upsert(lagrangeOrbit));
	        return lagrangeOrbit;
        }

        // A turret co-orbits with its station, on one side of it (side is -1 behind, +1 ahead), clear of the body inside
        // it on that side by one of the turret's own radii: its centre sits innerReach + 2 turretReach beyond that
        // body's. Every body's reach counts, so turrets of differing reach clear each other. Phase is in turns, so the
        // arc length divides by the orbit's circumference. Returns where the turret sits (signed arc from the station)
        // and its reach, or null when there is no turret hull to generate, before any orbit is created.
        (float arc, float reach, bool placed)? PlaceTurret(OrbitData orbit, LoadoutGenerator loadoutGenerator, float side, float innerArc, float innerReach,
	        OrbitData baseOrbit)
        {
	        var turret = loadoutGenerator.GenerateTurretLoadout();
	        if (turret == null) return null;
	        var turretReach = (itemManager.GetData(turret.Hull) as HullData).Reach(itemManager.GameplaySettings.SchematicCellSize);
	        var arc = innerArc + side * (innerReach + 2 * turretReach);
		        // A turret whose centre is within the base body's extent plus its own reach of the base is not placed: its side stops.
		        var phase = orbit.Phase + arc / (2 * PI * orbit.Distance);
		        if (2 * orbit.Distance * abs(sin(PI * (phase - baseOrbit.Phase))) < extentOf[baseOrbit] + turretReach)
		        {
			        itemManager.Log("A turret was not placed: its station's base body is within its reach (story-turret-tail-clearance).");
			        return (arc, turretReach, false);
		        }

	        var turretOrbit = new OrbitData
	        {
		        Parent = orbit.Parent,
		        Distance = orbit.Distance,
		        Phase = phase

	        };
	        pack.Orbits.Add(cache.Upsert(turretOrbit));
	        turret.Orbit = cache.RefOf(turretOrbit);
	        pack.Entities.Add(turret);
	        return (arc, turretReach, true);
        }

        // Turrets flank the station alternately, behind then ahead, each side growing outward from the station.
        void PlaceTurrets(OrbitData orbit, LoadoutGenerator loadoutGenerator, int count, float stationReach, OrbitData baseOrbit)
        {
	        var arcs = new float[2];
		var stopped = new bool[2];
	        var reaches = new[] { stationReach, stationReach };
	        for(int t=0; t<count; t++)
	        {
		        var side = t % 2;
		        if (stopped[side]) continue;
		var placed = PlaceTurret(orbit, loadoutGenerator, side == 0 ? -1f : 1f, arcs[side], reaches[side], baseOrbit);
		if (placed == null) return;
		if (!placed.Value.placed) { stopped[side] = true; continue; }
		(arcs[side], reaches[side]) = (placed.Value.arc, placed.Value.reach);
	        }
        }

        float StationReach(OrbitalEntityPack station) =>
	        (itemManager.GetData(station.Hull) as HullData).Reach(itemManager.GameplaySettings.SchematicCellSize);

        for (var i = 0; i < storyStations.Length; i++)
        {
	        var story = storyStations[i];
	        if (i >= selectedStationOrbits.Length) { itemManager.Log($"Story station for {story.Faction?.Name} has no rail clear of the bodies; its story has nowhere to live!"); continue; }
	        var orbit = selectedStationOrbits[i];
	        var lagrangeOrbit = CreateLagrangeOrbit(orbit);
	        var station = GetLoadoutGenerator(story.Faction).GenerateStationLoadout();
	        if (station == null)
	        {
		        itemManager.Log($"Story station for {story.Faction?.Name} could not be generated; its story has nowhere to live!");
		        continue;
	        }
	        station.Orbit = cache.RefOf(lagrangeOrbit);
	        station.SecurityLevel = story.Security;
	        station.SecurityRadius = pack.Radius;
	        station.Story = i;
	        pack.Entities.Add(station);

	        if (ambient) PlaceTurrets(lagrangeOrbit, GetLoadoutGenerator(story.Faction), story.Turrets, StationReach(station), orbit);
        }

        for (var i = storyStations.Length; i < selectedStationOrbits.Length; i++)
        {
	        var orbit = selectedStationOrbits[i];
	        var security = (SecurityLevel) ((int) ((1f - pow(random.NextFloat(), factionPresence / 2f)) * 3f));

	        var lagrangeOrbit = CreateLagrangeOrbit(orbit);
	        var station = GetLoadoutGenerator(nearestFaction).GenerateStationLoadout();
	        if (station == null)
	        {
		        if (isTutorialEntrance)
			        throw new InvalidOperationException(
				        $"The tutorial entrance zone must always get a station, but {nearestFaction?.Name} has no station loadout to generate.");
		        continue;
	        }
	        station.Orbit = cache.RefOf(lagrangeOrbit);
	        station.SecurityLevel = security;
	        station.SecurityRadius = pack.Radius;
	        pack.Entities.Add(station);

	        if (ambient) PlaceTurrets(lagrangeOrbit, GetLoadoutGenerator(nearestFaction), 2, StationReach(station), orbit);
        }

        // A boss zone gets its faction's boss whether or not the zone is ambient: the boss is the run's goal, not
        // wandering traffic. RunGoal reads the mark; nothing else decides whether a boss lives.
        foreach (var bossFaction in galaxy.BossZones.Where(entry => entry.Value == galaxyZone).Select(entry => entry.Key))
        {
	        var boss = GetLoadoutGenerator(bossFaction).GenerateBossLoadout();
	        if (boss == null)
	        {
		        itemManager.Log($"No boss could be generated for {bossFaction.Name}; its boss zone has no boss to kill!");
		        continue;
	        }
	        boss.Boss = true;
	        pack.Entities.Add(boss);
	        pack.BossSpawned = true;
        }

        if (galaxyZone == galaxy.Exit && !pack.BossSpawned)
	        itemManager.Log("Galaxy.Exit has no boss: Galaxy.BossZones does not map a boss faction to the exit zone, or its boss could not be generated (ZoneGenerator exit-without-boss). The exit gate stays sealed.");

        if (!ambient) return pack;

        var enemyCount = (int)(random.NextFloat() * factionPresence * 2) + baseStationCount;
        for (int i = 0; i < enemyCount; i++)
        {
	        var ship = GetLoadoutGenerator(nearestFaction).GenerateShipLoadout();
	        if (ship == null) continue;
	        pack.Entities.Add(ship);
        }

        // Neutral wanderers: ships belonging to a faction that neither owns the zone nor is its
        // nearest faction, so existing hostility rules leave them non-hostile to the player.
        // Testing affordance for hand-testing combat/targeting; none spawn if no faction qualifies.
        var eligibleWandererFactions = EligibleWandererFactions(galaxy.Factions, galaxyZone.Owner, nearestFaction);
        if (zoneSettings.NeutralWandererCount > 0 && eligibleWandererFactions.Length > 0)
        {
	        var wandererFaction = eligibleWandererFactions[random.NextInt(eligibleWandererFactions.Length)];
	        var wandererLoadoutGenerator = GetLoadoutGenerator(wandererFaction);
	        for (int i = 0; i < zoneSettings.NeutralWandererCount; i++)
	        {
		        var ship = wandererLoadoutGenerator.GenerateShipLoadout();
		        if (ship == null) continue;
		        pack.Entities.Add(ship);
	        }
        }

        return pack;
	}

	// The neutral-wanderer faction pool: every galaxy faction except the zone owner and the nearest
	// faction (whose ships are already the zone's hostile-when-trespassing/enemy spawns). Extracted so
	// the selection rule is testable without running the full zone-generation pipeline.
	public static Faction[] EligibleWandererFactions(Faction[] factions, Faction owner, Faction nearest) =>
		factions.Where(f => f != owner && f != nearest).ToArray();

	// static float ResourceValue(ref Random random, ZoneGenerationSettings settings, SimpleCommodityData resource, float density)
	// {
	// 	return random.NextPowerDistribution(resource.Minimum, resource.Maximum, resource.Exponent,
	// 		1 / lerp(settings.ResourceDensityMinimum, settings.ResourceDensityMaximum, density));
	// }

	public static GeneratorPlanet[] GenerateEntities(ZoneGenerationSettings settings, PlanetSettings planetSettings, ref Random random, float mass, float radius, float2 fixedPosition,
		float rootReach)
	{
		var root = new GeneratorPlanet
		{
			FixedPosition = fixedPosition,
			Settings = settings,
			Mass = mass
			};

		// There is some chance of generating a rosette or binary system
		// Probabilities which are fixed for the entire galaxy are in GlobalData, contained in the GameContext
		var rosette = random.NextFloat() < settings.RosetteProbability;

		if (rosette)
		{
			// Create a rosette with a number of vertices between 2 and 9 inclusive
			root.ExpandRosette(ref random, (int)(random.NextFloat(1, 5) + random.NextFloat(1, 5)));

			// Create a small number of less massive "captured" planets orbiting past the rosette
			root.ExpandSolar(
				ref random,
				count: (int)(random.NextFloat(1, 3) * random.NextFloat(1, 2)),
				massMulMin: .6f,
				massMulMax: .8f,
				jupiterJump: 1,
				massFraction: .1f);

			var averageChildMass = root.Children.Sum(p => p.Mass) / root.Children.Count;
			foreach(var p in root.Children.Where(c=>c.Mass>settings.GasGiantMass))
			{
				var m = p.Mass / averageChildMass;
				// Give each child in the rosette its own mini solar system
				p.ExpandSolar(
					ref random,
					count: (int) (random.NextFloat(1, 3 * m) + random.NextFloat(1, 3 * m)),
					massMulMin: 0.75f,
					massMulMax: 2.5f,
					jupiterJump: random.NextFloat() * random.NextFloat() * 10 + 1,
					massFraction: .5f
				);
			}
		}
		else
		{
			// Create a regular old boring solar system
			root.ExpandSolar(
				ref random,
				count: random.NextInt(5, 15),
				massMulMin: 0.75f,
				massMulMax: 2.5f,
				jupiterJump: random.NextFloat() * random.NextFloat() * 10 + 1,
				massFraction: .25f
			);
		}

		var alreadyExpanded = new List<GeneratorPlanet>();
		var binaries = new List<GeneratorPlanet>();
		for(int i=0; i<settings.SatellitePasses; i++)
		{
			// Get all children that are above the satellite creation mass floor and not rosette members
			var satelliteCandidates = rosette
				? root.AllPlanets().Where(p =>
					p != root &&
					p.Parent != root &&
					p.Mass > settings.SatelliteCreationMassFloor &&
					!alreadyExpanded.Contains(p))
				: root.AllPlanets().Where(p =>
					p != root &&
					p.Mass > settings.SatelliteCreationMassFloor &&
					!alreadyExpanded.Contains(p));

			foreach (var planet in satelliteCandidates)
			{
				// There's a chance of generating satellites for each qualified planet
				if (random.NextFloat() < settings.SatelliteCreationProbability)
				{
					// Sometimes the satellite is so massive that it forms a binary system (like Earth!)
					if (random.NextFloat() < settings.BinaryCreationProbability)
					{
						planet.ExpandRosette(ref random, 2);
						binaries.AddRange(planet.Children);
					}
					// Otherwise, terrestrial planets get a couple satellites while gas giants get many
					else
					{
						planet.ExpandSolar(
							ref random,
							count: planet.Mass < settings.GasGiantMass ? random.NextInt(1, 3) : random.NextInt(2, 6),
							massMulMin: .75f,
							massMulMax: 1.5f,
							jupiterJump: 1,
							massFraction: .15f); // Planetary satellites are not nearly as massive as planets themselves
					}
					alreadyExpanded.Add(planet);
				}
			}
		}

		// Get all children that are below the belt creation mass floor and not rosette members, also exclude binaries
		var beltCandidates = rosette
			? root.AllPlanets().Where(p => p != root && p.Parent != root && p.Mass < settings.BeltMassCeiling && !binaries.Contains(p) && p.Children.Count == 0)
			: root.AllPlanets().Where(p => p != root && p.Mass < settings.BeltMassCeiling && !binaries.Contains(p) && p.Children.Count == 0);

		foreach(var planet in beltCandidates.Reverse())
			if (random.NextFloat() < settings.BeltProbability && !planet.Parent.Children.Any(p=>p.Belt))
				planet.Belt = true;

		var totalMass = root.AllPlanets().Sum(p => p.Mass);
		var rootMass = root.Mass;
		foreach (var planet in root.AllPlanets())
			planet.Mass = (planet.Mass / totalMass) * rootMass;

				Place(root, planetSettings, radius, rootReach);

		return root.AllPlanets().ToArray();
	}

	// Placement (tail-taming-law-r3-band-loose-cusp, c4-proceed-and-fly): a child sits at least RocheMargin of its parent's Roche
	// limits out; a massive child of a body also far enough that its own pull peak beats CuspMargin times its parent's pull there;
	// siblings sit SiblingSpacing of a reach apart. Every other number is a GravityField constant.
	public const float RocheMargin = 1.5f;

	private const uint TrojanStream = 0x9E3779B9u;

	// A swarm's rocks: alternately round L4 and L5 of the rail at `distance` and `phase`, each within half an arc of its point
	// along the orbit and a half-width of the rail across it, thickest at the point.
	public static Asteroid[] TrojanRocks(ZoneGenerationSettings settings, ref Random random, float distance, float phase)
	{
		var rocks = new Asteroid[2 * (int) settings.TrojanCount.Evaluate(distance)];
		for (var i = 0; i < rocks.Length; i++)
			rocks[i] = new Asteroid
			{
				Distance = distance * (1 + random.NextFloat() * (random.NextFloat() - .5f) * 2 * settings.TrojanHalfWidth),
				Phase = frac(phase + (i % 2 == 0 ? 1f : -1f) / 6f + random.NextFloat() * (random.NextFloat() - .5f) * settings.TrojanArc),
				Size = random.NextFloat(),
				RotationSpeed = settings.AsteroidRotationSpeed.Evaluate(random.NextFloat())
			};
		return rocks;
	}
	public const float CuspMargin = 1.5f;
	public const float SiblingSpacing = .5f;

	/// <summary>
	/// The one placement pass: gives every generated node its distance and every belt its half width, top-down. A body's children
	/// are laid inside-out at the nearest the rules allow, the lightest family is folded into the parent (its mass kept) while the
	/// outermost overflows the window, and the rest are stretched geometrically so the outermost sits at the window's edge. A
	/// non-root's window ends before its own fade (Gamma x Reach x its distance); a root's, in a zone of several, ends under
	/// rootReach / Beta so its support never meets another root's (root-window-floor), and is 0.75 x the area radius at most.
	/// A rosette's ring is one orbit, and a member may itself be bodyless, which counts as one body of its family's mass; once its members have settled (their own children folded in), it folds into its node,
	/// which becomes a body, if it cannot keep its heaviest member outside its Roche margin. Ported from docs/research/gravity-well-law/replace.py.
	/// </summary>
	public static void Place(GeneratorPlanet root, PlanetSettings planetSettings, float radius, float rootReach)
	{
		var q = 1 / (1 - SiblingSpacing * GravityField.Reach);
		float Roche(float mass) => WellTree.Curves(planetSettings, mass).Softening;
		float Strength(float mass) => WellTree.Curves(planetSettings, mass).Strength;
		float WellMass(GeneratorPlanet p) => p.Empty || p.Belt ? 0f : p.Mass;
		float Family(GeneratorPlanet p) => (p.Empty ? 0f : p.Mass) + p.Children.Sum(c => Family(c));
		// A bodyless node pulls from afar as one body of its family's mass, which it becomes if its ring cannot stand (binaries-orbited-from-afar)
		float OneBody(GeneratorPlanet p) => p.Empty ? Family(p) : WellMass(p);
		// A folded family joins its target: a body takes its mass, a bodyless node (whose own Mass is no mass) takes it as a child,
		// which its own Settle then places.
		void Fold(GeneratorPlanet node, GeneratorPlanet into)
		{
			node.Parent.Children.Remove(node);
			if (into.Empty) { node.Parent = into; into.Children.Add(node); }
			else into.Mass += Family(node);
		}

		void PlaceChildren(List<GeneratorPlanet> children, Func<float> lower, float upper, GeneratorPlanet into)
		{
			upper *= 1 - 1e-6f;
			float AtLeast(GeneratorPlanet child, float edge)
			{
				var m = OneBody(child);
				var mInto = OneBody(into);
				return m <= 0 || mInto <= 0 ? edge : Math.Max(edge, 2 * CuspMargin * Roche(m) * Strength(mInto) / Strength(m));
			}

			var lo = 0f;
			var positions = new List<float>();
			while (children.Count > 0)
			{
				lo = lower();
				positions.Clear();
				for (var k = 0; k < children.Count; k++)
					positions.Add(AtLeast(children[k], k == 0 ? lo : Math.Max(lo, positions[k - 1] * q)));
				if (positions[positions.Count - 1] <= upper) break;
				var lightest = children.OrderBy(c => Family(c)).First();
				Fold(lightest, into);
				children.Remove(lightest);
			}
			if (children.Count == 0) return;

			var g = children.Count > 1 ? pow(upper / positions[positions.Count - 1], 1f / (children.Count - 1)) : 1f;
			for (var k = 0; k < children.Count; k++)
				children[k].Distance = positions[k] * pow(g, k);
			// A belt is an interval inside its slot: inner edge outside the lower bound and the previous sibling's spacing, outer edge
			// inside the next sibling's spacing and the window; the authored width is the widest it may be.
			var outer = 0f;
			for (var k = 0; k < children.Count; k++)
			{
				var c = children[k];
				if (!c.Belt) { outer = c.Distance; continue; }
				var inner = Math.Max(lo, outer * q);
				var outerLimit = k + 1 < children.Count ? Math.Min(upper, children[k + 1].Distance / q) : upper;
				c.Distance = (inner + outerLimit) / 2;
				var half = Math.Max(0f, Math.Min(c.Settings.AsteroidBeltWidth.Evaluate(c.Distance), outerLimit - inner) / 2);
				c.BeltHalfWidth = half;
				outer = c.Distance + half;
			}
		}

		var chordCap = new Dictionary<GeneratorPlanet, float>(); // a ring member's children stay within .2 of the nearest partner's chord
		void Settle(GeneratorPlanet node)
		{
			var isRoot = node.Parent == null;
			var reach = isRoot ? rootReach : GravityField.Reach * node.Distance;
			var share = isRoot ? 1 / GravityField.Beta : GravityField.Gamma;
			var cap = isRoot ? .75f * radius : float.PositiveInfinity;
			var upper = Math.Min(cap, share * reach / Math.Max(GravityField.Beta, 1 + GravityField.Reach));
			if (chordCap.TryGetValue(node, out var chord)) upper = Math.Min(upper, chord);
			if (WellMass(node) > 0)
			{
				PlaceChildren(node.Children.ToList(), () => RocheMargin * Roche(WellMass(node)), upper, node);
				foreach (var child in node.Children.ToList()) Settle(child);
				return;
			}

			var ring = node.Children.Where(c => c.Ring && OneBody(c) > 0).ToList();
			if (ring.Count == 0) { foreach (var child in node.Children.ToList()) Settle(child); return; }
			var heavy = ring.OrderByDescending(OneBody).First();
			var ringDistance = Math.Min(.5f * cap, share * reach / (GravityField.Beta * GravityField.Beta * (1 + GravityField.RingReach)));
			PlaceChildren(node.Children.Where(c => !ring.Contains(c)).ToList(),
				() => (1 + GravityField.RingReach) * ringDistance * GravityField.Beta, upper, heavy);
			foreach (var member in ring)
			{
				member.Distance = ringDistance;
				chordCap[member] = .2f * 2 * ringDistance * sin(PI / ring.Count);
				Settle(member);
			}
			// A member's mass is final only once it has settled (its own overflowing children fold into it).
			if (ringDistance < RocheMargin * Roche(ring.Max(OneBody)))
			{
				// The ring cannot keep its heaviest member outside its Roche margin inside its node's reach: the node is one body.
				node.Empty = false;
				node.Mass = 0;
				foreach (var child in node.Children.ToList()) Fold(child, node);
				return;
			}
			foreach (var child in node.Children.Where(c => !ring.Contains(c)).ToList()) Settle(child);
		}

		Settle(root);
	}
}

// All orbiting bodies are referred to here as "Planets"
// This goes back to the original etymology for the word planet, which is traveler,
// Because they were objects we observed to move in the sky instead of remaining fixed like the stars
public class GeneratorPlanet
{
	public ZoneGenerationSettings Settings;
	public float Distance;
	public float Phase;
	public float Mass;
	// A belt's half width, written by ZoneGenerator.Place: the belt fills the slot between its siblings' spacing, the parent's
	// Roche margin and the parent's window, up to the authored width (ruling belt-fills-its-slot).
	public float BeltHalfWidth;
	public bool Empty = false;
	public bool Belt = false;
	// A member of a rosette's ring, which Place puts on one orbit round its bodyless parent.
	public bool Ring = false;
	public List<GeneratorPlanet> Children = new List<GeneratorPlanet>(); // Planets orbiting this one are referred to as children
	public GeneratorPlanet Parent;
	public float2 FixedPosition;

	// Recursively gather all planets in the hierarchy
	public IEnumerable<GeneratorPlanet> AllPlanets()
	{
		return new[]{this}.Concat(Children.SelectMany(c=>c.AllPlanets()));
	}

	// Create children that fill a single orbit, equally spaced; Place sets the orbit's radius
	// https://en.wikipedia.org/wiki/Klemperer_rosette
	public void ExpandRosette(ref Random random, int vertices)
	{
		// Rosette children replace the parent, parent orbital node is left empty
		Empty = true;

		// Masses in a rosette alternate, so every sequential pair has the same shared mass
		var sharedMass = Mass / vertices * 2;

		// Proportion of the masses of sequential pairs is random, but only for even vertex counts
		var proportion = vertices % 2 == 0 ? random.NextFloat(.5f,.95f) : .5f;

		for (int i = 0; i < vertices; i++)
		{
			Children.Add(new GeneratorPlanet
			{
				Settings = Settings,
				Parent = this,
				Mass = sharedMass * (i % 2 == 0 ? proportion : 1 - proportion), // Masses alternate
				Phase = (float) i / vertices,
				Ring = true
			});
		}
	}

	// Create children that mimic the distribution of planetary masses in the solar system, inside-out in list order;
	// Place gives them their distances
	public void ExpandSolar(ref Random random, int count, float massMulMin, float massMulMax, float jupiterJump, float massFraction)
	{
		if (count == 0)
			return;

		var masses = new float[count];

		// Accumulate total mass
		// Initialize first mass, actual number doesn't matter since total mass will be divided proportionally
		float massTotal = masses[0] = 1;

		// Masses multiply from one planet to the next
		// Mass typically increases exponentially as you go further out
		// jupiterJump is an additional mass multiplier applied after half of the planets
		for (var i = 1; i < count; i++)
			massTotal += masses[i] = masses[i - 1] * random.NextFloat(massMulMin, massMulMax) * (count/2==i ? jupiterJump : 1);

		// Add some randomness to child masses
		for (var i = 0; i < masses.Length; i++)
			masses[i] *= random.NextFloat(.1f, 1f);

		// Normalize the masses of the children
		for (var i = 0; i < count; i++)
			masses[i] = masses[i] / massTotal * Mass * massFraction;

		for (var i = 0; i < count; i++)
		{
			// Only instantiate children above the mass floor
			if (masses[i] > Settings.MassFloor)
				Children.Add(new GeneratorPlanet
				{
					Settings = Settings,
					Parent = this,
					Mass = masses[i],
					Phase = random.NextFloat()
				});
		}
	}
}
