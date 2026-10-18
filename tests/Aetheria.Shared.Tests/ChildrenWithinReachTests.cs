using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using Xunit.Abstractions;
using static CultMath.math;
using float2 = CultMath.float2;

// Aetheria gravity, children within reach: the production generator's zones (every zone of seeds 1-30, ambient off) read back
// through the Zone's own tree, orbits and bodies. Each expectation is spelled from the rule it names and the law's constants
// (GravityField), never from ZoneGenerator.Place's helpers.
public sealed class ChildrenWithinReachTests : IClassFixture<ChildrenWithinReachTests.Zones>
{
    public sealed class Zones : IDisposable
    {
        private readonly ZoneWorlds _worlds = new ZoneWorlds();
        public readonly List<(uint Seed, Zone Zone)> All = new List<(uint, Zone)>();
        public ZoneGenerationSettings Settings;

        // Seeds 7, 11 and 12 held rings inside their heavy member's Roche margin (ring-roche-final-heavy), and seeds 10, 13, 21 and 30
        // held a bodyless ring member whose own bodies sat at their node's centre (bodyless-node-places-children); seeds 1-6 show neither.
        public Zones() : this(1, 30, null) { }

        // Not public: xunit builds the fixture through its one public constructor; a test that wants a tuned sample builds its own.
        internal Zones(uint first, uint last, Action<ZoneGenerationSettings> tune)
        {
            for (uint seed = first; seed <= last; seed++)
            {
                var dir = Path.Combine(_worlds.Root, $"galaxy{seed}");
                Directory.CreateDirectory(dir);
                var catalog = Path.Combine(dir, "Aetheria.cc");
                File.Copy(TestCatalog.Repo, catalog);
                var cache = new CultCache(TestCatalog.Registry());
                _worlds.Caches.Add(cache);
                cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
                cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(Path.Combine(dir, "run.cc")), AetheriaStores.RunTypes);
                var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
                var galaxy = new Galaxy(authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings"),
                    authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
                    cache, new PlayerSettings(),
                    Directory.CreateDirectory(Path.Combine(dir, "Narrative")), _ => { }, null, seed);
                var items = new ItemManager(cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
                var planetSettings = authored.Read<PlanetSettings>("PlanetSettings");
                foreach (var galaxyZone in galaxy.Zones)
                {
                    Settings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
                    tune?.Invoke(Settings);
                    var pack = ZoneGenerator.GenerateZone(items, Settings, planetSettings, galaxy, galaxyZone,
                        galaxyZone == galaxy.Entrance && galaxy.IsPrelude, false);
                    All.Add((seed, new Zone(items, planetSettings, pack, galaxyZone, galaxy)));
                }
            }
        }

        public void Dispose() => _worlds.Dispose();
    }

    private readonly Zones _zones;
    private readonly ITestOutputHelper _out;

    public ChildrenWithinReachTests(Zones zones, ITestOutputHelper output)
    {
        _zones = zones;
        _out = output;
    }

        private const float Tolerance = 1e-3f;
    // A child's own family folded into it after it was placed raises its mass, and the cusp bound grows as mass^.05.
    private const float CuspTolerance = 1e-2f;
    private const float RocheMargin = 1.5f, CuspMargin = 1.5f; // the rulings tail-taming-law-r3-band-loose-cusp and c4-proceed-and-fly

    private static float Roche(Zone zone, float mass) => zone.Settings.RocheLimit.Evaluate(mass);
    private static float Strength(Zone zone, float mass) => zone.Settings.WellStrength.Evaluate(mass);

    // Orbits that carry a well-bearing body, with its mass; belts and bodyless nodes carry none.
    private static Dictionary<CultRecordKey, float> BodyMass(Zone zone) =>
        zone.Planets.Values.Where(b => !(b is AsteroidBeltData)).GroupBy(b => b.Orbit.Key).ToDictionary(g => g.Key, g => g.First().Mass);

    // The generated orbits: every body's and its ancestors', which leaves out stations, turrets and Lagrange copies.
    private static HashSet<CultRecordKey> Placed(Zone zone)
    {
        var placed = new HashSet<CultRecordKey>();
        foreach (var body in zone.Planets.Values)
            for (var key = body.Orbit.Key; placed.Add(key) && zone.Orbits[key].Data.Parent.IsSet();) key = zone.Orbits[key].Data.Parent.Key;
        return placed;
    }

