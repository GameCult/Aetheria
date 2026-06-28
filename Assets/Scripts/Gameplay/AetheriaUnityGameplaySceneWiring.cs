/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public sealed class AetheriaUnityGameplaySceneWiring
{
    public ZoneRenderer ZoneRenderer { get; set; }
    public CinemachineVirtualCamera DockCamera { get; set; }
    public CinemachineVirtualCamera FollowCamera { get; set; }
    public MenuPanel Menu { get; set; }
    public TradeMenu TradeMenu { get; set; }
    public CanvasGroup GameplayUI { get; set; }
    public InventoryMenu Inventory { get; set; }
    public InventoryPanel ShipPanel { get; set; }
    public InventoryPanel TargetShipPanel { get; set; }
    public SchematicDisplay SchematicDisplay { get; set; }
    public SchematicDisplay TargetSchematicDisplay { get; set; }
    public PlaceUIElementWorldspace[] Crosshairs { get; set; } = Array.Empty<PlaceUIElementWorldspace>();
    public Prototype LockIndicator { get; set; }
    public GameObject HitMarker { get; set; }
    public float HitMarkerDuration { get; set; }
    public Image TargetShieldsBackground { get; set; }
    public Image TargetShieldsIcon { get; set; }
    public Color ShieldColor { get; set; }
    public Color NoShieldColor { get; set; }
    public Sprite ShieldIcon { get; set; }
    public Sprite NoShieldIcon { get; set; }
    public Prototype HostileTargetIndicator { get; set; }
    public Prototype FriendlyTargetIndicator { get; set; }
    public PlaceUIElementWorldspace ViewDot { get; set; }
    public PlaceUIElementWorldspace TargetIndicator { get; set; }
    public Image TargetHitpointsFill { get; set; }
    public Image TargetVisibilityFill { get; set; }
    public Image VisibilityToTargetFill { get; set; }
    public Image TargetShieldsFill { get; set; }
    public MainMenu MainMenu { get; set; }

    public void ConfigureCurrentEntityPresentation(
        AetheriaUnityCurrentEntityPresentation presentation,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog)
    {
        if (presentation == null)
            return;

        presentation.ZoneRenderer = ZoneRenderer;
        presentation.RuntimeCatalog = runtimeCatalog;
        presentation.DockCamera = DockCamera;
        presentation.FollowCamera = FollowCamera;
        presentation.Menu = Menu;
        presentation.TradeMenu = TradeMenu;
        presentation.GameplayUI = GameplayUI;
        presentation.ShipPanel = ShipPanel;
        presentation.TargetShipPanel = TargetShipPanel;
        presentation.SchematicDisplay = SchematicDisplay;
        presentation.TargetSchematicDisplay = TargetSchematicDisplay;
        presentation.Crosshairs = Crosshairs;
        presentation.LockIndicator = LockIndicator;
        presentation.HitMarker = HitMarker;
        presentation.HitMarkerDuration = HitMarkerDuration;
        presentation.TargetImages = new ImageTargetPresentation
        {
            TargetShieldsBackground = TargetShieldsBackground,
            TargetShieldsIcon = TargetShieldsIcon,
            ShieldColor = ShieldColor,
            NoShieldColor = NoShieldColor,
            ShieldIcon = ShieldIcon,
            NoShieldIcon = NoShieldIcon
        };
    }

    public void ConfigureTargetPresentation(
        AetheriaUnityTargetPresentation presentation,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        AetheriaUnityObservedEntityIndex observedEntityIndex,
        AetheriaUnityObservedTargetQuery observedTargetQuery,
        Func<AetheriaClient> resolveClient)
    {
        if (presentation == null)
            return;

        presentation.ZoneRenderer = ZoneRenderer;
        presentation.RuntimeCatalog = runtimeCatalog;
        presentation.HostileTargetIndicator = HostileTargetIndicator;
        presentation.FriendlyTargetIndicator = FriendlyTargetIndicator;
        presentation.ViewDot = ViewDot;
        presentation.TargetIndicator = TargetIndicator;
        presentation.TargetHitpointsFill = TargetHitpointsFill;
        presentation.TargetVisibilityFill = TargetVisibilityFill;
        presentation.VisibilityToTargetFill = VisibilityToTargetFill;
        presentation.TargetShieldsFill = TargetShieldsFill;
        presentation.ResolveClient = resolveClient;
        presentation.ResolveEntity = daemonEntityIndex =>
            observedEntityIndex != null &&
            observedEntityIndex.TryResolveEntityByDaemonIndex(daemonEntityIndex, out var entity)
                ? entity
                : null;
        presentation.ResolveTarget = observedTargetQuery.GetObservedTarget;
        presentation.ResolveInfoGathered = observedTargetQuery.GetObservedInfoGathered;
        presentation.ResolveHostileContact = observedTargetQuery.IsObservedHostileContact;
        presentation.ResolveVisibleContacts = observedTargetQuery.GetObservedVisibleContacts;
    }

    public void ConfigureInventoryDragSession(AetheriaUnityDragSession dragSession)
    {
        Inventory?.SetDragSession(dragSession);
        ShipPanel?.SetDragSession(dragSession);
        TargetShipPanel?.SetDragSession(dragSession);
    }

    public void ConfigureActionBarPresentation(
        AetheriaUnityActionBarPresentation actionBarPresentation,
        AetheriaUnityGameplayInputShell gameplayInputShell,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        GameSettings settings,
        Func<Entity> resolveCurrentEntity,
        Func<AetheriaClient> resolveActionBarClient)
    {
        actionBarPresentation?.Bind(
            gameplayInputShell?.ActionBarSlots ?? Array.Empty<ActionBarSlot>(),
            runtimeCatalog,
            settings,
            resolveCurrentEntity,
            resolveActionBarClient);
        Inventory?.SetActionBarPresentation(actionBarPresentation);
    }

    public void ConfigureRuntimeInputScreenShell(AetheriaUnityMenuShell menuShell)
    {
        MainMenu?.SetRuntimeInputScreenShell(menuShell.CanOpenRuntimeInputScreen, menuShell.ShowRuntimeInputScreen);
    }

    public void ConfigureObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)
    {
        Inventory?.SetObservedEntityIndex(observedEntityIndex);
        ShipPanel?.SetObservedEntityIndex(observedEntityIndex);
        TargetShipPanel?.SetObservedEntityIndex(observedEntityIndex);
        foreach (var localMenu in UnityEngine.Object.FindObjectsByType<LocalMenu>(FindObjectsSortMode.None))
            localMenu.SetObservedEntityIndex(observedEntityIndex);
    }
}
