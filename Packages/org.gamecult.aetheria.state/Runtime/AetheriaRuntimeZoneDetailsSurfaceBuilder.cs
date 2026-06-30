using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeZoneDetailsBodyFacts
    {
        public AetheriaRuntimeZoneDetailsBodyFacts(string kind)
        {
            Kind = kind ?? "";
        }

        public string Kind { get; }
    }

    public sealed class AetheriaRuntimeZoneDetailsEntityFacts
    {
        public AetheriaRuntimeZoneDetailsEntityFacts(string hullType)
        {
            HullType = hullType ?? "";
        }

        public string HullType { get; }
    }

    public sealed class AetheriaRuntimeZoneDetailsFacts
    {
        public AetheriaRuntimeZoneDetailsFacts(
            double mass,
            double radius,
            IReadOnlyList<AetheriaRuntimeZoneDetailsBodyFacts> bodies,
            IReadOnlyList<AetheriaRuntimeZoneDetailsEntityFacts> entities,
            bool hasContents)
        {
            Mass = mass;
            Radius = radius;
            Bodies = bodies ?? Array.Empty<AetheriaRuntimeZoneDetailsBodyFacts>();
            Entities = entities ?? Array.Empty<AetheriaRuntimeZoneDetailsEntityFacts>();
            HasContents = hasContents;
        }

        public double Mass { get; }
        public double Radius { get; }
        public IReadOnlyList<AetheriaRuntimeZoneDetailsBodyFacts> Bodies { get; }
        public IReadOnlyList<AetheriaRuntimeZoneDetailsEntityFacts> Entities { get; }
        public bool HasContents { get; }
    }

    public static class AetheriaRuntimeZoneDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.sector_map.zone_details";
        public const string Close = "aetheria.sector_map.zone_details.close";

        public static AetheriaRuntimeZoneDetailsFacts Facts(
            AetheriaRuntimeZoneSnapshotCommit zone,
            Func<string, string> resolveHullType)
        {
            if (zone == null)
            {
                return new AetheriaRuntimeZoneDetailsFacts(
                    0,
                    0,
                    Array.Empty<AetheriaRuntimeZoneDetailsBodyFacts>(),
                    Array.Empty<AetheriaRuntimeZoneDetailsEntityFacts>(),
                    false);
            }

            return new AetheriaRuntimeZoneDetailsFacts(
                (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                    .Where(body => body != null)
                    .Sum(body => body.Mass),
                Math.Max(0, zone.GravityTerrainRadius),
                BodyFacts(zone),
                EntityFacts(zone, resolveHullType),
                true);
        }

        public static AetheriaRuntimeZoneDetailsFacts Facts(
            AetheriaRuntimeZoneDetailsDocument zone,
            Func<string, string> resolveHullType)
        {
            if (zone == null)
            {
                return new AetheriaRuntimeZoneDetailsFacts(
                    0,
                    0,
                    Array.Empty<AetheriaRuntimeZoneDetailsBodyFacts>(),
                    Array.Empty<AetheriaRuntimeZoneDetailsEntityFacts>(),
                    false);
            }

            resolveHullType ??= _ => "";
            return new AetheriaRuntimeZoneDetailsFacts(
                zone.Mass,
                Math.Max(0, zone.Radius),
                (zone.BodyKinds ?? Array.Empty<string>())
                    .Select(kind => new AetheriaRuntimeZoneDetailsBodyFacts(kind))
                    .ToArray(),
                (zone.EntityHullItemKeys ?? Array.Empty<string>())
                    .Select(hullItemKey => new AetheriaRuntimeZoneDetailsEntityFacts(resolveHullType(hullItemKey)))
                    .ToArray(),
                zone.HasContents);
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            string zoneName,
            string ownerName,
            string mass,
            string radius,
            IEnumerable<string> otherFactions,
            IEnumerable<AetheriaRuntimeZoneDetailsBodyFacts> bodies,
            IEnumerable<AetheriaRuntimeZoneDetailsEntityFacts> entities,
            bool hasContents,
            string updatedAtUtc,
            long version = 1)
        {
            zoneName ??= "";
            ownerName ??= "";
            mass ??= "";
            radius ??= "";
            updatedAtUtc ??= "";
            var bodyList = (bodies ?? Array.Empty<AetheriaRuntimeZoneDetailsBodyFacts>())
                .Where(body => body != null)
                .ToArray();
            var entityList = (entities ?? Array.Empty<AetheriaRuntimeZoneDetailsEntityFacts>())
                .Where(entity => entity != null)
                .ToArray();
            var sortedFactions = (otherFactions ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var planets = bodyList.Count(IsPlanetBody).ToString();
            var asteroidBelts = bodyList.Count(body => IsBodyKind(body, "asteroid_belt")).ToString();
            var gasGiants = bodyList.Count(body => IsBodyKind(body, "gas_giant")).ToString();
            var stars = bodyList.Count(body => IsBodyKind(body, "sun")).ToString();
            var stations = entityList.Count(entity => HasHullType(entity, "Station")).ToString();
            var turrets = entityList.Count(entity => HasHullType(entity, "Turret")).ToString();
            var ships = entityList.Count(entity => HasHullType(entity, "Ship")).ToString();

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.card",
                    zoneName,
                    Metric($"{SurfaceId}.owner", "Owner", string.IsNullOrWhiteSpace(ownerName) ? "None" : ownerName),
                    Metric($"{SurfaceId}.mass", "Mass", mass),
                    Metric($"{SurfaceId}.radius", "Radius", radius))
            };

            if (sortedFactions.Length > 0)
            {
                children.Add(Text(
                    $"{SurfaceId}.factions",
                    $"Factions Present: {string.Join(", ", sortedFactions)}"));
            }

            if (!hasContents)
            {
                children.Add(Text(
                    $"{SurfaceId}.unvisited",
                    "Has not been visited."));
            }
            else
            {
                children.Add(Card(
                    $"{SurfaceId}.contents",
                    "Contents",
                    Metric($"{SurfaceId}.planets", "Planets", planets),
                    Metric($"{SurfaceId}.belts", "Asteroid Belts", asteroidBelts),
                    Metric($"{SurfaceId}.giants", "Gas Giants", gasGiants),
                    Metric($"{SurfaceId}.stars", "Stars", stars),
                    Metric($"{SurfaceId}.stations", "Stations", stations),
                    Metric($"{SurfaceId}.turrets", "Turrets", turrets),
                    Metric($"{SurfaceId}.ships", "Ships", ships)));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "sector.map",
                title: zoneName,
                version: version,
                updatedAtUtc: updatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        $"{SurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        children.ToArray()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        public static AetheriaRuntimeSurfaceDocument BuildFromDocuments(
            AetheriaRuntimeZoneDetailsDocument zoneDetails,
            AetheriaRuntimeSectorMapDocument sectorMap,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            string updatedAtUtc,
            long version = 1)
        {
            var sectorZone = ResolveSectorZone(sectorMap, zoneDetails?.ZoneIndex ?? -1);
            var ownerFactionIndex = sectorZone?.OwnerFactionIndex ?? -1;
            var otherFactions = (sectorZone?.FactionIndices ?? Array.Empty<int>())
                .Where(index => index >= 0 && index != ownerFactionIndex)
                .Distinct()
                .Select(FormatFaction)
                .ToArray();
            var facts = Facts(zoneDetails, hullItemKey => ResolveHullType(catalog, hullItemKey));

            return Build(
                ResolveZoneName(sectorZone, zoneDetails),
                ownerFactionIndex >= 0 ? FormatFaction(ownerFactionIndex) : "None",
                FormatValue(facts.Mass, playerSettings),
                FormatValue(facts.Radius, playerSettings),
                otherFactions,
                facts.Bodies,
                facts.Entities,
                facts.HasContents,
                updatedAtUtc,
                version);
        }

        private static IReadOnlyList<AetheriaRuntimeZoneDetailsBodyFacts> BodyFacts(
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            return (zone?.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .Where(body => body != null)
                .Select(body => new AetheriaRuntimeZoneDetailsBodyFacts(body.Kind))
                .ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeZoneDetailsEntityFacts> EntityFacts(
            AetheriaRuntimeZoneSnapshotCommit zone,
            Func<string, string> resolveHullType)
        {
            resolveHullType ??= _ => "";
            return (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .Select(entity => new AetheriaRuntimeZoneDetailsEntityFacts(resolveHullType(entity.HullItemKey)))
                .ToArray();
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title ?? "") }, children);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label ?? ""), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id ?? "",
                kind ?? "",
                (props ?? Array.Empty<(string Key, string Value)>())
                    .ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static bool IsBodyKind(AetheriaRuntimeZoneDetailsBodyFacts body, string kind)
        {
            return body != null && string.Equals(body.Kind ?? "", kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlanetBody(AetheriaRuntimeZoneDetailsBodyFacts body)
        {
            return body != null &&
                   !IsBodyKind(body, "asteroid_belt") &&
                   !IsBodyKind(body, "gas_giant") &&
                   !IsBodyKind(body, "sun");
        }

        private static bool HasHullType(AetheriaRuntimeZoneDetailsEntityFacts entity, string hullType)
        {
            return entity != null && string.Equals(entity.HullType ?? "", hullType, StringComparison.Ordinal);
        }

        private static AetheriaRuntimeSectorMapZone ResolveSectorZone(
            AetheriaRuntimeSectorMapDocument sectorMap,
            int zoneIndex)
        {
            if (zoneIndex < 0)
                return null;

            return (sectorMap?.Zones ?? Array.Empty<AetheriaRuntimeSectorMapZone>())
                .FirstOrDefault(zone => zone.ZoneIndex == zoneIndex);
        }

        private static string ResolveZoneName(
            AetheriaRuntimeSectorMapZone sectorZone,
            AetheriaRuntimeZoneDetailsDocument zoneDetails)
        {
            if (!string.IsNullOrWhiteSpace(zoneDetails?.ZoneName))
                return zoneDetails.ZoneName;

            if (!string.IsNullOrWhiteSpace(sectorZone?.Name))
                return sectorZone.Name;

            return sectorZone == null ? "Unknown" : $"Zone {sectorZone.ZoneIndex}";
        }

        private static string ResolveHullType(
            AetheriaRuntimeCatalogSnapshot catalog,
            string hullItemKey)
        {
            var typedHull = catalog?.FindItem(hullItemKey ?? "");
            return typedHull?.HullType ?? "";
        }

        private static string FormatFaction(int factionIndex)
        {
            return factionIndex < 0 ? "None" : $"Faction {factionIndex}";
        }

        private static string FormatValue(
            double value,
            AetheriaRuntimePlayerSettingsDocument playerSettings)
        {
            var digits = playerSettings?.SignificantDigits ?? 3;
            var magnitude = value == 0 ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1;
            digits -= magnitude;
            if (digits < 0)
                digits = 0;

            var formatted = value.ToString($"N{digits}", CultureInfo.CurrentCulture);
            var decimalSeparator = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            return formatted.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
                ? formatted.TrimEnd('0').TrimEnd(decimalSeparator)
                : formatted;
        }
    }

    public enum AetheriaRuntimeZoneDetailsCommandKind
    {
        Unknown = 0,
        Close = 1
    }

    public readonly struct AetheriaRuntimeZoneDetailsCommand
    {
        public AetheriaRuntimeZoneDetailsCommand(AetheriaRuntimeZoneDetailsCommandKind kind)
        {
            Kind = kind;
        }

        public AetheriaRuntimeZoneDetailsCommandKind Kind { get; }
    }

    public static class AetheriaRuntimeZoneDetailsSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeZoneDetailsCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeZoneDetailsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            if (string.Equals(request.Operation?.OperationId, AetheriaRuntimeZoneDetailsSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeZoneDetailsCommand(AetheriaRuntimeZoneDetailsCommandKind.Close);
                return true;
            }

            return false;
        }
    }
}