    private static List<CultRecordKey> ChildrenOf(Zone zone, CultRecordKey parent, HashSet<CultRecordKey> placed) =>
        placed.Where(k => zone.Orbits[k].Data.Parent.IsSet() && zone.Orbits[k].Data.Parent.Key.Equals(parent))
            .OrderBy(k => zone.Orbits[k].Data.Distance).ToList();

    private static float DistanceOf(Zone zone, CultRecordKey orbit) => zone.Orbits[orbit].Data.Distance;

    private static List<CultRecordKey> MassiveRoots(Zone zone) =>
        zone.Orbits.Keys.Where(k => !zone.Orbits[k].Data.Parent.IsSet() && zone.Wells.Reach(k) > 0f).ToList();

    private static float2 Anchor(Zone zone, CultRecordKey root) => zone.Orbits[root].Data.FixedPosition;

    private static IEnumerable<CultRecordKey> Descendants(Zone zone, CultRecordKey root, HashSet<CultRecordKey> placed)
    {
        foreach (var child in ChildrenOf(zone, root, placed))
        {
            yield return child;
            foreach (var below in Descendants(zone, child, placed)) yield return below;
        }
    }

    [Fact]
    public void SubzonesAreIsolated()
    {
        var zonesChecked = 0;
        var pairs = 0;
        var violatingPairs = 0;
        var orbits = 0;
        var violatingOrbits = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var roots = MassiveRoots(zone);
            if (roots.Count < 2) continue;
            zonesChecked++;
            var placed = Placed(zone);
            foreach (var i in roots)
            {
                var nearest = roots.Where(j => !j.Equals(i)).Min(j => length(Anchor(zone, i) - Anchor(zone, j)));
                Assert.True(Math.Abs(zone.Wells.Reach(i) - GravityField.Reach * nearest) <= Tolerance * nearest,
                    $"seed {seed}: a root's reach {zone.Wells.Reach(i)} is not Reach x the nearest root's distance {nearest} (its floor binds)");
                foreach (var j in roots.Where(j => !j.Equals(i)))
                {
                    pairs++;
                    if (zone.Wells.Reach(i) + zone.Wells.Support(j) > length(Anchor(zone, i) - Anchor(zone, j)) * (1 + Tolerance)) violatingPairs++;
                    foreach (var orbit in Descendants(zone, i, placed))
                    {
                        orbits++;
                        if (length(zone.OrbitPositionAt(orbit, 0) - Anchor(zone, j)) < zone.Wells.Reach(j) * (1 - Tolerance)) violatingOrbits++;
                    }
                }
            }
        }
        _out.WriteLine($"subzones isolated: {zonesChecked} multi-root zones, {pairs} ordered root pairs ({violatingPairs} with reach_i + support_j > d), " +
                       $"{orbits} orbit-vs-root checks ({violatingOrbits} inside another root's reach)");
        Assert.True(zonesChecked > 0 && pairs > 0 && orbits > 0, "the seeds generated no zone with two massive roots");
        Assert.True(violatingPairs == 0, $"{violatingPairs} of {pairs} ordered root pairs overlap");
        Assert.True(violatingOrbits == 0, $"{violatingOrbits} of {orbits} orbits sit inside another root's reach");
    }

    [Fact]
    public void NoChildSitsInsideItsParentsRocheMargin()
    {
        var children = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            var placed = Placed(zone);
            foreach (var parent in placed.Where(mass.ContainsKey))
                foreach (var child in ChildrenOf(zone, parent, placed))
                {
                    children++;
                    var margin = RocheMargin * Roche(zone, mass[parent]);
                    Assert.True(DistanceOf(zone, child) >= margin * (1 - Tolerance), $"seed {seed}: a child sits at {DistanceOf(zone, child)}, inside {margin}");
                }
        }
        Assert.True(children > 100, $"only {children} children checked");
    }

    [Fact]
    public void EveryMassiveChildKeepsACusp()
    {
        var checkedChildren = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            var placed = Placed(zone);
            foreach (var parent in placed.Where(mass.ContainsKey))
                foreach (var child in ChildrenOf(zone, parent, placed).Where(mass.ContainsKey))
                {
                    checkedChildren++;
                    var cusp = 2 * CuspMargin * Roche(zone, mass[child]) * Strength(zone, mass[parent]) / Strength(zone, mass[child]);
                    Assert.True(DistanceOf(zone, child) >= cusp * (1 - CuspTolerance), $"seed {seed}: a massive child sits at {DistanceOf(zone, child)}, inside its cusp bound {cusp}");
                }
        }
        Assert.True(checkedChildren > 50, $"only {checkedChildren} massive children checked");
    }

    [Fact]
    public void AFamilyEndsBeforeItsParentsFade()
    {
        var families = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var placed = Placed(zone);
            var mass = BodyMass(zone);
            foreach (var node in placed.Where(k => zone.Orbits[k].Data.Parent.IsSet() && zone.Wells.Reach(k) > 0f))
            {
                var kids = ChildrenOf(zone, node, placed);
                if (!kids.Any(k => zone.Wells.Reach(k) > 0f)) continue;
                families++;
                Assert.True(zone.Wells.Support(node) <= GravityField.Gamma * zone.Wells.Reach(node) * (1 + Tolerance),
                    $"seed {seed}: a family's support {zone.Wells.Support(node)} runs past Gamma x its reach {zone.Wells.Reach(node)}");
                var parent = zone.Orbits[node].Data.Parent.Key;
                if (mass.ContainsKey(parent)) // a ring member's reach is RingReach x D; a body's child's is Reach x D
                    Assert.True(Math.Abs(zone.Wells.Reach(node) - GravityField.Reach * DistanceOf(zone, node)) <= Tolerance * DistanceOf(zone, node),
                        $"seed {seed}: a family's reach {zone.Wells.Reach(node)} is not Reach x its distance {DistanceOf(zone, node)}");
            }
        }
        Assert.True(families > 20, $"only {families} families checked");
    }

    [Fact]
    public void SiblingsSitHalfAReachApart()
    {
        var q = 1 / (1 - GravityField.Reach / 2);
        var pairs = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            var placed = Placed(zone);
            foreach (var parent in placed.Where(mass.ContainsKey))
            {
                var kids = ChildrenOf(zone, parent, placed);
                for (var k = 1; k < kids.Count; k++)
                {
                    pairs++;
                    Assert.True(DistanceOf(zone, kids[k]) >= DistanceOf(zone, kids[k - 1]) * q * (1 - Tolerance),
                        $"seed {seed}: siblings at {DistanceOf(zone, kids[k - 1])} and {DistanceOf(zone, kids[k])} are closer than a factor {q}");
                }
            }
        }
        Assert.True(pairs > 100, $"only {pairs} sibling pairs checked");
    }

    // The same random stream makes the same tree; only the window differs, so a small one (tight) folds some of what a huge one
    // (wide) keeps whole. No rosette or binary: a bodyless node's own mass is renormalised with the rest.
    private static IEnumerable<(uint Seed, GeneratorPlanet[] Tight, GeneratorPlanet[] Wide)> TightAndWideTrees(float mass)
    {
        var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
        var zoneSettings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
        var planetSettings = authored.Read<PlanetSettings>("PlanetSettings");
        zoneSettings.RosetteProbability = 0;
        zoneSettings.BinaryCreationProbability = 0;
        for (uint seed = 1; seed <= 200; seed++)
        {
            var tight = new CultMath.Random(seed);
            var tightTree = ZoneGenerator.GenerateEntities(zoneSettings, planetSettings, ref tight, mass, 1500f, float2.zero, float.PositiveInfinity);
            var wide = new CultMath.Random(seed);
            var wideTree = ZoneGenerator.GenerateEntities(zoneSettings, planetSettings, ref wide, mass, 1e7f, float2.zero, float.PositiveInfinity);
            yield return (seed, tightTree, wideTree);
        }
    }

    // Folding moves a family's mass into its parent body, so a tree's masses still sum to its root's.
    [Fact]
    public void FoldingConservesMass()
    {
        const float mass = 300000f;
        var folded = 0;
        foreach (var (seed, tight, wide) in TightAndWideTrees(mass))
        {
            if (tight.Length < wide.Length) folded++;
            Assert.True(Math.Abs(tight.Sum(p => p.Mass) - mass) <= 1e-3f * mass, $"seed {seed}: a folded tree's bodies sum to {tight.Sum(p => p.Mass)}, not {mass}");
        }
        _out.WriteLine($"folding: {folded} of 200 trees with a fold");
        Assert.True(folded > 10, $"only {folded} trees folded");
    }

    // Under one parent the families folded away are never heavier than the ones kept (rule 4: the lightest goes first).
    [Fact]
    public void FoldingTakesTheLightestFamily()
    {
        float Family(GeneratorPlanet p) => p.Mass + p.Children.Sum(Family);
        var parentsThatFolded = 0;
        foreach (var (seed, tight, wide) in TightAndWideTrees(300000f))
        {
            var tightByPhase = tight.ToDictionary(p => p.Phase);
            void Compare(GeneratorPlanet w)
            {
                if (w.Children.Count == 0 || !tightByPhase.TryGetValue(w.Phase, out var t)) return;
                var keptPhases = t.Children.Select(c => c.Phase).ToHashSet();
                var kept = w.Children.Where(c => keptPhases.Contains(c.Phase)).ToList();
                var gone = w.Children.Where(c => !keptPhases.Contains(c.Phase)).ToList();
                if (gone.Count > 0 && kept.Count > 0)
                {
                    parentsThatFolded++;
                    Assert.True(gone.Max(Family) <= kept.Min(Family) * (1 + 1e-4f), $"seed {seed}: a folded family outweighs one that was kept");
                }
                foreach (var child in kept) Compare(child);
            }
            Compare(wide[0]);
        }
        Assert.True(parentsThatFolded > 10, $"only {parentsThatFolded} parents folded part of their children");
    }

    [Fact]
    public void RingsKeepTheirMembersApartAndCircumbinaryChildrenFar()
    {
        var rings = 0;
        var outer = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            var placed = Placed(zone);
            foreach (var node in placed.Where(k => !mass.ContainsKey(k) && !zone.Planets.Values.Any(b => b.Orbit.Key.Equals(k))))
            {
                var kids = ChildrenOf(zone, node, placed);
                var members = kids.Where(mass.ContainsKey).ToList();
                if (members.Count < 2) continue;
                var ring = members.Where(k => DistanceOf(zone, k) - DistanceOf(zone, members[0]) <= WellTree.RingWindow).ToList();
                if (ring.Count < 2) continue;
                rings++;
                var radius = DistanceOf(zone, ring[0]);
                var heavy = ring.Max(k => mass[k]);
                Assert.True(radius >= RocheMargin * Roche(zone, heavy) * (1 - Tolerance),
                    $"seed {seed}: a ring of radius {radius} sits inside its heavy member's Roche margin");
                foreach (var far in kids.Where(k => !ring.Contains(k)))
                {
                    outer++;
                    Assert.True(DistanceOf(zone, far) >= (1 + GravityField.RingReach) * GravityField.Beta * radius * (1 - Tolerance),
                        $"seed {seed}: a circumbinary child at {DistanceOf(zone, far)} is inside the ring group of radius {radius}");
                }
            }
        }
        Assert.True(rings > 3, $"only {rings} rings checked");
        _out.WriteLine($"rings {rings}, circumbinary children {outer}");
    }

    [Fact]
    public void BeltsKeepTheirEdges()
    {
        var belts = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            foreach (var (beltKey, belt) in zone.AsteroidBelts)
            {
                belts++;
                var orbit = zone.Planets[beltKey].Orbit.Key;
                var parent = zone.Orbits[orbit].Data.Parent.Key;
                var inner = belt.Data.Asteroids.Min(a => a.Distance);
                var outermost = belt.Data.Asteroids.Max(a => a.Distance);
                Assert.True(inner > 0f, $"seed {seed}: an asteroid sits at {inner}");
                if (mass.TryGetValue(parent, out var m))
                    Assert.True(inner >= RocheMargin * Roche(zone, m) * (1 - Tolerance), $"seed {seed}: a belt's inner edge {inner} is inside its parent's Roche margin");
                                // The belt keeps clear of its neighbours' rails by a sibling spacing (rule 3), inner and outer.
                var sibling = ChildrenOf(zone, parent, Placed(zone));
                var at = sibling.IndexOf(orbit);
                if (at > 0)
                    Assert.True(inner >= DistanceOf(zone, sibling[at - 1]) * (1 / (1 - GravityField.Reach / 2)) * (1 - Tolerance), $"seed {seed}: a belt reaches in to {inner}, within a spacing of its inner neighbour");
                if (at < sibling.Count - 1)
                    Assert.True(outermost <= DistanceOf(zone, sibling[at + 1]) * (1 - GravityField.Reach / 2) * (1 + Tolerance), $"seed {seed}: a belt reaches out to {outermost}, within a spacing of its outer neighbour");
                if (zone.Orbits[parent].Data.Parent.IsSet())
                    Assert.True(outermost <= GravityField.Gamma * GravityField.Reach * DistanceOf(zone, parent) / Math.Max(GravityField.Beta, 1 + GravityField.Reach) * (1 + Tolerance),
                        $"seed {seed}: a belt reaches {outermost}, past its parent's window");
            }
        }
        Assert.True(belts > 20, $"only {belts} belts checked");
    }

    private static float Percentile(List<float> sorted, float fraction) => sorted[Math.Min(sorted.Count - 1, (int) (fraction * sorted.Count))];

    // A belt fills the slot between its neighbours' spacing, its parent's Roche margin and the parent's window, up to the
    // authored width (ruling belt-fills-its-slot): it sits at the slot's midpoint where the test can spell both edges, and
    // none collapses to a line (finding belts-collapse-to-a-line). Rocks are scattered inside the belt's half width, so their
    // spread is at most the belt's width; a belt beside another belt has an edge only the generator knows, and is checked
    // against the weaker bound.
    [Fact]
    public void BeltsFillTheirSlots()
    {
        var q = 1 / (1 - GravityField.Reach / 2);
        var ratios = new List<float>();
        int belts = 0, bounded = 0, midpoints = 0, lines = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            var placed = Placed(zone);
            var beltAt = zone.AsteroidBelts.ToDictionary(b => zone.Planets[b.Key].Orbit.Key, b => b.Value.Data.Asteroids);
            foreach (var (orbit, rocks) in beltAt)
            {
                var parent = zone.Orbits[orbit].Data.Parent.Key;
                if (rocks.Length == 0 || !mass.TryGetValue(parent, out var parentMass)) continue;
                belts++;
                var sibling = ChildrenOf(zone, parent, placed);
                var at = sibling.IndexOf(orbit);
                var d = DistanceOf(zone, orbit);
                var spread = rocks.Max(a => a.Distance) - rocks.Min(a => a.Distance);
                var width = _zones.Settings.AsteroidBeltWidth.Evaluate(d);
                ratios.Add(spread / width);
                if (spread < 1e-3f) lines++;

                var prevIsBelt = at > 0 && beltAt.ContainsKey(sibling[at - 1]);
                var nextIsBelt = at < sibling.Count - 1 && beltAt.ContainsKey(sibling[at + 1]);
                var prevEdge = at == 0 ? 0f : prevIsBelt && beltAt[sibling[at - 1]].Length > 0 ? beltAt[sibling[at - 1]].Max(a => a.Distance) : DistanceOf(zone, sibling[at - 1]);
                var inner = Math.Max(RocheMargin * Roche(zone, parentMass), prevEdge * q);
                var grand = zone.Orbits[parent].Data.Parent;
                var window = grand.IsSet() ? GravityField.Gamma * GravityField.Reach * DistanceOf(zone, parent) / Math.Max(GravityField.Beta, 1 + GravityField.Reach) : float.PositiveInfinity;
                var next = at < sibling.Count - 1 && !nextIsBelt ? DistanceOf(zone, sibling[at + 1]) / q : float.PositiveInfinity;
                var outerLimit = Math.Min(window, next);
                if (!nextIsBelt) bounded++;
                Assert.True(spread <= Math.Min(width, outerLimit - inner) * (1 + Tolerance),
                    $"seed {seed}: a belt spreads {spread} in a slot of {outerLimit - inner} and an authored width of {width}");
                // Past the last sibling a root's window is its own to spell, and a ring member's child is held by a chord as well.
                var upperKnown = at < sibling.Count - 1 || (grand.IsSet() && mass.ContainsKey(grand.Key));
                if (prevIsBelt || nextIsBelt || !upperKnown) continue;
                midpoints++;
                Assert.True(Math.Abs(d - (inner + outerLimit) / 2) <= Tolerance * d,
                    $"seed {seed}: a belt at {d} is not the midpoint {(inner + outerLimit) / 2} of its slot [{inner}, {outerLimit}]");
            }
        }
        ratios.Sort();
        _out.WriteLine($"belts {belts}, with a slot bound {bounded}, midpoints checked {midpoints}, rock spread under 0.001: {lines}; " +
            $"spread / authored width at p10/25/50/75/90/max: {string.Join(" / ", new[] { .1f, .25f, .5f, .75f, .9f }.Select(f => Percentile(ratios, f).ToString("F3")))} / {ratios[ratios.Count - 1]:F3}");
        Assert.True(belts > 20 && bounded > 0 && midpoints > 0, $"{belts} belts, {bounded} bounded, {midpoints} midpoints: the seeds do not exercise the rule");
        Assert.True(lines == 0, $"{lines} of {belts} belts have no radial width");
    }

    // With the authored width narrower than the slot the width is the ceiling and the belt keeps it: neither wider (W/2 -> W, a
    // deleted cap) nor reduced to the slot.
    [Fact]
    public void ANarrowAuthoredBeltKeepsItsWidth()
    {
        var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot()).Read<ZoneGenerationSettings>("ZoneSettings").AsteroidBeltWidth;
        using var narrow = new Zones(1, 3, z => z.AsteroidBeltWidth = new ExponentialCurve
            { Exponent = authored.Exponent, Multiplier = authored.Multiplier * .02f, Constant = authored.Constant * .02f });
        int belts = 0, filled = 0;
        foreach (var (seed, zone) in narrow.All)
            foreach (var (key, belt) in zone.AsteroidBelts)
            {
                var rocks = belt.Data.Asteroids;
                if (rocks.Length < 2) continue;
                var width = narrow.Settings.AsteroidBeltWidth.Evaluate(DistanceOf(zone, zone.Planets[key].Orbit.Key));
                var spread = rocks.Max(a => a.Distance) - rocks.Min(a => a.Distance);
                belts++;
                Assert.True(spread <= width * (1 + Tolerance), $"seed {seed}: a belt spreads {spread}, past its authored width {width}");
                if (spread >= .5f * width) filled++;
            }
        _out.WriteLine($"narrow belts {belts}, spreading at least half the authored width {filled}");
        Assert.True(filled >= 10, $"only {filled} of {belts} belts fill the authored width");
    }

    private static GeneratorPlanet Body(GeneratorPlanet parent, float mass, float phase = 0, bool ring = false)
    {
        var node = new GeneratorPlanet { Parent = parent, Mass = mass, Phase = phase, Ring = ring };
        parent.Children.Add(node);
        return node;
    }

    private static PlanetSettings AuthoredPlanetSettings() => AuthoredSettings.Load(RunStartTests.FindRepoRoot()).Read<PlanetSettings>("PlanetSettings");

    // A rosette's ring round a root with no reach limit of its own sits at half the root's 0.75 x radius window, 3/8 of the radius.
    [Fact]
    public void ASoleRootRingSitsAtThreeEighthsOfItsRadius()
    {
        const float radius = 1e6f;
        var root = new GeneratorPlanet { Empty = true };
        var members = new[] { Body(root, 100f, 0, true), Body(root, 100f, .5f, true) };
        ZoneGenerator.Place(root, AuthoredPlanetSettings(), radius, float.PositiveInfinity);
        Assert.True(root.Empty && root.Children.Count == 2, "the ring folded into its node");
        foreach (var member in members)
            Assert.True(Math.Abs(member.Distance - .375f * radius) <= 1e-4f * radius, $"a ring member sits at {member.Distance}, not {.375f * radius}");
    }

    // A ring is judged on its members' settled masses: a member's own children fold into it when it settles
    // (ring-roche-fixed-in-generator-cut).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARingIsJudgedOnItsMembersSettledMasses(bool heavyGrows)
    {
        var settings = AuthoredPlanetSettings();
        const float mass = 1000f;
        var ringDistance = RocheMargin * settings.RocheLimit.Evaluate(mass) * 1.02f;
        var root = new GeneratorPlanet { Empty = true };
        var heavy = Body(root, mass, 0, true);
        var light = Body(root, .9f * mass, .5f, true);
        Body(heavyGrows ? heavy : light, .5f * mass);
        ZoneGenerator.Place(root, settings, ringDistance / .375f, float.PositiveInfinity);
        Assert.True(!root.Empty && root.Children.Count == 0 && Math.Abs(root.Mass - 2.4f * mass) <= 1e-3f * mass,
            $"the ring stood at {ringDistance} with a member grown to {Math.Max(heavy.Mass, light.Mass)}");
    }

    // A ring member that is itself bodyless (a binary of binaries) is still on its node's ring: from afar it pulls as one body of
    // its family's mass (binaries-orbited-from-afar), so it is placed and judged with the ring, never left at its node's centre.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ABodylessRingMemberSitsOnItsRing(bool bothBodyless)
    {
        const float radius = 1e6f;
        var root = new GeneratorPlanet { Empty = true };
        var a = Body(root, 100f, 0, true);
        var b = Body(root, 100f, .5f, true);
        foreach (var member in bothBodyless ? new[] { a, b } : new[] { a })
        {
            member.Empty = true;
            Body(member, 50f, 0, true);
            Body(member, 50f, .5f, true);
        }
        ZoneGenerator.Place(root, AuthoredPlanetSettings(), radius, float.PositiveInfinity);
        Assert.True(root.Empty && root.Children.Count == 2, "the ring folded into its node or lost a member");
        foreach (var member in new[] { a, b })
        {
            Assert.True(Math.Abs(member.Distance - .375f * radius) <= 1e-4f * radius, $"a ring member sits at {member.Distance}, not {.375f * radius}");
            Assert.True(member.Children.All(c => c.Distance > 0), "a bodyless member's own ring was never placed");
        }
    }

    private static float Total(GeneratorPlanet p) => (p.Empty ? 0f : p.Mass) + p.Children.Sum(Total);

    // A bodyless ring member is the heavy one when its family outweighs its siblings, whatever mass it was stored with before it
    // became a binary, and a family folded out of the ring's way joins it: a bodyless member of family 600 (stored 10), a body of
    // 100 and a non-ring child of 150 (a child that cannot fit beyond the ring always folds) keep all 850, and the body is untouched.
    [Fact]
    public void ABodylessHeavyMemberTakesTheFoldedChildAndItsMass()
    {
        var root = new GeneratorPlanet { Empty = true };
        var bodyless = Body(root, 10f, 0, true);
        bodyless.Empty = true;
        Body(bodyless, 300f, 0, true);
        Body(bodyless, 300f, .5f, true);
        var body = Body(root, 100f, .5f, true);
        var outer = Body(root, 150f);
        ZoneGenerator.Place(root, AuthoredPlanetSettings(), 1e8f, float.PositiveInfinity);
        Assert.True(root.Empty && root.Children.Count == 2 && !root.Children.Contains(outer), "the non-ring child did not fold out of the ring's way");
        Assert.True(Math.Abs(Total(root) - 850f) <= 1e-3f, $"the tree holds {Total(root)} of 850: a folded family's mass was lost");
        Assert.True(body.Mass == 100f, "the child folded into the lighter member, not the bodyless one whose family outweighs it");
    }

    // A ring is judged on its heaviest member's family, however little mass a bodyless member was stored with. With a Roche limit
    // steepening as mass squared, a ring of one bodyless member of nine 1000s and a body of 1 stands outside nine 1000s' Roche margin and
    // folds inside it; judged on the stored mass of 1, it would stand either way.
    [Theory]
    [InlineData(.98f, false)]
    [InlineData(1.02f, true)]
    public void ARingIsJudgedOnABodylessMembersFamily(float clearance, bool stands)
    {
        var settings = AuthoredPlanetSettings();
        settings.RocheLimit = new ExponentialCurve { Exponent = 2f, Multiplier = 1f, Constant = 0f };
        var ringDistance = clearance * RocheMargin * settings.RocheLimit.Evaluate(9000f);
        var root = new GeneratorPlanet { Empty = true };
        var bodyless = Body(root, 1f, 0, true);
        bodyless.Empty = true;
        for (var k = 0; k < 9; k++) Body(bodyless, 1000f, k / 9f, true);
        Body(root, 1f, .5f, true);
        ZoneGenerator.Place(root, settings, ringDistance / .375f, float.PositiveInfinity);
        Assert.True(root.Empty == stands, stands ? "a ring outside its heaviest family's Roche margin folded" : "a ring inside its heaviest family's Roche margin stood");
    }

    // A node with no ring still settles its children: a satellite of a child at distance 0 has no reach and folds into it.
    [Fact]
    public void ABodylessNodeWithoutARingStillSettlesItsChildren()
    {
        var root = new GeneratorPlanet { Empty = true };
        var child = Body(root, 500f);
        Body(child, 50f);
        Body(root, 700f);
        ZoneGenerator.Place(root, AuthoredPlanetSettings(), 1e8f, float.PositiveInfinity);
        Assert.True(child.Children.All(c => c.Distance > 0), "a child's satellite was left at its centre, unsettled");
    }

    // A ring member's children end within a fifth of the chord to the member's nearest partner, where the chord, not the member's
    // fade, is what holds them.
    [Fact]
    public void ARingMembersChildrenStayWithinAFifthOfTheChord()
    {
        const float radius = 1e6f;
        const int count = 12;
        var root = new GeneratorPlanet { Empty = true };
        var members = Enumerable.Range(0, count).Select(i => Body(root, 100f, (float) i / count, true)).ToList();
        foreach (var member in members) { Body(member, 10f); Body(member, 10f); }
        ZoneGenerator.Place(root, AuthoredPlanetSettings(), radius, float.PositiveInfinity);
        foreach (var member in members)
        {
            Assert.True(root.Children.Count == count && member.Children.Count == 2, "a ring member or its children folded");
            var chord = .2f * 2 * member.Distance * (float) Math.Sin(Math.PI / count);
            var fade = GravityField.Gamma * GravityField.Reach * member.Distance / Math.Max(GravityField.Beta, 1 + GravityField.Reach);
            Assert.True(chord < fade, "the fade, not the chord, would hold these children");
            var outermost = member.Children.Max(c => c.Distance);
            Assert.True(Math.Abs(outermost - chord) <= 1e-4f * chord, $"a ring member's outermost child sits at {outermost}, not at the chord's fifth {chord}");
        }
    }

    [Fact]
    public void NoStrengthlessOrbitRidesARing()
    {
        var rings = 0;
        foreach (var (seed, zone) in _zones.All)
        {
            var mass = BodyMass(zone);
            var placed = Placed(zone);
            foreach (var node in placed.Where(k => !mass.ContainsKey(k) && !zone.Planets.Values.Any(b => b.Orbit.Key.Equals(k))))
            {
                var members = ChildrenOf(zone, node, placed).Where(mass.ContainsKey).ToList();
                if (members.Count < 2) continue;
                rings++;
                var radius = DistanceOf(zone, members[0]);
                // Every orbit of the zone, stations and turrets included, that hangs on the node and carries no body.
                foreach (var orbit in zone.Orbits.Keys.Where(k => zone.Orbits[k].Data.Parent.IsSet() && zone.Orbits[k].Data.Parent.Key.Equals(node) && !mass.ContainsKey(k)))
                    Assert.True(DistanceOf(zone, orbit) >= (1 + GravityField.RingReach) * radius * (1 - Tolerance),
                        $"seed {seed}: a strengthless orbit at {DistanceOf(zone, orbit)} rides the ring group of radius {radius}");
            }
        }
        Assert.True(rings > 3, $"only {rings} rings checked");
    }
}
