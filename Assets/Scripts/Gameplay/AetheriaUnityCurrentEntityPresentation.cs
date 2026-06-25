/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
using UniRx;
using UnityEngine;

public sealed class AetheriaUnityCurrentEntityPresentation
{
    private readonly List<IDisposable> _shipSubscriptions = new List<IDisposable>();
    private readonly List<IDisposable> _targetSubscriptions = new List<IDisposable>();
    private float _hitMarkerTime;

    public ZoneRenderer ZoneRenderer { get; set; }
    public CinemachineVirtualCamera DockCamera { get; set; }
    public CinemachineVirtualCamera FollowCamera { get; set; }
    public MenuPanel Menu { get; set; }
    public TradeMenu TradeMenu { get; set; }
    public CanvasGroup GameplayUI { get; set; }
    public InventoryPanel ShipPanel { get; set; }
    public InventoryPanel TargetShipPanel { get; set; }
    public SchematicDisplay SchematicDisplay { get; set; }
    public SchematicDisplay TargetSchematicDisplay { get; set; }
    public ImageTargetPresentation TargetImages { get; set; }
    public AetheriaRuntimeCatalogSnapshot RuntimeCatalog { get; set; }
    public PlaceUIElementWorldspace[] Crosshairs { get; set; } = Array.Empty<PlaceUIElementWorldspace>();
    public Prototype LockIndicator { get; set; }
    public GameObject HitMarker { get; set; }
    public float HitMarkerDuration { get; set; }

    public (HardpointData[] hardpoints, Transform[] barrels, PlaceUIElementWorldspace crosshair)[] ArticulationGroups { get; private set; } =
        Array.Empty<(HardpointData[] hardpoints, Transform[] barrels, PlaceUIElementWorldspace crosshair)>();

    public (LockWeapon targetLock, PlaceUIElementWorldspace indicator, Rotate spin)[] LockingIndicators { get; private set; } =
        Array.Empty<(LockWeapon targetLock, PlaceUIElementWorldspace indicator, Rotate spin)>();

    public void BindUndocked(
        Entity entity,
        EntityInstance entityInstance,
        IReadOnlyList<AetheriaUnityActionBarBinding> actionBarBindings,
        Action enablePlayerInput,
        Action<MusicType> playMusic,
        Action<Entity> updateCurrentEntity,
        Action<Entity> updateTargetPanel,
        Action reconcileVisibleTargetIndicators,
        Action<IReadOnlyList<AetheriaUnityActionBarBinding>> applyActionBarBindings)
    {
        if (entity == null || entityInstance == null)
            return;

        updateCurrentEntity?.Invoke(entity);
        ZoneRenderer.PerspectiveEntity = entity;

        Menu.gameObject.SetActive(false);
        DockCamera.enabled = false;
        FollowCamera.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        enablePlayerInput?.Invoke();
        GameplayUI.gameObject.SetActive(true);
        ShipPanel.Display(entity, true);
        SchematicDisplay.ShowShip(entity);

        FollowCamera.LookAt = entityInstance.LookAtPoint;
        FollowCamera.Follow = entityInstance.transform;
        ArticulationGroups = entity.Equipment
            .Where(HasArticulatedWeaponBehavior)
            .GroupBy(item => entityInstance
                .GetBarrel(entity.Hardpoints[item.Position.x, item.Position.y])
                .GetComponentInParent<ArticulationPoint>()?.Group ?? -1)
            .Select((group, index) =>
            {
                return (
                    group.Select(item => entity.Hardpoints[item.Position.x, item.Position.y]).ToArray(),
                    group.Select(item => entityInstance.GetBarrel(entity.Hardpoints[item.Position.x, item.Position.y])).ToArray(),
                    Crosshairs[index]
                );
            }).ToArray();

        foreach (var crosshair in Crosshairs)
            crosshair.gameObject.SetActive(false);
        foreach (var group in ArticulationGroups)
            group.crosshair.gameObject.SetActive(true);

        _shipSubscriptions.Add(entity.TargetedByCount.Subscribe(count =>
        {
            playMusic?.Invoke(count > 0 ? MusicType.Combat : MusicType.Overworld);
        }));

        _shipSubscriptions.Add(entity.Target.Subscribe(target =>
        {
            ClearTargetSubscriptions();
            updateTargetPanel?.Invoke(target);
            ConfigureTargetShieldPresentation(target);
            if (target != null)
            {
                _targetSubscriptions.Add(target.IncomingHit.Where(e => e == entity).Subscribe(_ =>
                {
                    HitMarker.SetActive(true);
                    _hitMarkerTime = HitMarkerDuration;
                }));
            }
        }));

        reconcileVisibleTargetIndicators?.Invoke();

        LockingIndicators = entity.GetBehaviors<LockWeapon>()
            .Select(x =>
            {
                var indicator = LockIndicator.Instantiate<PlaceUIElementWorldspace>();
                return (x, indicator, indicator.GetComponent<Rotate>());
            }).ToArray();

        applyActionBarBindings?.Invoke(actionBarBindings ?? Array.Empty<AetheriaUnityActionBarBinding>());
    }

