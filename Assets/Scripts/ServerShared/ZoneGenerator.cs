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

	public static ZonePack GenerateZone(
		ItemManager itemManager,
		ZoneGenerationSettings zoneSettings,
		Galaxy galaxy,
		GalaxyZone galaxyZone,
		bool isTutorial = false)
	{
		var pack = new ZonePack();
		var cache = itemManager.ItemData;

		var random = new Random(galaxyZone.Name.StableHash() ^ (uint) pcg3d(galaxyZone.Position).x);

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
			foreach (var c in occupiedAreas)
			{
				planets.AddRange(GenerateEntities(zoneSettings, ref random, c.Area / totalArea * pack.Mass, c.Radius, c.Center));
			}
		}
		else
			planets.AddRange(GenerateEntities(zoneSettings, ref random, pack.Mass, pack.Radius, float2.zero));

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
				            Distance = orbitMap[planet].Distance + random.NextFloat()*(random.NextFloat()-.5f)*zoneSettings.AsteroidBeltWidth.Evaluate(orbitMap[planet].Distance),
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
		            float primary = random.NextFloat();
		            float secondary = frac(primary + 1 + zoneSettings.SunSecondaryColorDistance * (random.NextFloat() > .5 ? 1 : -1));
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
            planetData.Name = cache.Upsert(planetData).Key.Value.Substring(0, 8);
            return cache.Upsert(planetData);
        }).ToList();

        var nearestFaction = galaxy.Factions.MinBy(f => galaxy.HomeZones[f].Distance[galaxyZone]);
        var nearestFactionHomeZone = galaxy.HomeZones[nearestFaction];
        var factionPresence = nearestFaction.InfluenceDistance - nearestFactionHomeZone.Distance[galaxyZone] + 1;

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
        var potentialLagrangePoints = planets
	        .Where(p => p.Parent != null && p.Parent.Children
		        .TrueForAll(c => !(c != p && abs(c.Distance - p.Distance) < .1f))) // Filter Rosettes
	        .OrderBy(p => p.Distance)
	        .ToArray();
        // Pick a selection from the middle of the distribution
        var selectedStationOrbits = potentialLagrangePoints
	        .Skip(potentialLagrangePoints.Length / 2)
	        .Take(stationCount)
	        .Select(p=>orbitMap[p])
	        .ToArray();
        // No planet has a sibling to share a Lagrange point with (a boring single-body system, or none at all),
        // so there is nowhere the ordinary path would put a station. The entrance zone still needs one: give it
        // a standalone orbit fixed at a safe distance from the zone centre rather than silently leaving it with
        // no station.
        if (isTutorialEntrance && selectedStationOrbits.Length == 0)
        {
	        var fallbackOrbit = new OrbitData { FixedPosition = random.NextFloat2Direction() * pack.Radius * .5f };
	        cache.Upsert(fallbackOrbit);
	        selectedStationOrbits = new[] { fallbackOrbit };
        }

        var loadoutGenerators = new Dictionary<Faction, LoadoutGenerator>();

        LoadoutGenerator GetLoadoutGenerator(Faction faction)
        {
	        if (!loadoutGenerators.ContainsKey(faction))
		        loadoutGenerators[faction] = new LoadoutGenerator(ref random, itemManager, galaxy, galaxyZone, faction, .5f);
	        return loadoutGenerators[faction];
        }

        OrbitData CreateLagrangeOrbit(OrbitData baseOrbit)
        {
	        var lagrangeOrbit = new OrbitData
	        {
		        Parent = baseOrbit.Parent,
		        Distance = baseOrbit.Distance,
		        Phase = baseOrbit.Phase + PI / 3 * sign(random.NextFloat() - .5f),
		        // Carried through for the tutorial-entrance fallback orbit below, which has no Parent and is
		        // positioned by FixedPosition alone; every other base orbit here has a Parent and ignores it.
		        FixedPosition = baseOrbit.FixedPosition,
	        };
	        pack.Orbits.Add(cache.Upsert(lagrangeOrbit));
	        return lagrangeOrbit;
        }

        void PlaceTurret(OrbitData orbit, LoadoutGenerator loadoutGenerator, int distanceMultiplier)
        {
	        var phase = 20f * distanceMultiplier / orbit.Distance;

	        var turretOrbit = new OrbitData
	        {
		        Parent = orbit.Parent,
		        Distance = orbit.Distance,
		        Phase = orbit.Phase + phase
	        };
	        pack.Orbits.Add(cache.Upsert(turretOrbit));
	        var turret = loadoutGenerator.GenerateTurretLoadout();
	        if (turret == null) return;
	        turret.Orbit = cache.RefOf(turretOrbit);
	        pack.Entities.Add(turret);
        }

        void PlaceTurrets(OrbitData orbit, LoadoutGenerator loadoutGenerator, int count)
        {
	        for(int t=0; t<count; t++)
	        {
		        var dist = t / 2;
		        if (t % 2 == 0) dist = -dist;
		        PlaceTurret(orbit, loadoutGenerator, dist);
	        }
        }

        for (var i = 0; i < storyStations.Length; i++)
        {
	        var story = storyStations[i];
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

	        PlaceTurrets(lagrangeOrbit, GetLoadoutGenerator(story.Faction), story.Turrets);
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

	        PlaceTurrets(lagrangeOrbit, GetLoadoutGenerator(nearestFaction), 2);
        }

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

	public static GeneratorPlanet[] GenerateEntities(ZoneGenerationSettings settings, ref Random random, float mass, float radius, float2 fixedPosition)
	{
		var root = new GeneratorPlanet
		{
			FixedPosition = fixedPosition,
			Settings = settings,
			Mass = mass,
			ChildDistanceMaximum = radius * .75f,
			ChildDistanceMinimum = settings.PlanetSafetyRadius.Evaluate(mass)
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
				distMulMin: 1.25f,
				distMulMax: 1.75f,
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
					distMulMin: 1 + m * .25f,
					distMulMax: 1.05f + m * .5f,
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
				distMulMin: 1.1f,
				distMulMax: 1.25f,
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
							distMulMin: 1.05f,
							distMulMax: 1.25f,
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

		return root.AllPlanets().ToArray();
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
	//public float Period;
	public float ChildDistanceMinimum;
	public float ChildDistanceMaximum;
	public bool Empty = false;
	public bool Belt = false;
	public List<GeneratorPlanet> Children = new List<GeneratorPlanet>(); // Planets orbiting this one are referred to as children
	public GeneratorPlanet Parent;
	public float2 FixedPosition;

	// Recursively gather all planets in the hierarchy
	public IEnumerable<GeneratorPlanet> AllPlanets()
	{
		return new[]{this}.Concat(Children.SelectMany(c=>c.AllPlanets()));
	}

	// Create children that fill a single orbit, equally spaced
	// https://en.wikipedia.org/wiki/Klemperer_rosette
	public void ExpandRosette(ref Random random, int vertices)
	{
		//Debug.Log("Expanding Rosette");

		// Rosette children replace the parent, parent orbital node is left empty
		Empty = true;

		// Masses in a rosette alternate, so every sequential pair has the same shared mass
		var sharedMass = Mass / vertices * 2;

		// Proportion of the masses of sequential pairs is random, but only for even vertex counts
		var proportion = vertices % 2 == 0 ? random.NextFloat(.5f,.95f) : .5f;

		// Place children at a fixed distance in the center of the range
		var dist = (ChildDistanceMinimum + ChildDistanceMaximum) / 2;

		// Position of first child
		var p0 = new float2(0, dist);

		// Position of second child
		var p1 = OrbitData.Evaluate(1.0f / vertices) * dist;

		// Maximum child distance is half the distance to the neighbor minus the neighbor's radius
		var p0ChildDist = (distance(p0, p1) * proportion - Settings.PlanetSafetyRadius.Evaluate(sharedMass * (1 - proportion))) * .75f;
		var p1ChildDist = (distance(p0, p1) * (1 - proportion) - Settings.PlanetSafetyRadius.Evaluate(sharedMass * proportion)) * .75f;

		for (int i = 0; i < vertices; i++)
		{
			var child = new GeneratorPlanet
			{
				Settings = Settings,
				Parent = this,
				Mass = sharedMass * (i % 2 == 0 ? proportion : 1 - proportion), // Masses alternate
				Distance = dist,
				Phase = (float) i / vertices,
				ChildDistanceMaximum = (i % 2 == 0 ? p0ChildDist : p1ChildDist)
			};
			child.ChildDistanceMinimum = Settings.PlanetSafetyRadius.Evaluate(child.Mass) * 2;
			//child.Period = Settings..Evaluate(child.Distance);
			Children.Add(child);
		}

		ChildDistanceMinimum = dist + p0ChildDist;
	}

	// Create children that mimic the distribution of planetary masses in the solar system
	public void ExpandSolar(ref Random random, int count, float massMulMin, float massMulMax, float distMulMin, float distMulMax, float jupiterJump, float massFraction)
	{
		//Debug.Log("Expanding Solar");
		// Expansion is impossible when child space is full
		if (count == 0 || ChildDistanceMaximum < ChildDistanceMinimum)
			return;

		var masses = new float[count];
		var distances = new float[count];

		// Accumulate total mass
		// Initialize first mass and distance, actual number doesn't matter since total mass will be divided proportionally
		float massTotal = distances[0] = masses[0] = 1;

		// Masses and distances multiply from one planet to the next
		// Mass typically increases exponentially as you go further out
		// jupiterJump is an additional mass multiplier applied after half of the planets
		for (var i = 1; i < count; i++)
		{
			massTotal += masses[i] = masses[i - 1] * random.NextFloat(massMulMin, massMulMax) * (count/2==i ? jupiterJump : 1);
			distances[i] = distances[i - 1] * random.NextFloat(distMulMin, distMulMax);
		}

		// Add some randomness to child masses
		for (var i = 0; i < masses.Length; i++)
			masses[i] *= random.NextFloat(.1f, 1f);

		// Normalize the masses of the children
		for (var i = 0; i < count; i++)
			masses[i] = masses[i] / massTotal * Mass * massFraction;

		// Map child distances to range between minimum and maximum
		if (count > 1)
		{
			var oldDistances = (float[]) distances.Clone();
			for (var i = 0; i < count; i++)
				distances[i] = lerp(ChildDistanceMinimum, ChildDistanceMaximum,
					(oldDistances[i] - oldDistances[0]) / (oldDistances[count - 1] - oldDistances[0])) + Settings.PlanetSafetyRadius.Evaluate(masses[i]);
		}

		for (var i = 0; i < count; i++)
		{
			// Only instantiate children above the mass floor
			if (masses[i] > Settings.MassFloor)
			{
				var child = new GeneratorPlanet
				{
					Settings = Settings,
					Parent = this,
					Mass = masses[i],
					Distance = distances[i],
					Phase = random.NextFloat()
				};
				//child.Period = Context.GlobalData.OrbitalPeriod(child.Distance);
				child.ChildDistanceMinimum = Settings.PlanetSafetyRadius.Evaluate(child.Mass) * 2;
				// Maximum child distance of child is the smallest distance to either of its neighbors
				child.ChildDistanceMaximum = min(i == 0 ? child.Distance - ChildDistanceMinimum : child.Distance - distances[i - 1],
										 i < count - 1 ? distances[i + 1] - child.Distance : float.PositiveInfinity);
				if (float.IsNaN(child.Distance))
					throw new NotFiniteNumberException($"Planet created with NaN distance, something went very wrong!");
				if (child.ChildDistanceMaximum > child.ChildDistanceMinimum)
					Children.Add(child);
			}
		}
	}
}
