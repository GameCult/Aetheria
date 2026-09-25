/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;
using Random = CultMath.Random;

// Cut 1 (docs/fire-control-cut.md): geometry -- whether a weapon bears on a point is decided here and
// nowhere else -- no prefab, transform, ArticulationPoint or renderer may influence it (R6, R7). Mount
// direction and arc are both derived per read from catalog data and the item's authored rotation; neither
// is ever stored on the entity.
// Cut 2 adds the reveal rule (R5) and the resolved targeting-system stats an entity fires with, both
// likewise derived on every read and never cached.
public static class FireControl
{
    // The planar unit vector a hardpoint's weapon points along, derived solely from the item's authored
    // rotation (R6, the same convention reaction thrusters already use) -- never from a live Unity
    // transform. This is the item-rotation path Behavior.Direction now uses unconditionally, after the
    // HardpointTransforms readback branch is deleted.
    public static float3 MountDirection(EquippedItem item)
    {
        var itemDirection = item.Entity.Direction.Rotate(item.EquippableItem.Rotation);
        return normalize(float3(itemDirection.x, 0, itemDirection.y));
    }

    // Full arc width in degrees: the per-hardpoint override when authored (nonzero), else
    // GameplaySettings.FiringArc. Zero on the hardpoint means "use the default," not "no arc."
    public static float ArcFor(EquippedItem item)
    {
        var hardpoint = item.Entity.Hardpoints[item.Position.x, item.Position.y];
        var arc = hardpoint?.FiringArc ?? 0f;
        return arc > 0 ? arc : item.ItemManager.GameplaySettings.FiringArc;
    }

    // Planar bearing test (R7: the simulation is 2D, the 3D is set dressing -- target height never enters
    // this). An arc of 360 degrees or more passes unconditionally, which is how a turret hardpoint
    // (authored FiringArc: 360, Q1) tracks all the way around without a second type.
    // Cut 5, 5.4 (Soul finding 11): a planar bearing shorter than 1e-6 -- point-blank range, target and firer
    // co-located -- returns true rather than falling through to normalize's NaN. This used to live as a
    // separate special case in Weapon.ArcAllowsFire; one bearing test now owns it, so a target at a weapon's
    // own exact position can never fail to bear regardless of caller.
    public static bool InArc(EquippedItem weapon, float3 toTarget)
    {
        var arc = ArcFor(weapon);
        if (arc >= 360f) return true;
        var planarToTarget = float3(toTarget.x, 0, toTarget.z);
        if (lengthsq(planarToTarget) < 1e-6f) return true;
        var planarTarget = normalize(planarToTarget);
        return dot(MountDirection(weapon), planarTarget) >= cos(radians(arc / 2f));
    }

    // Cut 2, R5: whether `observer` has resolved `item` (which must belong to some entity in the zone) well
    // enough to aim at it. Derived fresh from EntityInfoGathered on every call -- nothing caches a reveal
    // result, so decay drops it the moment info falls back below tier, with no clearing loop anywhere.
    //
    // Ranking: the target's own non-hull equipment, hardpoint-mounted gear first (the big obvious things),
    // then by size descending, ties broken by equipment order (OrderByDescending is a stable sort, so this
    // needs no explicit tie-break key). Item i of N reveals once info crosses
    // lerp(TargetArmorInfoThreshold, TargetGearInfoThreshold, i / max(1, N - 1)) -- the first item to reveal
    // does so at the armor tier, the last at the gear tier.
    public static bool IsRevealed(Entity observer, EquippedItem item)
    {
        var target = item.Entity;
        // Cut 5, 5.5 (Soul finding 10): a destroyed item drops out of the ranking entirely rather than merely
        // failing its own tier -- ranked.IndexOf(item) then returns -1 for it (the same "no longer on this
        // target" path below), and the survivors' tiers close up over the gap instead of leaving one.
        var ranked = target.Equipment
            .Where(e => e.Data.HardpointType != HardpointType.Hull && e.EquippableItem.Durability >= .01f)
            .OrderByDescending(e => e.Data.HardpointType != HardpointType.Tool)
            .ThenByDescending(e => e.Data.Shape.Coordinates.Length)
            .ToList();
        var index = ranked.IndexOf(item);
        if (index < 0) return false; // the hull itself, or an item that is no longer on this target

        var settings = observer.ItemManager.GameplaySettings;
        var info = observer.EntityInfoGathered.TryGetValue(target, out var gathered) ? gathered : 0f;
        var tier = lerp(settings.TargetArmorInfoThreshold, settings.TargetGearInfoThreshold,
            (float) index / max(1, ranked.Count - 1));
        return info >= tier;
    }

    // Cut 2, Q4: the stats an entity actually fires with. A working targeting system (equipped, online --
    // not destroyed, not shut down) supplies its own resolved stats; anything else falls back to the
    // authored unaided figures, which make aiming without one a last resort rather than an alternative.
    // Cut 3's roll is the only planned reader.
    public static float Accuracy(Entity entity)
    {
        var system = entity.GetBehavior<TargetingSystem>();
        return system != null && system.Item.Active.Value
            ? system.Accuracy
            : entity.ItemManager.GameplaySettings.UnaidedAccuracy;
    }

    public static float Resolution(Entity entity)
    {
        var system = entity.GetBehavior<TargetingSystem>();
        return system != null && system.Item.Active.Value ? system.Resolution : 1f;
    }

    // Cut 6d (docs/fire-control-cut.md): falls back to GameplaySettings.UnaidedPrecision, the same shape
    // Accuracy/Tracking already fall back to their own Unaided* floors. The old coin-flip rule could get away
    // with a bare 0 here (0 meant "never draw the aimed-item branch," a legitimate value under that rule); the
    // dart-throw kernel reads Precision as a grouping tightness, and 0 would ask Sigma for an infinite, division-
    // guarded group -- not the authored "spraying at the silhouette" floor the spec asks for.
    public static float Precision(Entity entity)
    {
        var system = entity.GetBehavior<TargetingSystem>();
        return system != null && system.Item.Active.Value ? system.Precision : entity.ItemManager.GameplaySettings.UnaidedPrecision;
    }

    // Cut 5, 5.1 (Soul finding 3): falls back to GameplaySettings.UnaidedTracking, exactly the shape Accuracy
    // falls back to UnaidedAccuracy. Tracking is now always positive -- Commit no longer carries a zero case.
    public static float Tracking(Entity entity)
    {
        var system = entity.GetBehavior<TargetingSystem>();
        return system != null && system.Item.Active.Value ? system.Tracking : entity.ItemManager.GameplaySettings.UnaidedTracking;
    }

    // Cut 3, the risk this map names explicitly: Combat.cs used to run its own first_order_intercept call to
    // aim, while the roll measured deviation against a separately-computed prediction -- two functions
    // claiming to answer the same question will eventually disagree, and an AI would then aim at one point and
    // be judged against another. This is the one function; CombatState (and FireControl.Fire below) both call
    // it instead of touching CultMath.first_order_intercept directly. Planar: source velocity is never fed in
    // (no shooter here leads its own motion into the shot), matching every existing call site this replaces.
    public static float3 PredictedIntercept(Weapon weapon, Entity source, Entity target)
    {
        var targetVelocity = float3(target.Velocity.x, 0, target.Velocity.y);
        return weapon.Velocity > .01f
            ? first_order_intercept(source.Position, float3.zero, weapon.Velocity, target.Position, targetVelocity)
            : target.Position;
    }

