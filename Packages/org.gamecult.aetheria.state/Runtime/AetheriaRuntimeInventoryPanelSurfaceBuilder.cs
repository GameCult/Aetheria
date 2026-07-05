using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeInventoryPanelSurfaceRequest
    {
        public string ViewTitle { get; set; } = "";

        public string DisplayedEntityKey { get; set; } = "";

        public int DisplayedEntityIndex { get; set; } = -1;

        public string DisplayedCargoEntityKey { get; set; } = "";

        public int DisplayedCargoEntityIndex { get; set; } = -1;

        public int DisplayedCargoIndex { get; set; } = -1;

        public bool ThermalView { get; set; }

        public bool HudView { get; set; }

        public bool HasDragSession { get; set; }

        public string DragItemKey { get; set; } = "";

        public string DragSourceKind { get; set; } = "";

        public string DragSourceEntityKey { get; set; } = "";

        public int DragSourceIndex { get; set; } = -1;

        public int DragOriginOffsetX { get; set; }

        public int DragOriginOffsetY { get; set; }

        public string DragRotation { get; set; } = "";

        public int HoverCellX { get; set; } = -1;

        public int HoverCellY { get; set; } = -1;
    }

    public static class AetheriaRuntimeInventoryPanelSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.panel";
        public const string ToggleThermal = "aetheria.inventory.panel.toggle_thermal";
        public const string SetCurrent = "aetheria.inventory.panel.set_current";
        public const string EditName = "aetheria.inventory.panel.edit_name";
        public const string OpenNavigation = "aetheria.inventory.panel.open_navigation";
        public const string DropdownSlotId = "aetheria.inventory.panel.dropdown.slot";

        public static AetheriaRuntimeSurfaceDocument BuildFromDocuments(
            AetheriaRuntimeCurrentEntityDocument currentEntity,
            AetheriaRuntimeStationRefitDocument stationRefit,
            AetheriaRuntimeInventoryDocument displayedInventory,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            AetheriaRuntimeInventoryPanelSurfaceRequest request,
            string dropdownSurfaceDocumentId,
            string updatedAtUtc,
            long version = 1)
        {
            request ??= new AetheriaRuntimeInventoryPanelSurfaceRequest();
            var viewTitle = ResolveViewTitle(request, displayedInventory);
            var isEquipmentView = !string.IsNullOrWhiteSpace(request.DisplayedEntityKey);
            var isCargoView = !string.IsNullOrWhiteSpace(request.DisplayedCargoEntityKey) &&
                              request.DisplayedCargoIndex >= 0;
            var items = ResolveItems(displayedInventory, request, isEquipmentView, isCargoView);
            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.summary",
                    viewTitle,
                    Layout(("gridArea", "summary")),
                    PanelStyle(),
                    Metric($"{SurfaceId}.summary.mode", "Mode", request.ThermalView ? "Thermal" : "Inventory"),
                    Metric($"{SurfaceId}.summary.entity", "Entity", ResolveEntityName(stationRefit, request)),
                    Metric($"{SurfaceId}.summary.current", "Current Ship", IsCurrentEntity(currentEntity, request) ? "Yes" : "No"),
                    Metric($"{SurfaceId}.summary.drag", "Drag", request.HasDragSession ? "Active" : "None"))
            };

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Layout(("gridArea", "actions"), ("display", "flex"), ("flexWrap", "wrap"), ("gap", "8"), ("alignSelf", "start")),
                Style(),
                Button($"{SurfaceId}.actions.navigate", "Navigate", OpenNavigation),
                Button($"{SurfaceId}.actions.thermal", request.ThermalView ? "Inventory" : "Thermal", ToggleThermal),
                Button($"{SurfaceId}.actions.current", "Set Current", SetCurrent),
                Button($"{SurfaceId}.actions.rename", "Rename", EditName)));

            children.Add(DragSession($"{SurfaceId}.drag", request));
            children.Add(EmbeddedDropdownSlot($"{SurfaceId}.dropdown", dropdownSurfaceDocumentId));
            children.Add(Grid($"{SurfaceId}.grid", items, catalog, playerSettings));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "inventory.panel",
                title: viewTitle,
                version: version,
                updatedAtUtc: updatedAtUtc ?? "",
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        $"{SurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        children.ToArray(),
                        Layout(
                            ("display", "grid"),
                            ("gridTemplateColumns", "minmax(260px, 320px) minmax(190px, 240px) minmax(360px, 1fr) minmax(320px, 0.9fr)"),
                            ("gridTemplateRows", "auto minmax(240px, 1fr)"),
                            ("gridTemplateAreas", "\"summary actions drag dropdown\" \"grid grid drag dropdown\""),
                            ("gap", "10"),
                            ("alignItems", "start")),
                        Style()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(OpenNavigation, "Navigate", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ToggleThermal, request.ThermalView ? "Inventory" : "Thermal", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(SetCurrent, "Set Current", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(EditName, "Rename", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        private static IReadOnlyList<AetheriaRuntimeInventoryItem> ResolveItems(
            AetheriaRuntimeInventoryDocument displayedInventory,
            AetheriaRuntimeInventoryPanelSurfaceRequest request,
            bool isEquipmentView,
            bool isCargoView)
        {
            if (displayedInventory == null)
                return Array.Empty<AetheriaRuntimeInventoryItem>();

            if (isEquipmentView)
                return displayedInventory.Equipment ?? Array.Empty<AetheriaRuntimeInventoryItem>();

            if (isCargoView)
            {
                return (displayedInventory.Cargo ?? Array.Empty<AetheriaRuntimeInventoryItem>())
                    .Where(item => item != null && item.SourceIndex == request.DisplayedCargoIndex)
                    .ToArray();
            }

            return Array.Empty<AetheriaRuntimeInventoryItem>();
        }

        private static AetheriaRuntimeSurfaceComponent Grid(
            string id,
            IReadOnlyList<AetheriaRuntimeInventoryItem> items,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimePlayerSettingsDocument playerSettings)
        {
            var props = new List<(string Key, string Value)>
            {
                ("count", "0"),
                ("columns", "6"),
                ("rows", "3"),
                ("cellSize", "72"),
                ("cellGap", "4"),
                ("cellSpriteMode", "bitmask8"),
                ("cellSpriteColumns", "16"),
                ("cellSpriteRows", "16")
            };
            AddAssetProps(props, "backgroundAtlas", AetheriaRuntimeAssets.InventoryCellBackgroundAtlas());
            AddAssetProps(props, "foregroundAtlas", AetheriaRuntimeAssets.InventoryCellForegroundAtlas());
            AddAssetProps(props, "thermalLayer", AetheriaRuntimeAssets.InventoryThermalLayerAtlas());

            var cells = (items ?? Array.Empty<AetheriaRuntimeInventoryItem>())
                .Where(item => item != null)
                .OrderBy(item => item.SourceIndex)
                .ThenBy(item => item.X)
                .ThenBy(item => item.Y)
                .Select(item => ItemCell(id, item, catalog, playerSettings))
                .ToArray();
            props[0] = ("count", cells.Length.ToString(CultureInfo.InvariantCulture));
            return Node(
                id,
                "inventory.grid",
                props,
                cells,
                Layout(
                    ("gridArea", "grid"),
                    ("alignSelf", "stretch"),
                    ("minHeight", "260"),
                    ("sectionLabel.margin", "0 0 8px"),
                    ("board.overflow", "auto"),
                    ("board.padding", "4"),
                    ("cell.minWidth", "0"),
                    ("cell.minHeight", "0")),
                Style(
                    ("background", "rgba(7, 25, 24, 0.72)"),
                    ("borderWidth", "1"),
                    ("borderStyle", "solid"),
                    ("borderColor", "var(--line)"),
                    ("borderRadius", "6"),
                    ("sectionLabel.color", "var(--quiet)"),
                    ("sectionLabel.font", "700 10px var(--font-mono)"),
                    ("sectionLabel.textTransform", "uppercase"),
                    ("cell.background", "rgba(10, 36, 35, 0.42)"),
                    ("cell.borderWidth", "1"),
                    ("cell.borderStyle", "solid"),
                    ("cell.borderColor", "rgba(103, 240, 228, 0.12)")));
        }

        private static AetheriaRuntimeSurfaceComponent DragSession(
            string id,
            AetheriaRuntimeInventoryPanelSurfaceRequest request)
        {
            return Node(
                id,
                "inventory.drag_session",
                new[]
                {
                    ("active", request.HasDragSession ? "true" : "false"),
                    ("itemKey", request.DragItemKey ?? ""),
                    ("sourceKind", request.DragSourceKind ?? ""),
                    ("sourceEntityKey", request.DragSourceEntityKey ?? ""),
                    ("sourceIndex", request.DragSourceIndex.ToString(CultureInfo.InvariantCulture)),
                    ("originOffsetX", request.DragOriginOffsetX.ToString(CultureInfo.InvariantCulture)),
                    ("originOffsetY", request.DragOriginOffsetY.ToString(CultureInfo.InvariantCulture)),
                    ("rotation", request.DragRotation ?? ""),
                    ("hoverCellX", request.HoverCellX.ToString(CultureInfo.InvariantCulture)),
                    ("hoverCellY", request.HoverCellY.ToString(CultureInfo.InvariantCulture))
                },
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                Layout(
                    ("gridArea", "drag"),
                    ("minHeight", "320"),
                    ("sectionLabel.margin", "0 0 8px"),
                    ("dragChip.width", "max-content"),
                    ("dragChip.maxWidth", "100%"),
                    ("dragChip.padding", "6px 8px")),
                Style(
                    ("background", "rgba(7, 25, 24, 0.92)"),
                    ("borderWidth", "1"),
                    ("borderStyle", "solid"),
                    ("borderColor", "var(--line)"),
                    ("borderRadius", "6"),
                    ("sectionLabel.color", "var(--quiet)"),
                    ("sectionLabel.font", "700 10px var(--font-mono)"),
                    ("sectionLabel.textTransform", "uppercase"),
                    ("empty.color", "var(--text)"),
                    ("empty.font", "600 12px var(--font-mono)"),
                    ("meta.color", "var(--text)"),
                    ("meta.font", "600 12px var(--font-mono)"),
                    ("dragChip.background", "rgba(10, 36, 35, 0.96)"),
                    ("dragChip.borderWidth", "1"),
                    ("dragChip.borderStyle", "solid"),
                    ("dragChip.borderColor", "var(--line-hot)"),
                    ("dragChip.borderRadius", "4"),
                    ("dragChip.color", "var(--accent)"),
                    ("dragChip.font", "700 12px var(--font-mono)")));
        }

        private static AetheriaRuntimeSurfaceComponent EmbeddedDropdownSlot(
            string id,
            string dropdownSurfaceDocumentId)
        {
            return Node(
                id,
                "surface.slot",
                new[]
                {
                    ("slotId", DropdownSlotId),
                    ("documentId", dropdownSurfaceDocumentId ?? ""),
                    ("schemaId", "gamecult.aetheria.runtime_surface.v1"),
                    ("presentationKind", "inventory.dropdown")
                },
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                new[]
                {
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        DropdownSlotId,
                        dropdownSurfaceDocumentId ?? "",
                        "gamecult.aetheria.runtime_surface.v1",
                        "inventory.dropdown")
                },
                Layout(("gridArea", "dropdown")),
                Style());
        }

        private static AetheriaRuntimeSurfaceComponent ItemCell(
            string gridId,
            AetheriaRuntimeInventoryItem item,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimePlayerSettingsDocument playerSettings)
        {
            var typedItem = catalog?.FindItem(item.ItemKey ?? "");
            var props = new List<(string Key, string Value)>
            {
                ("itemKey", item.ItemKey ?? ""),
                ("label", string.IsNullOrWhiteSpace(typedItem?.Name) ? item.ItemKey ?? "" : typedItem.Name),
                ("source", item.Source ?? ""),
                ("sourceIndex", item.SourceIndex.ToString(CultureInfo.InvariantCulture)),
                ("x", item.X.ToString(CultureInfo.InvariantCulture)),
                ("y", item.Y.ToString(CultureInfo.InvariantCulture)),
                ("quantity", item.Quantity.ToString(CultureInfo.InvariantCulture)),
                ("quality", FormatValue(item.Quality, playerSettings)),
                ("durability", FormatValue(item.Durability, playerSettings))
            };
            AddAssetProps(props, "icon", ResolveItemIconAsset(item, typedItem));
            return Node(
                $"{gridId}.item.{SafeId(item.ItemKey)}.{item.SourceIndex}.{item.X}.{item.Y}",
                "inventory.item",
                props,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                Layout(
                    ("width", "72"),
                    ("height", "72"),
                    ("minWidth", "0"),
                    ("minHeight", "0"),
                    ("position", "relative"),
                    ("display", "grid"),
                    ("placeItems", "center"),
                    ("cursor", "pointer"),
                    ("icon.display", "grid"),
                    ("icon.placeItems", "center"),
                    ("icon.width", "28"),
                    ("icon.height", "28"),
                    ("label.position", "absolute"),
                    ("label.right", "4"),
                    ("label.bottom", "3"),
                    ("label.left", "4"),
                    ("quantity.position", "absolute"),
                    ("quantity.top", "2"),
                    ("quantity.right", "4")),
                Style(
                    ("background", "rgba(10, 36, 35, 0.96)"),
                    ("borderWidth", "1"),
                    ("borderStyle", "solid"),
                    ("borderColor", "var(--line-hot)"),
                    ("borderRadius", "4"),
                    ("color", "var(--text-bright)"),
                    ("font", "700 11px var(--font-mono)"),
                    ("icon.borderWidth", "1"),
                    ("icon.borderStyle", "solid"),
                    ("icon.borderColor", "rgba(255, 184, 79, 0.34)"),
                    ("icon.borderRadius", "50%"),
                    ("icon.color", "var(--accent)"),
                    ("icon.background", "rgba(1, 7, 7, 0.62)"),
                    ("icon.fontSize", "14"),
                    ("label.overflow", "hidden"),
                    ("label.color", "var(--quiet)"),
                    ("label.fontSize", "9"),
                    ("label.textAlign", "center"),
                    ("label.textOverflow", "ellipsis"),
                    ("label.whiteSpace", "nowrap"),
                    ("quantity.color", "var(--accent)"),
                    ("quantity.fontSize", "10")));
        }

        private static AetheriaRuntimeAssetRef ResolveItemIconAsset(
            AetheriaRuntimeInventoryItem item,
            AetheriaRuntimeCatalogItem typedItem)
        {
            if (!string.IsNullOrWhiteSpace(item?.IconAsset?.AssetKey))
                return item.IconAsset;

            return AetheriaRuntimeAssets.AssetRefFromCatalogIcon(
                typedItem?.ActionBarIcon,
                $"item.{item?.ItemKey ?? ""}.icon");
        }

        private static void AddAssetProps(
            List<(string Key, string Value)> props,
            string prefix,
            AetheriaRuntimeAssetRef asset)
        {
            if (props == null || string.IsNullOrWhiteSpace(prefix) || asset == null)
                return;

            props.Add(($"{prefix}AssetKey", asset.AssetKey ?? ""));
            props.Add(($"{prefix}AssetKind", asset.Kind ?? ""));
            props.Add(($"{prefix}AssetUri", asset.Uri ?? ""));
            props.Add(($"{prefix}AssetTransport", asset.Transport ?? ""));
            props.Add(($"{prefix}AssetMimeType", asset.MimeType ?? ""));
            props.Add(($"{prefix}AssetContentHash", asset.ContentHash ?? ""));
        }

        private static string ResolveViewTitle(
            AetheriaRuntimeInventoryPanelSurfaceRequest request,
            AetheriaRuntimeInventoryDocument displayedInventory)
        {
            if (!string.IsNullOrWhiteSpace(request?.ViewTitle))
                return request.ViewTitle;

            if (!string.IsNullOrWhiteSpace(displayedInventory?.EntityKey))
                return displayedInventory.EntityKey;

            return "Inventory";
        }

        private static string ResolveEntityName(
            AetheriaRuntimeStationRefitDocument stationRefit,
            AetheriaRuntimeInventoryPanelSurfaceRequest request)
        {
            var entityKey = string.IsNullOrWhiteSpace(request?.DisplayedEntityKey)
                ? request?.DisplayedCargoEntityKey ?? ""
                : request.DisplayedEntityKey;
            var entity = (stationRefit?.AvailableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
                .FirstOrDefault(option => string.Equals(option?.EntityKey ?? "", entityKey, StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(entity?.DisplayName) ? entityKey : entity.DisplayName;
        }

        private static bool IsCurrentEntity(
            AetheriaRuntimeCurrentEntityDocument currentEntity,
            AetheriaRuntimeInventoryPanelSurfaceRequest request)
        {
            return currentEntity != null &&
                   !string.IsNullOrWhiteSpace(request?.DisplayedEntityKey) &&
                   string.Equals(currentEntity.EntityKey ?? "", request.DisplayedEntityKey, StringComparison.Ordinal);
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
            var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return formatted.Contains(separator)
                ? formatted.TrimEnd('0').TrimEnd(Convert.ToChar(separator))
                : formatted;
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            IReadOnlyDictionary<string, string> layout,
            IReadOnlyDictionary<string, string> style,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title ?? "") }, children, layout, style);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label ?? ""), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(
                id,
                "control.button",
                new[] { ("label", label ?? ""), ("command", command ?? "") },
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                Layout(("minWidth", "96"), ("minHeight", "42")),
                Style());
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            IReadOnlyDictionary<string, string> layout,
            IReadOnlyDictionary<string, string> style,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children, layout, style);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, kind, props, children, Style(), Style());
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children,
            IReadOnlyDictionary<string, string> layout,
            IReadOnlyDictionary<string, string> style)
        {
            return Node(id, kind, props, children, Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(), layout, style);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children,
            IReadOnlyList<AetheriaRuntimeEmbeddedDocumentSlot> embeddedDocuments)
        {
            return Node(id, kind, props, children, embeddedDocuments, Style(), Style());
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children,
            IReadOnlyList<AetheriaRuntimeEmbeddedDocumentSlot> embeddedDocuments,
            IReadOnlyDictionary<string, string> layout,
            IReadOnlyDictionary<string, string> style)
        {
            var propMap = (props ?? Array.Empty<(string Key, string Value)>())
                .ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal);
            return new AetheriaRuntimeSurfaceComponent(
                id ?? "",
                kind ?? "",
                propMap,
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(propMap),
                embeddedDocuments ?? Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                layout ?? Style(),
                style ?? Style());
        }

        private static IReadOnlyDictionary<string, string> Layout(params (string Key, string Value)[] values)
        {
            return Map(values);
        }

        private static IReadOnlyDictionary<string, string> Style(params (string Key, string Value)[] values)
        {
            return Map(values);
        }

        private static IReadOnlyDictionary<string, string> PanelStyle()
        {
            return Style(
                ("background", "rgba(7, 25, 24, 0.92)"),
                ("borderWidth", "1"),
                ("borderStyle", "solid"),
                ("borderColor", "var(--line)"),
                ("borderRadius", "6"));
        }

        private static IReadOnlyDictionary<string, string> Map(params (string Key, string Value)[] values)
        {
            return (values ?? Array.Empty<(string Key, string Value)>())
                .ToDictionary(value => value.Key, value => value.Value ?? "", StringComparer.Ordinal);
        }

        private static string SafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            return new string(value
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray()).Trim('-');
        }
    }
}
