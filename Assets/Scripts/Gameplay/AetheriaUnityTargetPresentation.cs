/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using UnityEngine.UI;
using float3 = Unity.Mathematics.float3;

public sealed class AetheriaUnityTargetPresentation
{
    private readonly Dictionary<Entity, VisibleTargetIndicator> _visibleHostileIndicators =
        new Dictionary<Entity, VisibleTargetIndicator>();
    private readonly Dictionary<Entity, VisibleTargetIndicator> _visibleFriendlyIndicators =
        new Dictionary<Entity, VisibleTargetIndicator>();
    private readonly HashSet<Entity> _observedHostileIndicatorTargets = new HashSet<Entity>();
    private readonly HashSet<Entity> _observedFriendlyIndicatorTargets = new HashSet<Entity>();
    private readonly List<Entity> _staleIndicatorTargets = new List<Entity>();

    public ZoneRenderer ZoneRenderer { get; set; }
    public Prototype HostileTargetIndicator { get; set; }
    public Prototype FriendlyTargetIndicator { get; set; }
    public PlaceUIElementWorldspace ViewDot { get; set; }
    public PlaceUIElementWorldspace TargetIndicator { get; set; }
    public Image TargetHitpointsFill { get; set; }
    public Image TargetVisibilityFill { get; set; }
    public Image VisibilityToTargetFill { get; set; }
    public Image TargetShieldsFill { get; set; }
    public AetheriaRuntimeCatalogSnapshot RuntimeCatalog { get; set; }
    public Func<AetheriaClient> ResolveClient { get; set; }
    public Func<int, Entity> ResolveEntity { get; set; }
    public Func<Entity, Entity> ResolveTarget { get; set; }
    public Func<Entity, Entity, float> ResolveInfoGathered { get; set; }
    public Func<Entity, Entity, bool> ResolveHostileContact { get; set; }
    public Func<Entity, double, bool, AetheriaRuntimeZoneContactRow[]> ResolveVisibleContacts { get; set; }

    public Entity Tick(Entity currentEntity, float time)
    {
        var observedTarget = ResolveTarget?.Invoke(currentEntity);
        ReconcileVisibleTargetIndicators(currentEntity);
        UpdateVisibleIndicatorPresentation(
            _visibleHostileIndicators,
            currentEntity,
            observedTarget,
            time,
            blinkSpottedTargets: true);
        UpdateVisibleIndicatorPresentation(
            _visibleFriendlyIndicators,
            currentEntity,
            observedTarget,
            time,
            blinkSpottedTargets: false);
        UpdateTargetStatus(currentEntity, observedTarget);
        return observedTarget;
    }

    public void ReconcileVisibleTargetIndicators(Entity currentEntity)
    {
        _observedHostileIndicatorTargets.Clear();
        _observedFriendlyIndicatorTargets.Clear();

        var renderSettings = ZoneRenderer?.RenderSettings;
        if (currentEntity != null && renderSettings != null)
        {
            var contacts = ResolveVisibleContacts?.Invoke(
                currentEntity,
                renderSettings.Value.TargetDetectionInfoThreshold,
                true) ?? Array.Empty<AetheriaRuntimeZoneContactRow>();

            foreach (var contact in contacts)
            {
                if (ResolveEntity?.Invoke(contact.TargetEntityIndex) is not { } target ||
                    ReferenceEquals(target, currentEntity))
                {
                    continue;
                }

                if (contact.Hostile)
                    _observedHostileIndicatorTargets.Add(target);
                else
                    _observedFriendlyIndicatorTargets.Add(target);
            }
        }

        ReconcileVisibleTargetIndicators(
            _visibleHostileIndicators,
            _observedHostileIndicatorTargets,
            HostileTargetIndicator);
        ReconcileVisibleTargetIndicators(
            _visibleFriendlyIndicators,
            _observedFriendlyIndicatorTargets,
            FriendlyTargetIndicator);
    }

    public void ClearIndicators()
    {
        ClearIndicators(_visibleHostileIndicators);
        ClearIndicators(_visibleFriendlyIndicators);
    }