    // Cut 3, R1/R3: the targeting solution (0b table) -- pure, no draw, recomputed fresh on every call. AI
    // reads this to decide whether a shot is worth taking; the HUD reads it to draw a number; Fire below reads
    // it once, at the instant the trigger is pulled, and freezes the result into the shot (R10, Q6). Two reads
    // a tick apart may differ; nothing here or anywhere else caches one.
    // Cut 10 (docs/fire-control-cut.md): the hot path. Every AI and turret calls this per weapon per tick,
    // and the common case is a target out of range or out of arc -- so the cheap gates run first and bail,
    // exactly as they did before the diagnostics refactor. The factors are computed only for a shot that is
    // actually possible, and they come from the same functions Inspect uses: one fire-control model, shared
    // at the level of its factors rather than by routing the hot path through a diagnostic struct.
    // HitProbabilityMatchesInspect pins that the two can never disagree.
    // Cut 12.2 (docs/fire-control-cut.md): the shooter-side share of the probability -- everything Fire
    // freezes (R10), gated exactly as HitProbability gates. Nothing about the target's facing or silhouette
    // enters here; that is Silhouette's and PSpread's job, priced fresh by HitProbability below and again,
    // live, by Commit at the commit tick. 0 the moment any gate closes.
    private static float PFire(Weapon weapon, Entity source, Entity target) => PFire(weapon, source, target, out _);

    // The `out range` overload is HitProbability's own gate call -- one computation of `target.Position -
    // source.Position`, not two. HitProbability used to recompute its own copy for PSpread, which is exactly
    // the kind of split authority Cut 3 named as the risk (two places computing the same vector, free to
    // drift or to have their subtraction order flipped in only one of them).
    private static float PFire(Weapon weapon, Entity source, Entity target, out float range)
    {
        range = 0f;
        if (target == null) return 0f;

        // F8 correction (Soul's fix batch, 2026-09-24): range is computed for any non-null target, before any
        // gate -- Fire reads it through this same overload for flightTime, and flightTime must reflect the
        // real distance even when a gate (not visible, out of arc) already prices the shot at PFire 0. The
        // gates below still short-circuit the rest of the shooter-side probability in the same order as before.
        var toTarget = target.Position - source.Position;
        range = length(toTarget);

        if (!source.VisibleEntities.Contains(target)) return 0f;
        if (range < weapon.MinRange || range > weapon.Range) return 0f;
        if (weapon is LockWeapon lockWeapon && !lockWeapon.IsLocked) return 0f;
        if (!InArc(weapon.Item, toTarget)) return 0f;

        var settings = source.ItemManager.GameplaySettings;
        var info = source.EntityInfoGathered.TryGetValue(target, out var gathered) ? gathered : 0f;
        return Accuracy(source) * PSensor(settings, Resolution(source), info);
    }

    // Cut 12.2 fix batch (F6, Soul, 2026-09-24): the one place a bearing, a silhouette and PSpread are
    // assembled into a live forecast -- HitProbability and Inspect both call this instead of each carrying
    // its own copy. They used to each build their own bearing from TravelDirection and the target's facing;
    // a mutant that read the SHOOTER's facing instead of the target's survived in HitProbability's own copy
    // (M11) while the identical mistake in Inspect's copy was caught (M22), because two functions computing
    // the same thing are two places for the same bug to hide and only be found once.
    // Cost fix batch (Self, 2026-09-24): Forecast reads only sil.POnHull and sil.Span, never sil.Intervals, so
    // it rents the Silhouette's own scratch array from ArrayPool<Interval>.Shared and returns it before
    // returning -- Silhouette itself (given this pooled buffer) allocates nothing, measured directly.
    // Commit's own path (CommitProbability -> Silhouette, uncalled from here) still needs sil.Intervals alive
    // for the lateral draw that follows it, so it keeps its own owned array; only the forecast pools.
    // S3 fix batch (Hands, 2026-09-25) correction: "the forecast path no longer allocates on a gated-in call"
    // overclaimed -- a gated-in HitProbability call still allocates roughly 120 bytes (measured, Release; ~280
    // for Inspect, which reads one more gated stat). 12.3 fix batch correction: that cost is exactly three
    // GetBehavior<TargetingSystem> calls (Accuracy, Resolution via PSensor, Precision), at ~40 B each -- each
    // one walks Entity.Equipment, a ReactiveCollection<T> whose enumerator is heap-allocated per foreach. It is
    // NOT `Behaviors` (the earlier wording here was wrong): EquippedItem.Behaviors is a plain array, allocates
    // nothing to enumerate. With an item aimed at, ResolveAimPoint -> CellsOf also allocates (a List, then
    // ToArray) -- about 944 B per HitProbability, on top of the three GetBehavior calls. None of this is
    // Silhouette's or this pooling's cost; it predates Cut 12.2 and belongs to Entity's own collection choice
    // and to CellsOf, out of this cut's scope. Cut 10's own guard is what actually matters
    // operationally: HitProbability and CommitProbability both bail before Forecast is ever called for the
    // common gated-out case (GatedOutHitProbabilityAllocatesNothing pins that at exactly 0).
    private static (Silhouette Silhouette, float PSpread) Forecast(Weapon weapon, Entity source, Entity target, HullData targetHull, float precision, float range)
    {
        var bearing = Bearing(target, TravelDirection(weapon, source, target));
        var buffer = ArrayPool<Interval>.Shared.Rent(targetHull.Shape.Coordinates.Length);
        float pSpread;
        Silhouette sil;
        try
        {
            sil = Silhouette(target, targetHull, source.ResolvedTargetItem, bearing, precision, buffer);
            var settings = source.ItemManager.GameplaySettings;
            pSpread = PSpread(weapon.Spread, sil.Span, range, settings.SchematicCellSize);
        }
        finally
        {
            ArrayPool<Interval>.Shared.Return(buffer);
        }
        // sil.Intervals is the returned buffer -- cleared here so nothing outside this function can read it
        // after it goes back to the pool and another caller starts overwriting it.
        sil.Intervals = null;
        return (sil, pSpread);
    }

    public static float HitProbability(Weapon weapon, Entity source, Entity target)
    {
        var pFire = PFire(weapon, source, target, out var range);
        if (pFire <= 0f) return 0f;

        var targetHull = source.ItemManager.GetData(target.Hull) as HullData;
        var (sil, pSpread) = Forecast(weapon, source, target, targetHull, Precision(source), range);
        return pFire * pSpread * sil.POnHull;
    }

    // Presentation only: the debug HUD's view of exactly the factors HitProbability multiplies. It computes
    // every factor unconditionally, gates included, because a HUD wants to see why a shot is impossible --
    // which is precisely why the hot path must not be routed through it. Both read the same factor
    // functions below, and PBase is HitProbability's own answer, so the gates and the product have one
    // owner and the HUD cannot grow a second, subtly different model.
    public static FireControlDiagnostic Inspect(Weapon weapon, Entity source, Entity target)
    {
        var settings = source.ItemManager.GameplaySettings;
        var diagnostic = new FireControlDiagnostic
        {
            HasTarget = target != null,
            Accuracy = Accuracy(source),
            Resolution = Resolution(source),
            Precision = Precision(source),
            Tracking = Tracking(source),
            MinRange = weapon.MinRange,
            MaxRange = weapon.Range
        };
        if (target == null) return diagnostic;

        var toTarget = target.Position - source.Position;
        diagnostic.Range = length(toTarget);
        diagnostic.Visible = source.VisibleEntities.Contains(target);
        diagnostic.InRange = diagnostic.Range >= weapon.MinRange && diagnostic.Range <= weapon.Range;
        diagnostic.Locked = !(weapon is LockWeapon lockWeapon) || lockWeapon.IsLocked;
        diagnostic.InArc = InArc(weapon.Item, toTarget);

        var targetHull = source.ItemManager.GetData(target.Hull) as HullData;
        diagnostic.Info = source.EntityInfoGathered.TryGetValue(target, out var gathered) ? gathered : 0f;
        diagnostic.InfoDemandCeiling = InfoDemandCeiling(settings, diagnostic.Resolution);
        diagnostic.PSensor = PSensor(settings, diagnostic.Resolution, diagnostic.Info);
        diagnostic.PFire = diagnostic.Accuracy * diagnostic.PSensor;

        var (sil, pSpread) = Forecast(weapon, source, target, targetHull, diagnostic.Precision, diagnostic.Range);
        diagnostic.PSpread = pSpread;
        diagnostic.POnHull = sil.POnHull;

        diagnostic.PBase = HitProbability(weapon, source, target);
        return diagnostic;
    }

