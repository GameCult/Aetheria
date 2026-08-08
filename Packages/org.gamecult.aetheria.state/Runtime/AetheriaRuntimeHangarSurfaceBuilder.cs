using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Aetheria.State.Documents;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeHangarCommands
    {
        public const string SurfaceId = "aetheria.hangar";
        public const string SelectShip = "aetheria.hangar.select_ship";
        public const string SelectTerminus = "aetheria.hangar.select_mode.terminus";
        public const string SelectStarbridge = "aetheria.hangar.select_mode.starbridge";
        public const string SelectArena = "aetheria.hangar.select_mode.arena";
        public const string EditLoadout = "aetheria.hangar.edit_loadout";
        public const string EquipItem = "aetheria.hangar.loadout.equip";
        public const string RemoveItem = "aetheria.hangar.loadout.remove";
        public const string Launch = "aetheria.hangar.launch";
        public const string Continue = "aetheria.hangar.continue";
    }

    public static class AetheriaRuntimeHangarSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaHangarState hangar,
            string selectedShipId,
            string selectedMode,
            string updatedAtUtc,
            long version = 1,
            AetheriaLoadoutTemplate? loadout = null,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            if (hangar == null) throw new ArgumentNullException(nameof(hangar));
            var ships = hangar.Ships ?? Array.Empty<AetheriaHangarShip>();
            var selected = ships.FirstOrDefault(ship => string.Equals(ship.ShipId, selectedShipId, StringComparison.Ordinal))
                ?? ships.FirstOrDefault();
            var mode = AetheriaGameModes.IsKnown(selectedMode) ? selectedMode : AetheriaGameModes.Terminus;
            var canLaunch = selected != null && loadout != null && string.Equals(selected.Status, AetheriaHangarShipStatuses.Available, StringComparison.Ordinal);
            var canContinue = selected != null && string.Equals(selected.Status, AetheriaHangarShipStatuses.Deployed, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(selected.ActiveDeploymentId);
            var equipment = loadout?.RootEntity?.Equipment ?? Array.Empty<AetheriaLoadoutItemSlot>();
            var inventory = hangar.Inventory ?? Array.Empty<AetheriaHangarItemStack>();

            var root = Component(
                "aetheria.hangar.root",
                "surface",
                Props(("title", "HANGAR"), ("selectedMode", mode), ("hangarRevision", hangar.Revision.ToString(CultureInfo.InvariantCulture))),
                new[]
                {
                    Panel("aetheria.hangar.ship_summary", "SHIP", selected == null
                        ? new[] { Text("aetheria.hangar.ship.none", "No ship selected") }
                        : new[]
                        {
                            Metric("aetheria.hangar.ship.id", "Ship", selected.ShipId),
                            Metric("aetheria.hangar.ship.hull", "Hull", selected.HullItemKey),
                            Metric("aetheria.hangar.ship.status", "Status", selected.Status),
                            Metric("aetheria.hangar.ship.loadout", "Loadout", selected.LoadoutTemplateKey)
                        }, "summary"),
                    Panel("aetheria.hangar.preview", "SHIP PREVIEW", new[]
                    {
                        Component("aetheria.hangar.preview.slot", "asset.preview", Props(
                            ("assetRole", "ship.preview"),
                            ("subjectKey", selected?.ShipId ?? "")), Array.Empty<AetheriaRuntimeSurfaceComponent>())
                    }, "preview"),
                    Panel("aetheria.hangar.fit", "FIT SUMMARY", new[]
                    {
                        Metric("aetheria.hangar.fit.hull", "Hull", selected?.HullItemKey ?? "-"),
                        Metric("aetheria.hangar.fit.template", "Template", selected?.LoadoutTemplateKey ?? "-"),
                        Metric("aetheria.hangar.fit.policy", "Authority", AetheriaModePolicies.ForMode(mode)),
                        Button("aetheria.hangar.fit.edit", "EDIT LOADOUT", AetheriaRuntimeHangarCommands.EditLoadout,
                            ("targetSurfaceId", AetheriaRuntimeInventoryPanelSurfaceBuilder.SurfaceId))
                    }, "fit"),
                    Panel("aetheria.hangar.loadout", "LOADOUT", equipment.Select((slot, index) =>
                        Button(
                            $"aetheria.hangar.loadout.item.{index}",
                            $"{ItemName(catalog, slot.Item?.ItemKey)} [{slot.Position?.X ?? 0},{slot.Position?.Y ?? 0}]  REMOVE",
                            AetheriaRuntimeHangarCommands.RemoveItem,
                            ("shipId", selected?.ShipId ?? ""),
                            ("equipmentIndex", index.ToString(CultureInfo.InvariantCulture)),
                            ("expectedHangarRevision", hangar.Revision.ToString(CultureInfo.InvariantCulture)))).ToArray(), "loadout"),
                    Panel("aetheria.hangar.inventory", "HANGAR INVENTORY", inventory.Select(stack =>
                        Button(
                            "aetheria.hangar.inventory." + StableToken(stack.ItemKey),
                            $"{ItemName(catalog, stack.ItemKey)} x{stack.Quantity}  EQUIP",
                            AetheriaRuntimeHangarCommands.EquipItem,
                            ("shipId", selected?.ShipId ?? ""),
                            ("itemKey", stack.ItemKey),
                            ("expectedHangarRevision", hangar.Revision.ToString(CultureInfo.InvariantCulture)))).ToArray(), "inventory"),
                    Component("aetheria.hangar.bays", "row", Props(("label", "OWNED SHIPS")),
                        ships.Select(ship => Button(
                            "aetheria.hangar.bay." + StableToken(ship.ShipId),
                            ship.ShipId,
                            AetheriaRuntimeHangarCommands.SelectShip,
                            ("shipId", ship.ShipId),
                            ("selected", string.Equals(ship.ShipId, selected?.ShipId, StringComparison.Ordinal) ? "true" : "false"),
                            ("status", ship.Status))).ToArray(),
                        Layout(("gridArea", "bays"), ("display", "flex"), ("overflowX", "auto"), ("gap", "8"))),
                    Component("aetheria.hangar.launcher", "row", Props(), new[]
                    {
                        Button("aetheria.hangar.mode.terminus", "TERMINUS", AetheriaRuntimeHangarCommands.SelectTerminus,
                            ("selected", (mode == AetheriaGameModes.Terminus).ToString().ToLowerInvariant())),
                        Button("aetheria.hangar.mode.starbridge", "STARBRIDGE", AetheriaRuntimeHangarCommands.SelectStarbridge,
                            ("selected", (mode == AetheriaGameModes.Starbridge).ToString().ToLowerInvariant())),
                        Button("aetheria.hangar.mode.arena", "ARENA", AetheriaRuntimeHangarCommands.SelectArena,
                            ("selected", (mode == AetheriaGameModes.Arena).ToString().ToLowerInvariant())),
                        Button("aetheria.hangar.launch", "LAUNCH", AetheriaRuntimeHangarCommands.Launch,
                            ("enabled", canLaunch.ToString().ToLowerInvariant()),
                            ("shipId", selected?.ShipId ?? ""),
                            ("mode", mode),
                            ("expectedHangarRevision", hangar.Revision.ToString(CultureInfo.InvariantCulture))),
                        Button("aetheria.hangar.continue", "CONTINUE", AetheriaRuntimeHangarCommands.Continue,
                            ("enabled", canContinue.ToString().ToLowerInvariant()),
                            ("shipId", selected?.ShipId ?? ""),
                            ("deploymentId", selected?.ActiveDeploymentId ?? ""))
                    }, Layout(("gridArea", "launch"), ("display", "flex"), ("justifyContent", "flex-end"), ("gap", "8")))
                },
                Layout(
                    ("display", "grid"),
                    ("gridTemplateColumns", "minmax(260px, 0.75fr) minmax(480px, 1.6fr) minmax(280px, 0.8fr)"),
                    ("gridTemplateRows", "auto minmax(300px, 1fr) minmax(180px, 0.65fr) auto"),
                    ("gridTemplateAreas", "\"launch launch launch\" \"summary preview fit\" \"inventory loadout loadout\" \"bays bays bays\""),
                    ("gap", "10"),
                    ("height", "100%")),
                Style(("background", "#070b0d"), ("color", "#d8eef2")));

            return new AetheriaRuntimeSurfaceDocument(
                "aetheria",
                "game.hangar",
                "Aetheria Hangar",
                version,
                updatedAtUtc ?? "",
                new AetheriaRuntimeSurfaceTree(AetheriaRuntimeHangarCommands.SurfaceId, root, Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                new[]
                {
                    Command(AetheriaRuntimeHangarCommands.SelectShip, "Select Ship"),
                    Command(AetheriaRuntimeHangarCommands.SelectTerminus, "Select Terminus"),
                    Command(AetheriaRuntimeHangarCommands.SelectStarbridge, "Select Starbridge"),
                    Command(AetheriaRuntimeHangarCommands.SelectArena, "Select Arena"),
                    Command(AetheriaRuntimeHangarCommands.EditLoadout, "Edit Loadout"),
                    Command(AetheriaRuntimeHangarCommands.EquipItem, "Equip Item"),
                    Command(AetheriaRuntimeHangarCommands.RemoveItem, "Remove Item"),
                    Command(AetheriaRuntimeHangarCommands.Launch, "Launch"),
                    Command(AetheriaRuntimeHangarCommands.Continue, "Continue")
                });
        }

        private static string ItemName(AetheriaRuntimeCatalogSnapshot? catalog, string? itemKey) =>
            catalog?.FindItem(itemKey ?? "")?.Name ?? (string.IsNullOrWhiteSpace(itemKey) ? "Unknown" : itemKey);

        private static AetheriaRuntimeSurfaceComponent Panel(string id, string title, IReadOnlyList<AetheriaRuntimeSurfaceComponent> children, string area) =>
            Component(id, "panel", Props(("title", title)), children, Layout(("gridArea", area)), Style(("background", "#11181c"), ("border", "1px solid #60737a"), ("padding", "12")));

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value) =>
            Component(id, "metric", Props(("label", label), ("value", value)), Array.Empty<AetheriaRuntimeSurfaceComponent>());

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value) =>
            Component(id, "text", Props(("value", value)), Array.Empty<AetheriaRuntimeSurfaceComponent>());

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command, params (string Key, string Value)[] extra) =>
            Component(id, "button", Props(new[] { ("label", label), ("command", command) }.Concat(extra).ToArray()), Array.Empty<AetheriaRuntimeSurfaceComponent>());

        private static AetheriaRuntimeSurfaceCommandTemplate Command(string command, string label) =>
            new AetheriaRuntimeSurfaceCommandTemplate(command, label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport);

        private static AetheriaRuntimeSurfaceComponent Component(
            string id,
            string kind,
            IReadOnlyDictionary<string, string> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children,
            IReadOnlyDictionary<string, string>? layout = null,
            IReadOnlyDictionary<string, string>? style = null) =>
            new AetheriaRuntimeSurfaceComponent(id, kind, props, children, Array.Empty<GameCult.Mesh.CultMeshStateBindingDescriptor>(), Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(), layout, style);

        private static IReadOnlyDictionary<string, string> Props(params (string Key, string Value)[] values) =>
            values.ToDictionary(value => value.Key, value => value.Value ?? "", StringComparer.Ordinal);

        private static IReadOnlyDictionary<string, string> Layout(params (string Key, string Value)[] values) => Props(values);
        private static IReadOnlyDictionary<string, string> Style(params (string Key, string Value)[] values) => Props(values);

        private static string StableToken(string value) =>
            new string((value ?? "").Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '.').ToArray()).Trim('.');
    }
}
