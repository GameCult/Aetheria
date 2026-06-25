using System;
using System.Linq;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State;

public static class AetheriaCatalogSurfaceProjector
{
    public const string SurfaceKey = "eve:surface:aetheria.catalog.operator";
    public const string SurfaceId = AetheriaRuntimeCatalogCommands.SurfaceId;

    public static EveSurfaceState Build(AetheriaCatalogSnapshot catalog, string updatedAtUtc, long version = 1)
    {
        var tradeItems = catalog.TradeItems.Take(12).Select(item =>
            Row(
                $"item.{SafeId(item.LegacyId)}",
                ("name", item.Name),
                ("manufacturer", catalog.GetManufacturer(item)?.Name ?? "GameCult"),
                ("price", item.Price.ToString("N0")),
                ("size", item.ShapeWidth > 0 && item.ShapeHeight > 0 ? $"{item.ShapeWidth}x{item.ShapeHeight}" : "")))
            .ToArray();

        var corporations = catalog.Corporations.Take(12).Select(corporation =>
            Row(
                $"corporation.{SafeId(corporation.LegacyId)}",
                ("name", corporation.Name),
                ("short", corporation.ShortName),
                ("names", catalog.GetNameFile(corporation)?.Name ?? ""),
                ("influence", corporation.InfluenceDistance.ToString())))
            .ToArray();
        var refreshCommand = GameCult.Mesh.CultMesh.OperationBindingRecord(
            GameCult.Mesh.CultMesh.OperationBinding(
                AetheriaRuntimeCatalogCommands.Refresh,
                label: "Refresh",
                routeHint: new GameCult.Mesh.CultMeshRouteHint(
                    GameCult.Mesh.CultMeshLocalityKind.Automatic,
                    "cultmesh")));

        return new EveSurfaceState
        {
            ProviderId = "aetheria",
            ProviderKind = "game.runtime",
            Title = "Aetheria Catalog",
            Version = version,
            UpdatedAtUtc = updatedAtUtc,
            Surface = new EveSurface
            {
                Id = SurfaceId,
                Root = Node(
                    "aetheria.catalog.root",
                    "surface",
                    [],
                    Node(
                        "aetheria.catalog.summary",
                        "grid",
                        [("columns", "6")],
                        Metric("summary.items", "Items", catalog.Items.Count.ToString()),
                        Metric("summary.trade", "Trade Items", catalog.TradeItems.Count().ToString()),
                        Metric("summary.equipment", "Equipment", catalog.EquipmentItems.Count().ToString()),
                        Metric("summary.behaviors", "Behavior Kinds", catalog.Items.SelectMany(item => item.BehaviorKinds).Distinct().Count().ToString()),
                        Metric("summary.corporations", "Corporations", catalog.Corporations.Count.ToString()),
                        Metric("summary.nameFiles", "Name Files", catalog.NameFiles.Count.ToString())),
                    Node(
                        "aetheria.catalog.trade",
                        "card",
                        [("title", "Trade Catalog")],
                        Node("aetheria.catalog.trade.rows", "inspector.kv", [], tradeItems)),
                    Node(
                        "aetheria.catalog.corporations",
                        "card",
                        [("title", "Corporations")],
                        Node("aetheria.catalog.corporation.rows", "inspector.kv", [], corporations)))
            },
            Commands =
            [
                new EveCommandTemplate
                {
                    Command = refreshCommand.OperationId,
                    Label = refreshCommand.Label,
                    Transport = refreshCommand.RouteDescription,
                    SchemaId = refreshCommand.SchemaId,
                    RouteKind = refreshCommand.RouteKind,
                    RouteDescription = refreshCommand.RouteDescription
                }
            ]
        };
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", [("label", label), ("value", value)]);
    }

    private static EveSurfaceComponent Row(string id, params (string Key, string Value)[] props)
    {
        return Node(id, "row", props);
    }

    private static EveSurfaceComponent Node(
        string id,
        string kind,
        (string Key, string Value)[] props,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceComponent
        {
            Id = id,
            Kind = kind,
            Props = props.ToDictionary(prop => prop.Key, prop => prop.Value),
            Children = children
        };
    }

    private static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "empty"
            : new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '.').ToArray());
    }
}