    // Cut 6c, 6c.1 (operator ruling 2026-09-19): Resolution stays a benefit -- higher is better -- so the
    // formula takes its reciprocal to derive the actual info ceiling instead of reading Resolution as that
    // ceiling directly. Resolution 1 (the unaided fallback) yields a ceiling of 1: needs complete
    // information, the floor by construction. The max guards a zero or negative authored Resolution; no
    // authored value should ever reach it.
    private static float InfoDemandCeiling(GameplaySettings settings, float resolution) =>
        settings.TargetDetectionInfoThreshold + (1f - settings.TargetDetectionInfoThreshold) / max(resolution, 1e-3f);

    private static float PSensor(GameplaySettings settings, float resolution, float info) =>
        saturate(unlerp(settings.TargetDetectionInfoThreshold, InfoDemandCeiling(settings, resolution), info));

    // Whether the weapon's own barrel-dispersion cone reaches the target's actual silhouette at this range:
    // weapon hardware, range-dependent, aim-point-blind. 1 for any zero-spread weapon. Cut 12.2: span comes
    // from the same Silhouette pOnHull reads, so the two factors can never disagree about the target's size
    // (the old bounding half-extent, max(Width, Height), is gone).
    private static float PSpread(float spread, float span, float range, float cellSize)
    {
        if (spread <= 0) return 1f;
        var halfExtent = .5f * span * cellSize;
        var angularRadius = degrees(atan(halfExtent / range));
        return saturate(angularRadius / (spread / 2f));
    }

    // Cut 12.2 (docs/fire-control-cut.md, "the rules every sub-cut reads"): the shot's world-planar travel
    // direction -- PredictedIntercept minus the source's own position, planar (R7). Frozen into PendingShot
    // at Fire (R10); HitProbability and Inspect recompute it fresh, at the current source/target positions,
    // for their forecast. Below a length of 1e-6 (point-blank, source and intercept coincide) it falls back
    // to world +z, the same fallback Apply used before this cut (`:463`).
    public static float2 TravelDirection(Weapon weapon, Entity source, Entity target)
    {
        var toIntercept = (PredictedIntercept(weapon, source, target) - source.Position).xz;
        return lengthsq(toIntercept) < 1e-6f ? float2(0, 1) : normalize(toIntercept);
    }

    // Cut 12.2: the only place a world direction meets a target's facing. One function, so nothing else may
    // compute a bearing (the authority map's forbidden-writer rule) -- Commit and the live forecast both read
    // this, never a second copy of the formula.
    private static float2 Bearing(Entity target, float2 travelDirection) => normalize(target.ToSchematic(travelDirection));

    // Cut 12.2: the lateral axis a bearing implies, fixed once here. Silhouette and Lane both read it instead
    // of each carrying their own copy of the sign.
    private static float2 Lateral(float2 bearing) => float2(-bearing.y, bearing.x);

    // Cut 12.2 fix batch (S1, Soul's second pass): the one function that projects a unit cell onto an axis --
    // the exact support of the axis-aligned unit square centred on c along `axis`, half-width
    // h = (|axis.x| + |axis.y|) / 2. Silhouette's shadow (axis = ell) and Lane's own admission test (axis =
    // ell) now read this SAME arithmetic instead of Lane re-deriving its own copy of centre/h -- the two used
    // to differ only in that Lane's copy was a plain re-derivation, not a second formula, but "not a second
    // formula" is exactly why they never drifted apart in exact arithmetic and exactly why the real defect was
    // never here: it was the second, independent SlabAlongB gate below, deleted with this fix.
    private static Interval Extent(float2 c, float2 axis)
    {
        var h = (abs(axis.x) + abs(axis.y)) / 2f;
        var centre = dot(c, axis);
        return new Interval { Lo = centre - h, Hi = centre + h };
    }

    public static float DeviationProbability(PendingShot shot, float now, out float deviation)
    {
        var elapsed = now - shot.FireTime;
        var predicted = shot.FireTargetPosition + shot.FireTargetVelocity * elapsed;
        deviation = shot.Target == null ? 0f : length((shot.Target.Position - predicted).xz);
        return shot.Target == null ? 1f : saturate(1f - deviation / shot.Tracking);
    }

    // Cut 3, R1: called once per burst step from InstantWeapon.Execute. Computes flight time, freezes the
    // payload snapshot (R10, Q6 -- the gun that fired it, not a re-read later), and queues a PendingShot for
    // Zone.Step to age and eventually resolve. Returns the ShotId so the caller's OnFire event can carry it to
    // presentation. Cut 5, 5.7 (Soul finding 12): does not store a predicted intercept -- Commit judges
    // deviation against a straight-line projection from FireTargetPosition/FireTargetVelocity, never the
    // intercept, so a stored copy decided nothing and PendingShot no longer carries one.
    // Cut 4 (docs/fire-control-cut.md): damageOverride lets a continuous weapon fire a shot for less than its
    // full Damage -- ConstantWeapon.Execute rolls one of these per GameplaySettings.BeamResolveInterval, for
    // Damage * interval, through this exact same freeze-and-queue path (a flight time of zero, since a beam's
    // authored Velocity is 0, commits and resolves in the tick it fires -- R4's short-flight degradation,
    // unchanged). Every other caller keeps reading the frozen weapon.Damage it always did.
    public static int Fire(Weapon weapon, EquippedItem item, Entity source, float? damageOverride = null)
    {
        var zone = source.Zone;
        var target = source.Target.Value;
        var now = zone.Time;

        // R1's engage gate and the shooter-side probability, evaluated now and frozen: nothing at commit time
        // re-reads a stat, an info level or a range. Cut 12.2: PFire no longer carries the spread/hull share --
        // those are priced live at Commit, against the target's facing then, not now (Bearing timing, R10).
        // F8 (Soul's fix batch, 2026-09-24): the `out range` overload is the one range HitProbability now
        // reads too -- Fire used to recompute its own second copy of `target.Position - source.Position` for
        // fireRange, right beside the copy PFire's gate already computed.
        var pFire = PFire(weapon, source, target, out var fireRange);
        var travelDirection = target != null ? TravelDirection(weapon, source, target) : float2(0, 1);

        var targetVelocity = float3.zero;
        var targetPosition = source.Position;
        var flightTime = 0f;
        if (target != null)
        {
            targetVelocity = float3(target.Velocity.x, 0, target.Velocity.y);
            targetPosition = target.Position;
            flightTime = weapon.Velocity > .01f ? fireRange / weapon.Velocity : 0f;
        }

        var commitHorizon = source.ItemManager.GameplaySettings.CommitHorizon;

        // Cut 6b, 6.2 (Soul finding 5): airburst is a property of the weapon, resolved and frozen here like
        // every other payload field -- never decided later by a Unity projectile. A weapon without the
        // Airburst flag freezes a zero radius, which Step below reads as "resolve this shot with Apply."
        var weaponItemData = item.Data as WeaponItemData;
        var isAirburst = weaponItemData != null && weaponItemData.WeaponModifiers.HasFlag(WeaponModifiers.Airburst);
        var burstPosition = isAirburst && target != null ? PredictedIntercept(weapon, source, target) : targetPosition;
        var burstRadius = isAirburst ? weaponItemData.AirburstRange ?? 0f : 0f;

        var shot = new PendingShot
        {
            ShotId = zone.NextShotId(),
            Source = source,
            Target = target,
            Weapon = item,
            Aimed = source.ResolvedTargetItem,
            Damage = damageOverride ?? weapon.Damage,
            Penetration = weapon.Penetration,
            DamageSpread = weapon.DamageSpread,
            DamageType = weapon.WeaponData.DamageType,
            PFire = pFire,
            Tracking = Tracking(source),
            Precision = Precision(source),
            TravelDirection = travelDirection,
            Spread = weapon.Spread,
            FireRange = fireRange,
            FireTime = now,
            FireTargetPosition = targetPosition,
            FireTargetVelocity = targetVelocity,
            ArrivalTime = now + flightTime,
            CommitTime = now + max(0f, flightTime - commitHorizon),
            BurstPosition = burstPosition,
            BurstRadius = burstRadius,
            Committed = false
        };

        zone.PendingShots.Add(shot);
        return shot.ShotId;
    }

