using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeZoneDetailsSurfaceState
    {
        public AetheriaRuntimeZoneDetailsSurfaceState(
            string zoneName,
            string ownerName,
            string mass,
            string radius,
            IReadOnlyList<string> otherFactions,
            bool hasContents,
            string planets,
            string asteroidBelts,
            string gasGiants,
            string stars,
            string stations,
            string turrets,
            string ships,
            string updatedAtUtc)
        {
            ZoneName = zoneName ?? "";
            OwnerName = ownerName ?? "";
            Mass = mass ?? "";
            Radius = radius ?? "";
            OtherFactions = otherFactions ?? Array.Empty<string>();
            HasContents = hasContents;
            Planets = planets ?? "";
            AsteroidBelts = asteroidBelts ?? "";
            GasGiants = gasGiants ?? "";
            Stars = stars ?? "";
            Stations = stations ?? "";
            Turrets = turrets ?? "";
            Ships = ships ?? "";
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string ZoneName { get; }
        public string OwnerName { get; }
        public string Mass { get; }
        public string Radius { get; }
        public IReadOnlyList<string> OtherFactions { get; }
        public bool HasContents { get; }
        public string Planets { get; }
        public string AsteroidBelts { get; }
        public string GasGiants { get; }
        public string Stars { get; }
        public string Stations { get; }
        public string Turrets { get; }
        public string Ships { get; }
        public string UpdatedAtUtc { get; }
    }

    public sealed class AetheriaRuntimeZoneDetailsBodyProjection
    {
        public AetheriaRuntimeZoneDetailsBodyProjection(string kind)
        {
            Kind = kind ?? "";
        }

        public string Kind { get; }
    }

    public sealed class AetheriaRuntimeZoneDetailsEntityProjection
    {
        public AetheriaRuntimeZoneDetailsEntityProjection(string hullType)
        {
            HullType = hullType ?? "";
        }

        public string HullType { get; }
    }

    public sealed class AetheriaRuntimeZoneDetailsDaemonProjection
    {
        public AetheriaRuntimeZoneDetailsDaemonProjection(
            double mass,
            double radius,
            IReadOnlyList<AetheriaRuntimeZoneDetailsBodyProjection> bodies,
            IReadOnlyList<AetheriaRuntimeZoneDetailsEntityProjection> entities,
            bool hasContents)
        {
            Mass = mass;
            Radius = radius;
            Bodies = bodies ?? Array.Empty<AetheriaRuntimeZoneDetailsBodyProjection>();
            Entities = entities ?? Array.Empty<AetheriaRuntimeZoneDetailsEntityProjection>();
            HasContents = hasContents;
        }

        public double Mass { get; }
        public double Radius { get; }
        public IReadOnlyList<AetheriaRuntimeZoneDetailsBodyProjection> Bodies { get; }
        public IReadOnlyList<AetheriaRuntimeZoneDetailsEntityProjection> Entities { get; }
        public bool HasContents { get; }
    }

    public static class AetheriaRuntimeZoneDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.sector_map.zone_details";
        public const string Close = "aetheria.sector_map.zone_details.close";

        public static AetheriaRuntimeZoneDetailsDaemonProjection ProjectDaemonZone(
            AetheriaRuntimeZoneSnapshotCommit zone,
            Func<string, string> resolveHullType)
        {
            if (zone == null)
            {
                return new AetheriaRuntimeZoneDetailsDaemonProjection(
                    0,
                    0,
                    Array.Empty<AetheriaRuntimeZoneDetailsBodyProjection>(),
                    Array.Empty<AetheriaRuntimeZoneDetailsEntityProjection>(),
                    false);
            }

            return new AetheriaRuntimeZoneDetailsDaemonProjection(
                (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                    .Where(body => body != null)
                    .Sum(body => body.Mass),
                Math.Max(0, zone.GravityTerrainRadius),
                ProjectBodies(zone),
                ProjectEntities(zone, resolveHullType),
                true);
        }

        public static AetheriaRuntimeZoneDetailsSurfaceState Project(
            string zoneName,
            string ownerName,
            string mass,
            string radius,
            IEnumerable<string> otherFactions,
            IEnumerable<AetheriaRuntimeZoneDetailsBodyProjection> bodies,
            IEnumerable<AetheriaRuntimeZoneDetailsEntityProjection> entities,
            bool hasContents,
            string updatedAtUtc)
        {
            var bodyList = (bodies ?? Array.Empty<AetheriaRuntimeZoneDetailsBodyProjection>())
                .Where(body => body != null)
                .ToArray();
            var entityList = (entities ?? Array.Empty<AetheriaRuntimeZoneDetailsEntityProjection>())
                .Where(entity => entity != null)
                .ToArray();

            return new AetheriaRuntimeZoneDetailsSurfaceState(
                zoneName,
                ownerName,
                mass,
                radius,
                (otherFactions ?? Array.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray(),
                hasContents,
                bodyList.Count(IsPlanetBody).ToString(),
                bodyList.Count(body => IsBodyKind(body, "asteroid_belt")).ToString(),
                bodyList.Count(body => IsBodyKind(body, "gas_giant")).ToString(),
                bodyList.Count(body => IsBodyKind(body, "sun")).ToString(),
                entityList.Count(entity => HasHullType(entity, "Station")).ToString(),
                entityList.Count(entity => HasHullType(entity, "Turret")).ToString(),
                entityList.Count(entity => HasHullType(entity, "Ship")).ToString(),
                updatedAtUtc);
        }

        private static IReadOnlyList<AetheriaRuntimeZoneDetailsBodyProjection> ProjectBodies(
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            return (zone?.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .Where(body => body != null)
                .Select(body => new AetheriaRuntimeZoneDetailsBodyProjection(body.Kind))
                .ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeZoneDetailsEntityProjection> ProjectEntities(
            AetheriaRuntimeZoneSnapshotCommit zone,
            Func<string, string> resolveHullType)
        {
            resolveHullType ??= _ => "";
            return (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .Select(entity => new AetheriaRuntimeZoneDetailsEntityProjection(resolveHullType(entity.HullItemKey)))
                .ToArray();
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeZoneDetailsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeZoneDetailsSurfaceState(
                "",
                "",
                "",
                "",
                Array.Empty<string>(),
                false,
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "");

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.card",
                    state.ZoneName,
                    Metric($"{SurfaceId}.owner", "Owner", string.IsNullOrWhiteSpace(state.OwnerName) ? "None" : state.OwnerName),
                    Metric($"{SurfaceId}.mass", "Mass", state.Mass),
                    Metric($"{SurfaceId}.radius", "Radius", state.Radius))
            };

            if (state.OtherFactions.Count > 0)
            {
                children.Add(Text(
                    $"{SurfaceId}.factions",
                    $"Factions Present: {string.Join(", ", state.OtherFactions)}"));
            }

            if (!state.HasContents)
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
                    Metric($"{SurfaceId}.planets", "Planets", state.Planets),
                    Metric($"{SurfaceId}.belts", "Asteroid Belts", state.AsteroidBelts),
                    Metric($"{SurfaceId}.giants", "Gas Giants", state.GasGiants),
                    Metric($"{SurfaceId}.stars", "Stars", state.Stars),
                    Metric($"{SurfaceId}.stations", "Stations", state.Stations),
                    Metric($"{SurfaceId}.turrets", "Turrets", state.Turrets),
                    Metric($"{SurfaceId}.ships", "Ships", state.Ships)));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "sector.map",
                title: state.ZoneName,
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
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

        private static bool IsBodyKind(AetheriaRuntimeZoneDetailsBodyProjection body, string kind)
        {
            return body != null && string.Equals(body.Kind ?? "", kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlanetBody(AetheriaRuntimeZoneDetailsBodyProjection body)
        {
            return body != null &&
                   !IsBodyKind(body, "asteroid_belt") &&
                   !IsBodyKind(body, "gas_giant") &&
                   !IsBodyKind(body, "sun");
        }

        private static bool HasHullType(AetheriaRuntimeZoneDetailsEntityProjection entity, string hullType)
        {
            return entity != null && string.Equals(entity.HullType ?? "", hullType, StringComparison.Ordinal);
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

            if (string.Equals(request.Command, AetheriaRuntimeZoneDetailsSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeZoneDetailsCommand(AetheriaRuntimeZoneDetailsCommandKind.Close);
                return true;
            }

            return false;
        }
    }
}
