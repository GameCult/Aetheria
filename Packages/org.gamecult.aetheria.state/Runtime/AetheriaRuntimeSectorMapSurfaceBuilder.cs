using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameCult.Aetheria.State.Verse
{
    /// <summary>
    /// Projects daemon-owned sector truth into the historical Aetheria map
    /// vocabulary without assigning gameplay authority to a renderer.
    /// </summary>
    public static class AetheriaRuntimeSectorMapSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.sector_map";
        public const string BackgroundAssetKey = "material.sector-map.background";
        public const string InfluenceAssetKey = "material.sector-map.influence";
        public const string StartIconAssetKey = "map.sector-map.icon.start";
        public const string TerminusIconAssetKey = "map.sector-map.icon.terminus";
        public const string HomeIconAssetKey = "map.sector-map.icon.home";
        public const string ExecutiveIconAssetKey = "map.sector-map.icon.executive";

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeSectorMapDocument sectorMap,
            string updatedAtUtc,
            long version = 1,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            sectorMap ??= new AetheriaRuntimeSectorMapDocument();
            updatedAtUtc ??= "";

            var discovered = new HashSet<int>(sectorMap.DiscoveredZoneIndices ?? Array.Empty<int>());
            foreach (var zone in sectorMap.Zones ?? Array.Empty<AetheriaRuntimeSectorMapZone>())
            {
                if (zone != null && zone.Discovered)
                    discovered.Add(zone.ZoneIndex);
            }
            if (sectorMap.CurrentZoneIndex >= 0)
                discovered.Add(sectorMap.CurrentZoneIndex);

            var allZones = (sectorMap.Zones ?? Array.Empty<AetheriaRuntimeSectorMapZone>())
                .Where(zone => zone != null)
                .OrderBy(zone => zone.ZoneIndex)
                .ToArray();
            var zones = allZones.Where(zone => discovered.Contains(zone.ZoneIndex)).ToArray();
            var zoneIndices = new HashSet<int>(zones.Select(zone => zone.ZoneIndex));
            var criticalEdges = CriticalPathEdges(sectorMap, allZones);
            var homeByZone = LandmarkByZone(sectorMap.HomeZones);
            var bossByZone = LandmarkByZone(sectorMap.BossZones);
            var factionIndices = zones
                .SelectMany(zone => (zone.FactionIndices ?? Array.Empty<int>()).Append(zone.OwnerFactionIndex))
                .Concat((sectorMap.HomeZones ?? Array.Empty<AetheriaRuntimeFactionZoneCommit>())
                    .Where(entry => entry != null).Select(entry => entry.FactionIndex))
                .Concat((sectorMap.BossZones ?? Array.Empty<AetheriaRuntimeFactionZoneCommit>())
                    .Where(entry => entry != null).Select(entry => entry.FactionIndex))
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            var children = new List<AetheriaRuntimeSurfaceComponent>();

            foreach (var factionIndex in factionIndices)
                children.Add(InfluenceRegion(factionIndex, zones, sectorMap, catalog));

            foreach (var link in (sectorMap.Links ?? Array.Empty<AetheriaRuntimeSectorMapLink>())
                         .Where(link => link != null && link.Discovered &&
                             zoneIndices.Contains(link.FromZoneIndex) && zoneIndices.Contains(link.ToZoneIndex))
                         .OrderBy(link => Math.Min(link.FromZoneIndex, link.ToZoneIndex))
                         .ThenBy(link => Math.Max(link.FromZoneIndex, link.ToZoneIndex)))
            {
                children.Add(Node(
                    $"{SurfaceId}.link.{link.FromZoneIndex}.{link.ToZoneIndex}",
                    "graph.edge",
                    ("from", ZoneId(link.FromZoneIndex)),
                    ("to", ZoneId(link.ToZoneIndex)),
                    ("critical", Bool(criticalEdges.Contains(EdgeKey(link.FromZoneIndex, link.ToZoneIndex)))),
                    ("discovered", "true")));
            }

            foreach (var zone in zones)
            {
                var homes = homeByZone.TryGetValue(zone.ZoneIndex, out var homeFactions)
                    ? homeFactions
                    : Array.Empty<int>();
                var bosses = bossByZone.TryGetValue(zone.ZoneIndex, out var bossFactions)
                    ? bossFactions
                    : Array.Empty<int>();
                children.Add(Node(
                    ZoneId(zone.ZoneIndex),
                    "graph.node",
                    ("label", zone.Name ?? ""),
                    ("x", Format(zone.X)),
                    ("y", Format(zone.Y)),
                    ("zoneIndex", zone.ZoneIndex.ToString(CultureInfo.InvariantCulture)),
                    ("ownerFactionIndex", zone.OwnerFactionIndex.ToString(CultureInfo.InvariantCulture)),
                    ("factionIndices", Join(zone.FactionIndices)),
                    ("homeFactionIndices", Join(homes)),
                    ("executiveFactionIndices", Join(bosses)),
                    ("current", Bool(zone.Current || zone.ZoneIndex == sectorMap.CurrentZoneIndex)),
                    ("entrance", Bool(zone.Entrance || zone.ZoneIndex == sectorMap.EntranceZoneIndex)),
                    ("exit", Bool(zone.Exit || zone.ZoneIndex == sectorMap.ExitZoneIndex)),
                    ("discovered", "true"),
                    ("role", Role(zone, sectorMap)),
                    ("landmarkAssetKeys", LandmarkAssets(zone, sectorMap, homes.Count > 0, bosses.Count > 0))));
            }

            var graph = new AetheriaRuntimeSurfaceComponent(
                $"{SurfaceId}.graph",
                "graph",
                Props(
                    ("label", "Aetheria Sector Map"),
                    ("coordinateSpace", "provider"),
                    ("backgroundAssetKey", BackgroundAssetKey),
                    ("influenceMaterialAssetKey", InfluenceAssetKey),
                    ("selectedNodeId", sectorMap.CurrentZoneIndex >= 0 ? ZoneId(sectorMap.CurrentZoneIndex) : ""),
                    ("pan", "true"),
                    ("zoom", "true"),
                    ("preserveAspect", "true")),
                children);

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "sector.map",
                title: "Sector Map",
                version: Math.Max(version, sectorMap.FrameId),
                updatedAtUtc: updatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    new AetheriaRuntimeSurfaceComponent(
                        $"{SurfaceId}.root",
                        "surface.map",
                        Props(("title", "Sector Map"), ("runId", sectorMap.RunId ?? "")),
                        new[] { graph, Legend(factionIndices, catalog) }),
                    StyleTokens(factionIndices, sectorMap.GenerationSeed, sectorMap.IsTutorial)),
                commands: Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>());
        }

        public static IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> StyleTokens(
            IEnumerable<int>? factionIndices = null,
            uint generationSeed = 0,
            bool tutorial = false)
        {
            var tokens = new List<AetheriaRuntimeSurfaceStyleToken>
            {
                Token("sectorMap.background.asset", BackgroundAssetKey),
                Token("sectorMap.background.noiseAmplitude", "1"),
                Token("sectorMap.background.noiseOffset", "0.3"),
                Token("sectorMap.background.noiseGain", "0.7"),
                Token("sectorMap.background.noiseLacunarity", "2"),
                Token("sectorMap.background.noiseFrequency", tutorial ? "0.1" : "1"),
                Token("sectorMap.background.noisePosition", tutorial ? "536.5106" : "91.29439"),
                Token("sectorMap.background.cloudExponent", "10"),
                Token("sectorMap.background.cloudAmplitude", "0.01"),
                Token("sectorMap.influence.material", InfluenceAssetKey),
                Token("sectorMap.influence.threshold", "1"),
                Token("sectorMap.influence.fillTiling", "1024"),
                Token("sectorMap.influence.fillBorderBlend", "0.05"),
                Token("sectorMap.influence.fillBlend", "20"),
                Token("sectorMap.influence.patternBlend", "0.25"),
                Token("sectorMap.influence.patternOffset", "-0.75"),
                Token("sectorMap.influence.fillAlpha", "0.4"),
                Token("sectorMap.influence.stroke", "4"),
                Token("sectorMap.influence.strokeBlend", "0.25"),
                Token("sectorMap.influence.strokeTransitionBlend", "1"),
                Token("sectorMap.faction.zonePrimaryBoost", "0.75"),
                Token("sectorMap.faction.zoneSecondaryBoost", "2"),
                Token("sectorMap.faction.linkBoost", "3"),
                Token("sectorMap.neutral.color", "#D9D9D9"),
                Token("sectorMap.zone.stroke", "#FFFFFF"),
                Token("sectorMap.zone.current.labelColor", "#FF720D"),
                Token("sectorMap.zone.current.scale", "1.25"),
                Token("sectorMap.link.width", "0.002"),
                Token("sectorMap.link.critical.width", "0.005"),
                Token("sectorMap.landmark.iconDistance", "1"),
                Token("sectorMap.landmark.backgroundSize", "3"),
                Token("sectorMap.label.offset", "0.4"),
                Token("sectorMap.reveal.linkDurationSeconds", "0.5"),
                Token("sectorMap.reveal.nodeDurationSeconds", "0.25"),
                Token("sectorMap.zoom.minimum", "0.1"),
                Token("sectorMap.zoom.maximum", "2")
            };

            foreach (var factionIndex in (factionIndices ?? Array.Empty<int>())
                         .Where(index => index >= 0).Distinct().OrderBy(index => index))
            {
                tokens.Add(Token($"sectorMap.faction.{factionIndex}.primary",
                    HsvHex(Fraction(factionIndex * 0.173), 0.62, 0.88)));
                tokens.Add(Token($"sectorMap.faction.{factionIndex}.secondary",
                    HsvHex(Fraction((factionIndex + 0.37) * 0.173), 0.48, 0.72)));
                tokens.Add(Token($"sectorMap.faction.{factionIndex}.fillTiltRadians",
                    Format(DeterministicTilt(generationSeed, factionIndex))));
            }
            return tokens;
        }

        private static AetheriaRuntimeSurfaceComponent InfluenceRegion(
            int factionIndex,
            IReadOnlyList<AetheriaRuntimeSectorMapZone> zones,
            AetheriaRuntimeSectorMapDocument map,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var samples = zones
                .Where(zone => (zone.FactionIndices?.Count ?? 0) > 0)
                .Select(zone => Node(
                    $"{SurfaceId}.influence.{factionIndex}.sample.{zone.ZoneIndex}",
                    "graph.region.sample",
                    ("nodeId", ZoneId(zone.ZoneIndex)),
                    ("value", zone.FactionIndices.Contains(factionIndex)
                        ? zone.OwnerFactionIndex == factionIndex ? "10" : "5"
                        : "-10")))
                .ToArray();
            return new AetheriaRuntimeSurfaceComponent(
                $"{SurfaceId}.influence.{factionIndex}",
                "graph.region",
                Props(
                    ("label", FactionLabel(catalog, factionIndex)),
                    ("factionIndex", factionIndex.ToString(CultureInfo.InvariantCulture)),
                    ("materialAssetKey", InfluenceAssetKey),
                    ("threshold", "1"),
                    ("fillTiltToken", $"sectorMap.faction.{factionIndex}.fillTiltRadians")),
                samples);
        }

        private static AetheriaRuntimeSurfaceComponent Legend(
            IReadOnlyList<int> factionIndices,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var items = factionIndices.Select(index => Node(
                    $"{SurfaceId}.legend.faction.{index}",
                    "graph.legend.item",
                    ("label", FactionLabel(catalog, index)),
                    ("role", "faction"),
                    ("factionIndex", index.ToString(CultureInfo.InvariantCulture))))
                .Concat(new[]
                {
                    LegendItem("start", "Start", StartIconAssetKey),
                    LegendItem("terminus", "Terminus", TerminusIconAssetKey),
                    LegendItem("home", "Home Zone", HomeIconAssetKey),
                    LegendItem("executive", "Executive", ExecutiveIconAssetKey)
                })
                .ToArray();
            return new AetheriaRuntimeSurfaceComponent(
                $"{SurfaceId}.legend",
                "graph.legend",
                Props(("label", "Legend")),
                items);
        }

        private static AetheriaRuntimeSurfaceComponent LegendItem(string id, string label, string assetKey) =>
            Node($"{SurfaceId}.legend.{id}", "graph.legend.item",
                ("label", label), ("role", id), ("assetKey", assetKey));

        private static Dictionary<int, IReadOnlyList<int>> LandmarkByZone(
            IReadOnlyList<AetheriaRuntimeFactionZoneCommit> entries) =>
            (entries ?? Array.Empty<AetheriaRuntimeFactionZoneCommit>())
                .Where(entry => entry != null && entry.ZoneIndex >= 0 && entry.FactionIndex >= 0)
                .GroupBy(entry => entry.ZoneIndex)
                .ToDictionary(group => group.Key,
                    group => (IReadOnlyList<int>)group.Select(entry => entry.FactionIndex).Distinct().OrderBy(index => index).ToArray());

        private static string LandmarkAssets(
            AetheriaRuntimeSectorMapZone zone,
            AetheriaRuntimeSectorMapDocument map,
            bool home,
            bool boss)
        {
            var assets = new List<string>();
            if (zone.Entrance || zone.ZoneIndex == map.EntranceZoneIndex) assets.Add(StartIconAssetKey);
            if (zone.Exit || zone.ZoneIndex == map.ExitZoneIndex) assets.Add(TerminusIconAssetKey);
            if (home) assets.Add(HomeIconAssetKey);
            if (boss) assets.Add(ExecutiveIconAssetKey);
            return string.Join(",", assets);
        }

        private static HashSet<string> CriticalPathEdges(
            AetheriaRuntimeSectorMapDocument map,
            IReadOnlyList<AetheriaRuntimeSectorMapZone> zones)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (map.EntranceZoneIndex < 0 || map.ExitZoneIndex < 0 || map.EntranceZoneIndex == map.ExitZoneIndex)
                return result;
            var byIndex = zones.ToDictionary(zone => zone.ZoneIndex);
            if (!byIndex.ContainsKey(map.EntranceZoneIndex) || !byIndex.ContainsKey(map.ExitZoneIndex))
                return result;
            var adjacency = byIndex.Keys.ToDictionary(index => index, _ => new List<int>());
            foreach (var link in map.Links ?? Array.Empty<AetheriaRuntimeSectorMapLink>())
            {
                if (link == null || !adjacency.ContainsKey(link.FromZoneIndex) || !adjacency.ContainsKey(link.ToZoneIndex))
                    continue;
                adjacency[link.FromZoneIndex].Add(link.ToZoneIndex);
                adjacency[link.ToZoneIndex].Add(link.FromZoneIndex);
            }
            var distance = byIndex.Keys.ToDictionary(index => index, _ => double.PositiveInfinity);
            var previous = new Dictionary<int, int>();
            var unvisited = new HashSet<int>(byIndex.Keys);
            distance[map.EntranceZoneIndex] = 0;
            while (unvisited.Count > 0)
            {
                var current = unvisited.OrderBy(index => distance[index]).ThenBy(index => index).First();
                if (double.IsPositiveInfinity(distance[current]) || current == map.ExitZoneIndex) break;
                unvisited.Remove(current);
                foreach (var adjacent in adjacency[current].Where(unvisited.Contains).OrderBy(index => index))
                {
                    var dx = byIndex[current].X - byIndex[adjacent].X;
                    var dy = byIndex[current].Y - byIndex[adjacent].Y;
                    var candidate = distance[current] + dx * dx + dy * dy;
                    if (candidate >= distance[adjacent]) continue;
                    distance[adjacent] = candidate;
                    previous[adjacent] = current;
                }
            }
            var cursor = map.ExitZoneIndex;
            while (cursor != map.EntranceZoneIndex && previous.TryGetValue(cursor, out var parent))
            {
                result.Add(EdgeKey(parent, cursor));
                cursor = parent;
            }
            return cursor == map.EntranceZoneIndex ? result : new HashSet<string>(StringComparer.Ordinal);
        }

        private static string FactionLabel(AetheriaRuntimeCatalogSnapshot? catalog, int factionIndex)
        {
            var corporations = catalog?.Corporations ?? Array.Empty<AetheriaRuntimeCorporation>();
            if (factionIndex < 0 || factionIndex >= corporations.Count || corporations[factionIndex] == null)
                return $"F{factionIndex}";
            var faction = corporations[factionIndex];
            return string.IsNullOrWhiteSpace(faction.ShortName)
                ? string.IsNullOrWhiteSpace(faction.Name) ? $"F{factionIndex}" : faction.Name
                : faction.ShortName;
        }

        private static string ZoneId(int zoneIndex) => $"{SurfaceId}.zone.{zoneIndex}";
        private static string EdgeKey(int left, int right) =>
            left < right ? $"{left}:{right}" : $"{right}:{left}";

        private static string Role(AetheriaRuntimeSectorMapZone zone, AetheriaRuntimeSectorMapDocument map)
        {
            if (zone.Current || zone.ZoneIndex == map.CurrentZoneIndex) return "current";
            if (zone.Entrance || zone.ZoneIndex == map.EntranceZoneIndex) return "entrance";
            if (zone.Exit || zone.ZoneIndex == map.ExitZoneIndex) return "exit";
            return "zone";
        }

        private static double DeterministicTilt(uint seed, int factionIndex)
        {
            var value = seed ^ unchecked((uint)(factionIndex + 1) * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            return value / (double)uint.MaxValue * Math.PI;
        }

        private static string HsvHex(double hue, double saturation, double value)
        {
            var sector = hue * 6;
            var index = (int)Math.Floor(sector);
            var fraction = sector - index;
            var p = value * (1 - saturation);
            var q = value * (1 - fraction * saturation);
            var t = value * (1 - (1 - fraction) * saturation);
            var (red, green, blue) = (index % 6) switch
            {
                0 => (value, t, p), 1 => (q, value, p), 2 => (p, value, t),
                3 => (p, q, value), 4 => (t, p, value), _ => (value, p, q)
            };
            return $"#{Byte(red):X2}{Byte(green):X2}{Byte(blue):X2}";
        }

        private static int Byte(double value) =>
            (int)Math.Round(Math.Max(0, Math.Min(1, value)) * 255, MidpointRounding.AwayFromZero);
        private static double Fraction(double value) => value - Math.Floor(value);
        private static AetheriaRuntimeSurfaceStyleToken Token(string name, string value) =>
            new AetheriaRuntimeSurfaceStyleToken(name, value);
        private static AetheriaRuntimeSurfaceComponent Node(
            string id, string kind, params (string Key, string Value)[] props) =>
            new AetheriaRuntimeSurfaceComponent(id, kind, Props(props), Array.Empty<AetheriaRuntimeSurfaceComponent>());
        private static IReadOnlyDictionary<string, string> Props(params (string Key, string Value)[] values) =>
            values.ToDictionary(value => value.Key, value => value.Value ?? "", StringComparer.Ordinal);
        private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Join(IEnumerable<int>? values) =>
            string.Join(",", values ?? Array.Empty<int>());
        private static string Bool(bool value) => value ? "true" : "false";
    }
}