    // Cut 3, R4: ages every shot in the zone, commits the ones that have reached their horizon and resolves
    // the ones that have arrived. Called from Zone.Update after the entity loop. A shot whose source or target
    // has left the zone resolves as a miss and is removed outright (0b table), whichever stage it is at.
    public static void Step(Zone zone, float dt)
    {
        var shots = zone.PendingShots;
        if (shots.Count == 0) return;

        var now = zone.Time;
        for (var i = shots.Count - 1; i >= 0; i--)
        {
            var shot = shots[i];

            var sourceGone = !zone.Entities.Contains(shot.Source);
            var targetGone = shot.Target != null && !zone.Entities.Contains(shot.Target);
            if (sourceGone || targetGone)
            {
                // Cut 11 (Soul C4): a shot whose source or target has left the zone resolves as a miss whichever
                // stage it is at (0b table). The resolution is published as a fresh miss rather than by
                // rewriting shot.Outcome: a committed outcome is immutable (R4) and stays a true record of what
                // the commit decided, while ShotResolved reports what actually happened -- nothing, because the
                // target is gone. Republishing the committed outcome here used to put a hit marker on a corpse.
                var miss = MakeOutcome(shot, false, false, false, int2.zero, float2.zero, 0f, now);
                if (!shot.Committed) zone.ShotCommitted.OnNext(miss);
                zone.ShotResolved.OnNext(miss);
                shots.RemoveAt(i);
                continue;
            }

            if (!shot.Committed && now >= shot.CommitTime)
            {
                shot.Outcome = Commit(zone, shot, now);
                shot.Committed = true;
                zone.ShotCommitted.OnNext(shot.Outcome);
                shots[i] = shot;
            }

            if (shot.Committed && now >= shot.ArrivalTime)
            {
                // Cut 6b, 6.2: an airburst shot (frozen BurstRadius > 0) resolves as an area effect instead of
                // a discrete hit -- Splash instead of Apply, never both, which is exactly the double-
                // application Soul was told to hunt for.
                if (shot.BurstRadius > 0f) Splash(zone, shot.BurstPosition, shot.BurstRadius, shot.Damage, shot.DamageType);
                else Apply(shot);
                zone.ShotResolved.OnNext(shot.Outcome);
                shots.RemoveAt(i);
            }
        }
    }

    // R3/R4: the one roll. p_base was frozen at fire; the only thing measured live is how far the target has
    // actually strayed, by now, from where a straight-line projection of its fire-time velocity said it would
    // be -- forgiven by the targeting system's Tracking, likewise frozen at fire.
    //
    // Cut 7: the short-circuit below (`p > 0f &&`) is a guard, NOT an invariant. It used to be one -- a shot
    // gated to zero at fire had to draw nothing so the shared stream advanced exactly as if it had never
    // queued -- but Cut 6b gave every shot its own generator, seeded from (zone, shot id) and discarded here,
    // so whether a gated shot draws is unobservable from anywhere. The claim this comment used to make is
    // gone with the stream it was about, and the test that asserted it became vacuous the same moment; what
    // replaced it pins the live rule instead (combat touches no shared stream, on any path).
    // 9.1 (docs/fire-control-cut.md): fmix32, MurmurHash3's 32-bit finalizer -- xor-shift, multiply,
    // xor-shift, multiply, xor-shift. Diffuses a structured seed (here, a zone seed XORed with a small,
    // low-bits-only ShotId) across every bit before the first xorshift draw reads it, so two seeds that
    // differ only in ShotId's low bits still land on uncorrelated first outputs. Caller-side mixing only
    // -- CultMath.Random itself is unchanged (see Commit's own note on why that follow-up is a separate
    // CultLib cut).
    private static uint MixSeed(uint seed)
    {
        seed ^= seed >> 16;
        seed *= 0x85ebca6bu;
        seed ^= seed >> 13;
        seed *= 0xc2b2ae35u;
        seed ^= seed >> 16;
        return seed;
    }

    // Cut 12.2 (docs/fire-control-cut.md, "Bearing timing"): steps 1-5. Everything the shooter decided is
    // frozen in `shot` (R10); everything the target is doing -- position (deviation) and now also facing
    // (bearing) -- is read live, here, at the commit tick. The Cut 7 guard extends to cover the new factors:
    // when PFire x pDeviation is already 0, no Silhouette is built. Shared by Commit's own roll and by the
    // debug HUD's forecast (TheHudEstimateIsTheCommitPrice) -- one function, one commit-time price.
    public static float CommitProbability(PendingShot shot, float now, out Silhouette sil)
    {
        sil = default;
        if (shot.Target == null) return 0f;

        var pDeviation = DeviationProbability(shot, now, out _);
        var p = shot.PFire * pDeviation;
        if (p <= 0f) return 0f;

        var bearing = Bearing(shot.Target, shot.TravelDirection);
        var hullData = shot.Source.ItemManager.GetData(shot.Target.Hull) as HullData;
        sil = Silhouette(shot.Target, hullData, shot.Aimed, bearing, shot.Precision);
        var settings = shot.Source.ItemManager.GameplaySettings;
        return p * PSpread(shot.Spread, sil.Span, shot.FireRange, settings.SchematicCellSize) * sil.POnHull;
    }