    public bool HasArticulatedWeaponBehavior(EquippedItem item)
    {
        return AetheriaUnityWeaponPresentationPolicy.HasArticulatedWeaponBehavior(RuntimeCatalog, item);
    }

    public void BindDocked(
        Entity entity,
        AetheriaRuntimeCurrentDockingDocument docking,
        AetheriaRuntimeZoneRenderDocument zoneRender)
    {
        if (entity == null)
            return;

        TradeMenu.Inventory = entity.CargoBays.First();
        ZoneRenderer.PerspectiveEntity = entity;
        DockCamera.enabled = true;
        FollowCamera.enabled = false;
        var orbital = entity as OrbitalEntity;
        if (orbital == null || !ZoneRenderer.TryGetEntityInstance(orbital, out var orbitalInstance))
        {
            Debug.LogError($"Attempted to dock at entity {entity.Name}, but ZoneRenderer has no daemon-indexed instance.");
            return;
        }

        DockCamera.Follow = orbitalInstance.transform;
        var parentOrbitPlanetBodyKey = ResolveDockParentBodyKey(orbital, docking, zoneRender);
        if (ZoneRenderer.TryGetBodyView(parentOrbitPlanetBodyKey, out var parentBodyView))
            DockCamera.LookAt = parentBodyView.Body.transform;
        else DockCamera.LookAt = ZoneRenderer.ZoneRoot;
        Menu.ShowTab(MenuTab.Inventory);
    }

    private static string ResolveDockParentBodyKey(
        OrbitalEntity orbital,
        AetheriaRuntimeCurrentDockingDocument docking,
        AetheriaRuntimeZoneRenderDocument zoneRender)
    {
        if (orbital == null)
            return "";

        var bodyPoses = zoneRender?.BodyPoses ?? Array.Empty<AetheriaRuntimeZoneRenderBodyPose>();
        var parentOrbitKey = bodyPoses
            .FirstOrDefault(body => body != null &&
                                    string.Equals(body.OrbitKey ?? "", orbital.OrbitKey, StringComparison.Ordinal))
            ?.ParentOrbitKey ?? docking?.DockParentParentOrbitKey ?? "";
        return bodyPoses
            .FirstOrDefault(body => body != null &&
                                    string.Equals(body.OrbitKey ?? "", parentOrbitKey, StringComparison.Ordinal))
            ?.BodyKey ?? docking?.DockParentParentBodyKey ?? "";
    }

    public void ClearBinding()
    {
        foreach (var (_, indicator, _) in LockingIndicators)
            indicator.GetComponent<Prototype>().ReturnToPool();
        LockingIndicators = Array.Empty<(LockWeapon targetLock, PlaceUIElementWorldspace indicator, Rotate spin)>();
        ArticulationGroups = Array.Empty<(HardpointData[] hardpoints, Transform[] barrels, PlaceUIElementWorldspace crosshair)>();

        foreach (var subscription in _shipSubscriptions)
            subscription.Dispose();
        _shipSubscriptions.Clear();
        ClearTargetSubscriptions();
    }

    public void Tick(float deltaTime)
    {
        _hitMarkerTime -= deltaTime;
        if (HitMarker.activeSelf && _hitMarkerTime < 0)
            HitMarker.SetActive(false);
    }

    private void ClearTargetSubscriptions()
    {
        foreach (var subscription in _targetSubscriptions)
            subscription.Dispose();
        _targetSubscriptions.Clear();
    }

    private void ConfigureTargetShieldPresentation(Entity target)
    {
        if (target == null || TargetImages == null)
            return;

        if (target.Shield != null)
        {
            TargetImages.TargetShieldsBackground.color = new Color(TargetImages.ShieldColor.r, TargetImages.ShieldColor.g, TargetImages.ShieldColor.b, .4f);
            TargetImages.TargetShieldsIcon.color = TargetImages.ShieldColor;
            TargetImages.TargetShieldsIcon.sprite = TargetImages.ShieldIcon;
        }
        else
        {
            TargetImages.TargetShieldsBackground.color = new Color(TargetImages.NoShieldColor.r, TargetImages.NoShieldColor.g, TargetImages.NoShieldColor.b, .4f);
            TargetImages.TargetShieldsIcon.color = TargetImages.NoShieldColor;
            TargetImages.TargetShieldsIcon.sprite = TargetImages.NoShieldIcon;
        }
    }
}

public sealed class ImageTargetPresentation
{
    public UnityEngine.UI.Image TargetShieldsBackground { get; set; }
    public UnityEngine.UI.Image TargetShieldsIcon { get; set; }
    public Color ShieldColor { get; set; }
    public Color NoShieldColor { get; set; }
    public Sprite ShieldIcon { get; set; }
    public Sprite NoShieldIcon { get; set; }
}