    public void UpdateTargetIndicators(
        Entity currentEntity,
        bool isCurrentEntityObservedUndocked,
        AetheriaUnityCurrentEntityPresentation currentEntityPresentation)
    {
        if (!isCurrentEntityObservedUndocked ||
            currentEntity == null ||
            ZoneRenderer == null ||
            ViewDot == null ||
            !ZoneRenderer.TryGetEntityInstance(currentEntity, out var entityInstance))
        {
            return;
        }

        var observedTarget = ResolveTarget?.Invoke(currentEntity);
        ViewDot.Target = entityInstance.LookAtPoint.position;
        if (observedTarget != null && TargetIndicator != null)
            TargetIndicator.Target = observedTarget.Position;

        var distance = CultMath.math.length(AetheriaMath.ToCult((float3)ViewDot.Target) - currentEntity.CultPosition);
        foreach (var (_, barrels, crosshair) in currentEntityPresentation.ArticulationGroups)
        {
            var averagePosition = Vector3.zero;
            foreach (var barrel in barrels)
                averagePosition += barrel.position + barrel.forward * distance;
            averagePosition /= barrels.Length;
            crosshair.Target = averagePosition;
        }

        foreach (var (targetLock, indicator, spin) in currentEntityPresentation.LockingIndicators)
        {
            var showLockingIndicator = targetLock.Lock > .01f &&
                                       observedTarget != null &&
                                       ResolveHostileContact?.Invoke(currentEntity, observedTarget) == true;
            indicator.gameObject.SetActive(showLockingIndicator);
            if (!showLockingIndicator)
                continue;

            var renderSettings = ZoneRenderer.RenderSettings;
            indicator.Target = observedTarget.Position;
            indicator.NoiseAmplitude = (float)renderSettings.ResolveLockIndicatorNoiseAmplitude(targetLock.Lock);
            indicator.NoiseFrequency = (float)renderSettings.ResolveLockIndicatorNoiseFrequency(targetLock.Lock);
            spin.Speed = (float)renderSettings.ResolveLockSpinSpeed(targetLock.Lock);
        }
    }

    private void UpdateVisibleIndicatorPresentation(
        Dictionary<Entity, VisibleTargetIndicator> indicators,
        Entity currentEntity,
        Entity observedTarget,
        float time,
        bool blinkSpottedTargets)
    {
        var renderSettings = ZoneRenderer?.RenderSettings;
        if (renderSettings == null)
            return;

        foreach (var indicator in indicators)
        {
            indicator.Value.gameObject.SetActive(indicator.Key != observedTarget);
            indicator.Value.Place.Target = indicator.Key.Position;
            if (!indicator.Key.Active)
            {
                indicator.Value.Fill.enabled = false;
                continue;
            }

            var infoGathered = ResolveInfoGathered?.Invoke(indicator.Key, currentEntity) ?? 0f;
            indicator.Value.Fill.fillAmount = (float)renderSettings.Value.NormalizeDetectionProgress(infoGathered);
            indicator.Value.Fill.enabled = !blinkSpottedTargets ||
                                           renderSettings.Value.ResolveTargetSpottedFillEnabled(infoGathered, time);
        }
    }

    private void UpdateTargetStatus(Entity currentEntity, Entity target)
    {
        if (currentEntity == null || target == null || ZoneRenderer?.RenderSettings == null)
            return;

        var renderSettings = ZoneRenderer.RenderSettings;
        var targetInfoGathered = ResolveInfoGathered?.Invoke(currentEntity, target) ?? 0f;
        var visibilityToTarget = ResolveInfoGathered?.Invoke(target, currentEntity) ?? 0f;
        TargetVisibilityFill.fillAmount = (float)renderSettings.NormalizeTargetVisibilityFill(targetInfoGathered);
        VisibilityToTargetFill.fillAmount = (float)renderSettings.NormalizeVisibilityToTargetFill(visibilityToTarget);
        var targetHull = RuntimeCatalog?.FindItem(target.Hull?.ItemKey ?? "");
        var targetMaxDurability = targetHull?.Durability > 0
            ? (float)targetHull.Durability
            : Math.Max(target.Hull.Durability, 1f);
        TargetHitpointsFill.fillAmount =
            (float)renderSettings.NormalizeTargetStatusFill(target.Hull.Durability / targetMaxDurability);
        TargetShieldsFill.fillAmount = target.Shield == null
            ? 0
            : (float)renderSettings.NormalizeTargetStatusFill(target.Shield.Progress);
    }

    private void ReconcileVisibleTargetIndicators(
        Dictionary<Entity, VisibleTargetIndicator> indicators,
        HashSet<Entity> desiredTargets,
        Prototype prototype)
    {
        if (prototype == null)
            return;

        _staleIndicatorTargets.Clear();
        foreach (var indicator in indicators)
        {
            if (!desiredTargets.Contains(indicator.Key))
                _staleIndicatorTargets.Add(indicator.Key);
        }

        foreach (var staleTarget in _staleIndicatorTargets)
        {
            indicators[staleTarget].GetComponent<Prototype>().ReturnToPool();
            indicators.Remove(staleTarget);
        }

        foreach (var target in desiredTargets)
        {
            if (!indicators.ContainsKey(target))
                indicators.Add(target, prototype.Instantiate<VisibleTargetIndicator>());
        }
    }

    private static void ClearIndicators(Dictionary<Entity, VisibleTargetIndicator> indicators)
    {
        foreach (var indicator in indicators.Values)
            indicator.GetComponent<Prototype>().ReturnToPool();
        indicators.Clear();
    }
}