    private static ShotOutcome Commit(Zone zone, PendingShot shot, float now)
    {
        // Cut 6b, 6.1 (Soul finding 6): a shot's dice belong to the shot, not to whatever else happened to
        // draw from the engine-wide shared generator first. A pure function of (zone identity, shot id) --
        // the `| 1u` guards
        // the degenerate zero seed -- so two runs of the same fight from the same galaxy seed roll identically
        // no matter what the UI drew from the shared stream in between. This generator is local and dies with
        // the call: nothing outside Commit may seed or advance a combat draw, and Commit may not read or write
        // a shared stream.
        // 9.1 (docs/fire-control-cut.md): the per-shot seed is unchanged -- a pure function of
        // (zone identity, shot id), Cut 6b's own rule -- but CultMath.Random draws only one xorshift
        // round for NextFloat's first call, and one round does not diffuse a seed that differs from its
        // neighbours only in ShotId's low bits. Unmixed, every zone's first draw was a near-constant
        // function of ShotId (measured span ~0.125), making a shot a step function of p fixed at zone
        // creation, not a roll (Soul C1). MixSeed is a standard fmix32 finalizer (MurmurHash3): this is
        // a caller's obligation, not a CultMath defect -- seeding xorshift with a structured value and
        // drawing once is the documented way to get a correlated first output, and a follow-up to give
        // CultMath.Random a mixed-seed entry point of its own is recorded for a separate CultLib cut.
        var random = new Random(MixSeed((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u));
        var p = CommitProbability(shot, now, out var sil);

        var hit = p > 0f && random.NextFloat() < p;
        var cell = int2.zero;
        var bearing = float2.zero;
        var lateral = 0f;
        var shielded = false;
        var shieldBroken = false;

        if (hit)
        {
            // Cut 12.2: the same silhouette pOnHull drew its mass from -- same bearing (live, at this commit
            // tick), same sigma (the frozen Precision). A shot that passed the roll always lands on metal (R3:
            // one roll decides damage; the off-hull share was already priced into that roll, not resolved here
            // as a second stage). The lateral draw picks where along the shadow the shot lands; Lane converts
            // that (bearing, lateral) pair into the actual cell -- only the first element (the impact cell,
            // nearest the shooter's side) is used here; the rest of the lane is 12.3's armour march.
            bearing = Bearing(shot.Target, shot.TravelDirection);
            lateral = LateralDraw(sil, random.NextFloat());

            var hullData = shot.Source.ItemManager.GetData(shot.Target.Hull) as HullData;
            var buffer = ArrayPool<LaneCell>.Shared.Rent(hullData.Shape.Coordinates.Length);
            try
            {
                var count = Lane(hullData, bearing, lateral, buffer);
                // R3: a hit that passed its roll always lands on metal. With LateralDraw's half-open clamp,
                // the drawn `lateral` always falls strictly inside the interval sil.POnHull priced, so Lane
                // must find at least one occupied cell -- an empty lane here is not a miss to fall back on
                // silently (that would put a hit marker on (0,0), which may not even be occupied); it is proof
                // the draw and the lane have drifted apart.
                if (count == 0)
                    throw new InvalidOperationException(
                        $"shot {shot.ShotId}: Lane found no occupied cell at bearing {bearing} lateral {lateral} " +
                        "after a hit passed its roll -- LateralDraw and Lane have drifted apart (R3).");
                cell = buffer[0].Cell;
            }
            finally
            {
                ArrayPool<LaneCell>.Shared.Return(buffer);
            }

            var shield = shot.Target.Shield;
            var shieldActive = shield != null && shield.Item.Active.Value;
            if (shieldActive && shield.CanTakeHit(shot.DamageType, shot.Damage)) shielded = true;
            else if (shieldActive) shieldBroken = true;
        }

        return MakeOutcome(shot, hit, shielded, shieldBroken, cell, bearing, lateral, now);
    }

    // R4: the commit is authoritative and immutable from here on -- this only performs what Commit already
    // decided. The one owner of the shield-absorbs-or-hull-takes-it branch (it used to exist seven times,
    // once per Unity effect).
    // Cut 12.3 (docs/fire-control-cut.md, "How a direct hit travels"): replaces Entity.ApplyHit's 0.5-step
    // march and Entity.DamageSchematic's even split over the whole hit shape. Spread is width (Q12-2 = A):
    // 2n+1 parallel lanes, sharing the shot's damage evenly over whichever lanes actually meet metal -- the
    // centre lane, at the committed Lateral, always does (Commit already proved it, R3). Each lane is Lane's
    // own contiguous cell run from its own facing cell, clipped to the penetration depth (a cell is reached
    // when entry - impact.entry < penetration; the impact cell is always reached, replacing the deleted
    // `> .5f` threshold at zero cost). Whatever a lane does not spend -- penetration exhausted, a gap, or the
    // far side -- goes to the hull where the lane ends (Q12-3 = A), summed across every lane into one
    // DamageHull call.
    // F2 fix batch (Soul, 2026-09-25; operator: "I would prefer if the overlapping damage cells were diffused
    // sideways to thicken the line rather than doubling up"): lanes are spaced by the shadow width a cell
    // casts at this bearing -- Extent's own h, doubled -- not by one cell. At axis-aligned bearings that width
    // is exactly 1 (today's spacing, unchanged); at 45 degrees it is sqrt(2). Reusing Extent (the one function
    // that owns h, Silhouette's shadow and Lane's own admission test) instead of re-deriving |bx|+|by| a
    // second way is the same discipline Cut 12.2's own fix batch already applied to Lane -- two derivations of
    // one geometric quantity is exactly how that cut's crash happened. Because each cell's admitted lateral
    // interval is exactly this width wide and half-open, lanes this far apart can never both admit the same
    // cell: no cell is struck twice, and the footprint widens at angles instead of doubling up.
    private static void Apply(PendingShot shot)
    {
        if (!shot.Outcome.Hit) return;

        // Cut 5, 5.2 (Soul finding 4): the frozen decision, not a re-check -- a shield that recharges during
        // the flight does not retroactively survive a hit that broke it at commit time.
        if (shot.Outcome.ShieldBroken) shot.Target.Shield.Break();

        if (shot.Outcome.Shielded)
        {
            shot.Target.Shield.TakeHit(shot.DamageType, shot.Damage);
            return;
        }

        var target = shot.Target;
        // Cut 12.3: moved here from Entity.ApplyHit's own first line -- the shield branches above still return
        // before this runs, unchanged from 12.2.
        target.IncomingHit.OnNext(shot.Source);

        // Cut 12.2 (R4): the committed geometry, frozen. Apply reads no position or facing of its own --
        // shot.Outcome.Bearing is already the schematic-frame bearing Commit computed at the commit tick, so
        // turning the target after commit changes nothing here (TurningAfterCommitChangesNothing).
        var hullData = target.ItemManager.GetData(target.Hull) as HullData;
        var bearing = shot.Outcome.Bearing;
        var n = (int) floor(shot.DamageSpread + .5f); // today's rounding, Entity.cs:389 before this cut
        var laneCount = 2 * n + 1;
        // F2: the shadow width at this bearing, from Extent's own h (the cell argument does not affect h).
        var shadowExtent = Extent(float2.zero, Lateral(bearing));
        var laneSpacing = shadowExtent.Hi - shadowExtent.Lo;

        var buffers = new LaneCell[laneCount][];
        var walked = new int[laneCount];
        var metalLanes = 0;
        for (var li = 0; li < laneCount; li++)
        {
            buffers[li] = ArrayPool<LaneCell>.Shared.Rent(hullData.Shape.Coordinates.Length);
            walked[li] = Lane(hullData, bearing, shot.Outcome.Lateral + (li - n) * laneSpacing, buffers[li]);
            if (walked[li] > 0) metalLanes++;
        }

        // The centre lane (li == n) always meets metal: it walks from the committed Lateral, and Commit's own
        // guard already proved Lane finds an occupied cell there (R3) -- metalLanes is never 0.
        var damagePerLane = shot.Damage / metalLanes;
        var hullDamage = 0f;
        for (var li = 0; li < laneCount; li++)
        {
            if (walked[li] > 0)
            {
                var rem = damagePerLane;
                var impactEntry = buffers[li][0].Entry;
                for (var i = 0; i < walked[li]; i++)
                {
                    if (i > 0 && buffers[li][i].Entry - impactEntry >= shot.Penetration) break;
                    rem = target.Absorb(buffers[li][i].Cell, rem);
                }
                hullDamage += rem;
            }
            ArrayPool<LaneCell>.Shared.Return(buffers[li]);
        }

        target.DamageHull(hullDamage);
    }

    private static ShotOutcome MakeOutcome(PendingShot shot, bool hit, bool shielded, bool shieldBroken, int2 cell, float2 bearing, float lateral, float now)
    {
        return new ShotOutcome
        {
            ShotId = shot.ShotId,
            Source = shot.Source,
            Target = shot.Target,
            Weapon = shot.Weapon,
            Hit = hit,
            Shielded = shielded,
            ShieldBroken = shieldBroken,
            Cell = cell,
            Bearing = bearing,
            Lateral = lateral,
            ArrivalIn = max(0f, shot.ArrivalTime - now),
            DamageType = shot.DamageType
        };
    }

    // Cut 4 (docs/fire-control-cut.md): the one splash rule -- an unconditional area effect, not a rolled
    // shot. Nothing here draws: a mine or an airburst round always damages everything it catches, the same
    // as the Physics.OverlapSphere queries this replaces always did. Applies the shield-absorb-or-hull-takes-
    // it branch (F5, docs/stats-and-power-cut.md) per target, then -- for a target the shield didn't fully
    // absorb -- an even split (still today's rule; 12.3 only retired the direct-hit lane's even split, not
    // Splash's) over the directional half of that target's own hull that faces the blast, the rule moved
    // verbatim from the Splash subscription EntityInstance.cs carried before Cut 3 deleted it (git show
    // b7743789^:Assets/Scripts/Gameplay/EntityInstance.cs), made planar (R7): the blast-to-target direction is
    // measured in the zone's (x,z) plane and rotated into each target's own facing by its Direction, the same
    // rotation Entity's own schematic frame (ToSchematic) uses.
    // Cut 12.3: Entity.DamageSchematic is deleted with the direct-hit march it shared a file with -- Splash
    // now calls Entity.Absorb per footprint cell directly and sums the leftover into one DamageHull call,
    // arithmetically identical to DamageSchematic's own body (same per-cell split, same order, same
    // thresholds). Untouched otherwise; deleted in 12.4 along with the rest of the ray model.
    public static void Splash(Zone zone, float3 position, float radius, float damage, DamageType damageType)
    {
        foreach (var target in zone.Entities)
        {
            var toTarget = (target.Position - position).xz;
            if (length(toTarget) > radius) continue;

            var shield = target.Shield;
            var shieldActive = shield != null && shield.Item.Active.Value;
            var shieldAbsorbs = shieldActive && shield.CanTakeHit(damageType, damage);
            if (shieldActive && !shieldAbsorbs) shield.Break();
            if (shieldAbsorbs)
            {
                shield.TakeHit(damageType, damage);
                continue;
            }

            var hullData = target.ItemManager.GetData(target.Hull) as HullData;
            var localDirection = lengthsq(toTarget) > 1e-6f
                ? normalize(target.ToSchematic(toTarget))
                : float2(0, 1);

            var hitShape = new Shape(hullData.Shape.Width, hullData.Shape.Height);
            foreach (var v in hullData.Shape.Coordinates)
                if (dot(normalize((float2) v - hullData.Shape.CenterOfMass), localDirection) < 0)
                    hitShape[v] = true;

            var cells = hitShape.Coordinates;
            var damagePerCell = damage / cells.Length;
            var hullDamage = 0f;
            foreach (var v in cells)
                hullDamage += target.Absorb(v, damagePerCell);
            target.DamageHull(hullDamage);
        }
    }

    // Cut 12.2 (docs/fire-control-cut.md): Sigma keeps its Cut 6d/9.2 rule unchanged -- the frozen Precision's
    // reciprocal, in hull-schematic cell units, floored at half a cell. The 9.2 floor's old rationale ("the
    // discrete sum undershoots the continuous integral") is retired: Silhouette below integrates the Gaussian
    // exactly over the shadow's intervals, so an exact 1D integral does not undershoot. The floor survives as
    // a design minimum on group tightness (F12-3, TheSigmaFloorHoldsAtHalfACell) -- a targeting system is never
    // authored tighter than half a schematic cell's width of spread, regardless of what the exact integral
    // would otherwise allow.
    private const float SigmaFloor = 0.5f;
    private static float Sigma(float precision) => max(1f / max(precision, 1e-3f), SigmaFloor);

    // The aim point a silhouette is centred on (R10/Q6: the same frozen Aimed both HitProbability and Fire
    // read) -- the aimed item's own cell centroid when it currently occupies cells on the target, else the
    // hull's own centre of mass. One path; the uniform-random branch Cut 6d replaced stays deleted.
    private static float2 ResolveAimPoint(Entity target, HullData hullData, EquippedItem aimed)
    {
        var aimedCells = aimed != null ? CellsOf(target, aimed) : null;
        if (aimedCells != null && aimedCells.Length > 0)
            return aimedCells.Aggregate(float2.zero, (total, c) => total + (float2) c) / aimedCells.Length;
        return hullData.Shape.CenterOfMass;
    }

    // Cut 12.2 (docs/fire-control-cut.md, "the rules every sub-cut reads"): the merged lateral shadow the
    // hull casts across the bearing, and the exact 1D Gaussian mass it carries -- the one function HitProbability's
    // forecast, Inspect's HUD factors and Commit's roll and placement all read. Nowhere else may compute a
    // sigma, a projection or a bearing. Allocates one interval array of length N (occupied-cell count, <=128 on
    // shipped hulls) plus a sort -- only on this, the gated-in, path (Cut 10's own gated-out guard is
    // unaffected: HitProbability and CommitProbability both bail on a zero shooter/deviation factor before
    // this is ever called).
    // Cost fix batch (Self, 2026-09-24): `buffer`, when supplied, replaces the `new Interval[coords.Length]`
    // allocation -- the caller owns renting and returning it (ArrayPool<Interval>.Shared, sized to at least
    // coords.Length). Only HitProbability's and Inspect's forecast reads POnHull/Span and is done with the
    // array before returning, so only they pool it; Commit keeps its own array alive across the LateralDraw
    // call that follows, so it still passes null and gets an owned allocation. A caller-supplied buffer may be
    // larger than coords.Length (ArrayPool rents "at least"), so every loop below is bounded by coords.Length,
    // never buffer.Length.
    public static Silhouette Silhouette(Entity target, HullData hull, EquippedItem aimed, float2 bearing, float precision, Interval[] buffer = null)
    {
        var ell = Lateral(bearing);
        var coords = hull.Shape.Coordinates;

        var intervals = buffer ?? new Interval[coords.Length];
        for (var i = 0; i < coords.Length; i++)
            intervals[i] = Extent((float2) coords[i], ell);
        Array.Sort(intervals, 0, coords.Length);

        // Merge in place: sorted ascending by Lo, so an interval overlaps (or exactly abuts) the last kept
        // interval whenever its own Lo does not exceed that interval's Hi.
        var count = 0;
        for (var i = 0; i < coords.Length; i++)
        {
            if (count > 0 && intervals[i].Lo <= intervals[count - 1].Hi)
                intervals[count - 1].Hi = max(intervals[count - 1].Hi, intervals[i].Hi);
            else
                intervals[count++] = intervals[i];
        }

        var sigma = Sigma(precision);
        var aimPoint = ResolveAimPoint(target, hull, aimed);
        var a = dot(aimPoint, ell);

        var pOnHull = 0f;
        for (var i = 0; i < count; i++)
            pOnHull += Phi((intervals[i].Hi - a) / sigma) - Phi((intervals[i].Lo - a) / sigma);

        var span = count > 0 ? intervals[count - 1].Hi - intervals[0].Lo : 0f;

        return new Silhouette
        {
            Intervals = intervals,
            Count = count,
            AimPoint = aimPoint,
            A = a,
            Sigma = sigma,
            Span = span,
            POnHull = saturate(pOnHull)
        };
    }

    // Phi(z) = 1/2 (1 + erf(z / sqrt(2))): the standard normal CDF, CultMath's erf the one function underneath
    // every Gaussian-mass sum this cut computes.
    private static float Phi(float z) => .5f * (1f + erf(z * (1f / SQRT2)));

    private const float SQRT2 = 1.4142135f;

    // Cut 12.2: the lateral draw. One NextFloat u, taken after the hit roll -- walks the cumulative interval
    // mass to u * POnHull to select interval k, then inverts the Gaussian CDF inside it. `p` here is the
    // absolute CDF value at the drawn point (Phi(lo_k) plus the leftover target mass within interval k), not
    // a value renormalised to [0,1] -- inverting that gives s directly in the same units A and Sigma are in.
    // Clamped into [lo_k, hi_k) at the end to absorb rounding (a guard, not an invariant); erfinv's own
    // handling of the domain edges (+-infinity at p = 0 or 1) means the clamp is what actually keeps s finite
    // for an interval with negligible mass.
    private static float LateralDraw(Silhouette sil, float u)
    {
        var target = u * sil.POnHull;
        var cumulative = 0f;
        for (var k = 0; k < sil.Count; k++)
        {
            var interval = sil.Intervals[k];
            var loValue = Phi((interval.Lo - sil.A) / sil.Sigma);
            var hiValue = Phi((interval.Hi - sil.A) / sil.Sigma);
            var mass = hiValue - loValue;

            if (target <= cumulative + mass || k == sil.Count - 1)
            {
                var p = clamp(loValue + (target - cumulative), loValue, hiValue);
                var s = sil.A + sil.Sigma * SQRT2 * erfinv(2f * p - 1f);
                // Half-open [lo_k, hi_k), matching the shadow interval itself ("Each occupied cell c projects
                // to [dot(c,ell)-h, dot(c,ell)+h)"). A draw that rounds to exactly Hi is a real, reachable
                // float value (Soul found 3 of them enumerating all 2^24 NextFloat outputs on the
                // TurningArmour geometry) -- clamping to the closed [Lo, Hi] let s == Hi through, which Lane's
                // own half-open filter (`s >= centre + h` continues) then rejects for every cell, returning an
                // empty lane for a shot that already passed its roll (R3 violation). The epsilon matches
                // Lane's own contiguity tolerance; every merged interval is at least one cell's shadow width
                // wide (>= 2h), far above it.
                var hiExclusive = max(interval.Lo, interval.Hi - 1e-4f);
                return clamp(s, interval.Lo, hiExclusive);
            }

            cumulative += mass;
        }

        return sil.A; // sil.Count == 0: no metal in the shadow at all -- unreachable while POnHull > 0 gated Commit's roll
    }

    // Cut 12.2 (docs/fire-control-cut.md, "the rules every sub-cut reads"): exact slab traversal along the
    // bearing at a fixed lateral offset s, replacing the old 0.5-step sampling march. Collects the occupied
    // cells whose shadow interval contains s, each with its own entry/exit parameter along b, ordered by
    // entry (ties broken by dot(cell, b), then cell index for full determinism), then walks forward while
    // consecutive cells are contiguous (`next.entry <= current.exit + 1e-4`) -- the first gap ends the walk,
    // the same rule Entity.cs's old march kept. A direct hit starts outside the hull (t -> -infinity), so the
    // walk's first element is the impact cell. Only that first element is read in 12.2; the rest is 12.3's
    // armour-first absorption march. Writes into the caller-supplied pooled buffer and returns the walked
    // count; never allocates on its own.
    // Cut 12.2 fix batch (S1, Soul's second pass): admission is now decided ONCE, by the exact same shadow
    // interval Silhouette sums over (Extent(c, ell), the shared function above) -- not by a re-derivation and
    // not by a second, independent geometric test. The old code ran two tests: this shadow prefilter (a
    // "re-derivation" of Silhouette's own centre/h, inline) AND SlabAlongB, an independent 2-axis ray-box
    // intersection that could reject a cell the shadow test had just admitted, purely on float rounding at an
    // interval edge -- an empty lane for a shot that had already passed its roll (R3 violation, the crash Soul
    // reproduced through Zone.Update). Deleting the second gate removes the split authority: a cell the shadow
    // sum counted metal for can no longer be un-counted here.
    public static int Lane(HullData hull, float2 b, float s, LaneCell[] buffer)
    {
        var ell = Lateral(b);
        var coords = hull.Shape.Coordinates;

        var n = 0;
        for (var i = 0; i < coords.Length; i++)
        {
            var c = (float2) coords[i];
            var lateral = Extent(c, ell);
            if (s < lateral.Lo || s >= lateral.Hi) continue; // half-open, the same admission Silhouette sums over

            // F9 (Soul's fix batch, 2026-09-24): Projection (dot(cell, b)) is precomputed here, into the
            // struct, so the sort below needs no closure over `b`. S3 fix batch: the sort itself now uses
            // LaneCell's own IComparable<LaneCell>, not a per-call Comparer<T>.Create allocation the "never
            // allocates" comment used to contradict. Projection is also the "along-bearing projection" the cut
            // doc orders entry by.
            AlongBearing(c, b, ell, s, out var entry, out var exit);
            buffer[n++] = new LaneCell { Cell = coords[i], Entry = entry, Exit = exit, Projection = dot(c, b) };
        }

        Array.Sort(buffer, 0, n);

        var walked = n > 0 ? 1 : 0;
        for (var i = 1; i < n; i++)
        {
            if (buffer[i].Entry > buffer[i - 1].Exit + 1e-4f) break;
            walked++;
        }
        return walked;
    }

    // Cut 12.2 fix batch (S1): the along-bearing entry/exit parameters for a cell the shadow test above has
    // ALREADY admitted -- unconditional, never a gate. The doc's own proof ("s lies in the shadow is exactly
    // the same event as this lane has metal") makes the intersection this computes a certainty, not a
    // hypothesis to re-test: for a convex shape, a value inside its projection onto ell is achieved by a
    // nonempty segment of the line ell=s, so the box-slab solve below cannot come back empty in exact
    // arithmetic. It only ever disagreed with the shadow test by float rounding at the interval's own edge,
    // which is exactly where SlabAlongB used to throw the metal away; here that same rounding is absorbed by
    // clamping entry <= exit instead of rejecting.
    private static void AlongBearing(float2 c, float2 b, float2 ell, float s, out float entry, out float exit)
    {
        entry = float.NegativeInfinity;
        exit = float.PositiveInfinity;
        SlabAxis(b.x, c.x - .5f, c.x + .5f, s * ell.x, ref entry, ref exit);
        SlabAxis(b.y, c.y - .5f, c.y + .5f, s * ell.y, ref entry, ref exit);
        if (entry > exit) exit = entry; // rounding only -- the shadow test already proved a real intersection
    }

    // The slab (ray-box) bound of point(t) = t*b + s*ell against the unit square centred on cell c, one axis at
    // a time: solving bAxis*t + val in [lo, hi], then folding the result into the running (entry, exit) bound.
    // A near-zero bAxis component means the ray does not move along that axis at all, so it adds no constraint
    // on t (the shadow test above already established this axis's bound holds).
    private static void SlabAxis(float bAxis, float lo, float hi, float val, ref float entry, ref float exit)
    {
        if (abs(bAxis) < 1e-9f) return; // no motion along this axis: already covered by the shadow admission test

        var t1 = (lo - val) / bAxis;
        var t2 = (hi - val) / bAxis;
        entry = max(entry, min(t1, t2));
        exit = min(exit, max(t1, t2));
    }

    // The cells of the target's hull schematic actually occupied by `item` -- GearOccupancy is the one source
    // of truth for where an equipped item's footprint lands, the same table Entity.Absorb reads.
    private static int2[] CellsOf(Entity target, EquippedItem item)
    {
        var hullData = target.ItemManager.GetData(target.Hull) as HullData;
        var cells = new List<int2>();
        foreach (var v in hullData.Shape.Coordinates)
            if (target.GearOccupancy[v.x, v.y] == item)
                cells.Add(v);
        return cells.ToArray();
    }
}

// Cut 3 (docs/fire-control-cut.md, 0b table): a shot in flight. Born in FireControl.Fire, aged by
// FireControl.Step (called from Zone.Update after the entity loop), never serialised -- a zone saved mid-
// flight loses its shots, which is correct, a save is a scene boundary. Zone owns the collection and the
// ShotId; FireControl owns every transition. Nothing else may add, remove or mutate one.
public struct PendingShot
{
    public int ShotId;
    public Entity Source;
    public Entity Target;
    public EquippedItem Weapon;
    public EquippedItem Aimed;

    // The frozen payload (R10, Q6): the gun's own stats at the instant the trigger was pulled. Nothing
    // downstream re-evaluates any of these.
    public float Damage;
    public float Penetration;
    public float DamageSpread;
    public DamageType DamageType;
    // Cut 12.2 (docs/fire-control-cut.md): renamed from PBase -- its meaning changes from "the whole shot's
    // frozen roll price" to "the shooter's own share of it," behind the same gates (Accuracy x PSensor). The
    // spread and hull factors are no longer frozen here; they are priced live at Commit, against the target's
    // facing then (Bearing timing).
    public float PFire;
    public float Tracking;
    public float Precision;

    // Cut 12.2: frozen at Fire alongside the rest of the shooter's own decision (R10) -- the shot's
    // world-planar travel direction (FireControl.TravelDirection), and the weapon stats PSpread reads at
    // Commit against the target's live silhouette.
    public float2 TravelDirection;
    public float Spread;
    public float FireRange;

    // Cut 6b, 6.2: the airburst payload, frozen at fire alongside everything else above. BurstRadius is zero
    // for a weapon without the Airburst flag (WeaponModifiers, Enums.cs) -- Step reads that zero as "resolve
    // with Apply," not a separate bool.
    public float3 BurstPosition;
    public float BurstRadius;

    public float3 FireTargetPosition;
    public float3 FireTargetVelocity;
    public float FireTime;
    public float CommitTime;
    public float ArrivalTime;

    public bool Committed;
    public ShotOutcome Outcome;
}

public struct FireControlDiagnostic
{
    public bool HasTarget;
    public bool Visible;
    public bool InRange;
    public bool Locked;
    public bool InArc;
    public float Range;
    public float MinRange;
    public float MaxRange;
    public float Info;
    public float InfoDemandCeiling;
    public float Accuracy;
    public float Resolution;
    public float Precision;
    public float Tracking;
    public float PSensor;
    // Cut 12.2 (optional, presentation): the shooter-side factor alone, Accuracy x PSensor -- the same value
    // Fire freezes into PendingShot.PFire.
    public float PFire;
    public float PSpread;
    public float POnHull;
    public float PBase;
}

// Cut 12.2 (docs/fire-control-cut.md): a merged interval of a hull's lateral shadow, in the schematic frame's
// lateral (ell) coordinate.
// S3 fix batch (Hands, 2026-09-25): IComparable<Interval> (sorted by Lo) instead of a separate IComparer<T> --
// Array.Sort(T[], int, int, IComparer<T>) boxes/wraps the comparer into a fresh delegate on CoreCLR every call
// even when the comparer instance itself is a cached singleton; Array.Sort(T[], int, int) resolves T's own
// IComparable<T> at JIT time and allocates nothing per call. Same order as the deleted IntervalByLoComparer.
public struct Interval : IComparable<Interval>
{
    public float Lo;
    public float Hi;
    public int CompareTo(Interval other) => Lo.CompareTo(other.Lo);
}

// Cut 12.2: the hull's lateral shadow at one bearing, and the exact Gaussian mass it carries. Intervals holds
// the merged, sorted shadow (only the first Count entries are valid -- the array is sized to the hull's own
// occupied-cell count, not trimmed, to avoid a second allocation). A, Sigma and POnHull are read together by
// the lateral draw; Span is read by PSpread so the two factors can never disagree about the target's size.
public struct Silhouette
{
    public Interval[] Intervals;
    public int Count;
    public float2 AimPoint;
    public float A;
    public float Sigma;
    public float Span;
    public float POnHull;
}

// Cut 12.2: one occupied cell FireControl.Lane crossed at a fixed lateral offset, with its own slab entry/exit
// parameter along the bearing. Entry ascending is nearest-to-farthest along the shot's own travel direction.
// S3 fix batch (Hands, 2026-09-25): IComparable<LaneCell> replaces the separate LaneCellComparer, for the same
// reason Interval's own comparer was deleted -- the (T[], int, int, IComparer<T>) sort overload allocates a
// wrapper delegate per call even for a cached singleton comparer; (T[], int, int) does not. Same order as the
// deleted comparer: entry ascending, ties by projection along b, then cell index for full determinism.
public struct LaneCell : IComparable<LaneCell>
{
    public int2 Cell;
    public float Entry;
    public float Exit;
    // F9 (Soul's fix batch, 2026-09-24): dot(Cell, b), precomputed once per candidate so Lane's sort tie-break
    // needs no closure over the bearing.
    public float Projection;

    public int CompareTo(LaneCell other)
    {
        var byEntry = Entry.CompareTo(other.Entry);
        if (byEntry != 0) return byEntry;
        var byProjection = Projection.CompareTo(other.Projection);
        if (byProjection != 0) return byProjection;
        var byX = Cell.x.CompareTo(other.Cell.x);
        return byX != 0 ? byX : Cell.y.CompareTo(other.Cell.y);
    }
}

// Cut 3 (docs/fire-control-cut.md, 0b table): a commit. Created once, at ArrivalTime - CommitHorizon (or at
// fire time when the flight is shorter than the horizon), immutable from then on, published once on
// Zone.ShotCommitted and again (unchanged) on Zone.ShotResolved at arrival. Presentations are the only
// readers, and nothing outside presentation may read one to change state.
public sealed class ShotOutcome
{
    public int ShotId;
    public Entity Source;
    public Entity Target;
    public EquippedItem Weapon;
    public bool Hit;
    public bool Shielded;
    // Cut 5, 5.2 (Soul finding 4): frozen alongside the rest of the outcome (R4) -- a shield present, active,
    // and unable to CanTakeHit this shot is decided broken right here, so Apply performs Break() rather than
    // deciding it, and a shield that recharges mid-flight cannot retroactively dodge it.
    public bool ShieldBroken;
    public int2 Cell;
    // Cut 12.2 (R4): the committed geometry, frozen alongside the rest of the outcome -- the schematic-frame
    // bearing and the lateral offset the roll's placement drew. Apply reads these, not a live position or
    // facing (TurningAfterCommitChangesNothing).
    public float2 Bearing;
    public float Lateral;
    public float ArrivalIn;
    public DamageType DamageType;
}
