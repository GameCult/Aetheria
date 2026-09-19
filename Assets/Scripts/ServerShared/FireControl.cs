/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

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

    public static float Precision(Entity entity)
    {
        var system = entity.GetBehavior<TargetingSystem>();
        return system != null && system.Item.Active.Value ? system.Precision : 0f;
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
    public static float HitProbability(Weapon weapon, Entity source, Entity target)
    {
        if (target == null) return 0f;
        if (!source.VisibleEntities.Contains(target)) return 0f;

        var toTarget = target.Position - source.Position;
        var range = length(toTarget);
        if (range < weapon.MinRange || range > weapon.Range) return 0f;
        if (weapon is LockWeapon lockWeapon && !lockWeapon.IsLocked) return 0f;
        if (!InArc(weapon.Item, toTarget)) return 0f;

        var settings = source.ItemManager.GameplaySettings;
        var info = source.EntityInfoGathered.TryGetValue(target, out var gathered) ? gathered : 0f;
        // Cut 6c, 6c.1 (operator ruling 2026-09-19): Resolution stays a benefit -- higher is better -- so the
        // formula takes its reciprocal to derive the actual info ceiling instead of reading Resolution as
        // that ceiling directly. Resolution 1 (the unaided fallback above) yields a ceiling of 1: needs
        // complete information, the floor by construction. A higher Resolution pulls the ceiling down toward
        // the detection threshold, needing less info before sensor state stops limiting hits. The max guards
        // a zero or negative authored Resolution; no authored value should ever reach it.
        var demandCeiling = settings.TargetDetectionInfoThreshold +
            (1f - settings.TargetDetectionInfoThreshold) / max(Resolution(source), 1e-3f);
        var pSensor = saturate(unlerp(settings.TargetDetectionInfoThreshold, demandCeiling, info));

        float pSpread;
        if (weapon.Spread > 0)
        {
            var targetHull = source.ItemManager.GetData(target.Hull) as HullData;
            var halfExtent = .5f * max(targetHull.Shape.Width, targetHull.Shape.Height) * settings.SchematicCellSize;
            var angularRadius = degrees(atan(halfExtent / range));
            pSpread = saturate(angularRadius / (weapon.Spread / 2f));
        }
        else pSpread = 1f;

        return Accuracy(source) * pSensor * pSpread;
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

        // R1's engage gate and probability, evaluated now and frozen: nothing at commit time re-reads a stat,
        // an info level or a range. Only live target *position* (deviation) is read again, at commit.
        var pBase = target != null ? HitProbability(weapon, source, target) : 0f;

        var targetVelocity = float3.zero;
        var targetPosition = source.Position;
        var flightTime = 0f;
        if (target != null)
        {
            targetVelocity = float3(target.Velocity.x, 0, target.Velocity.y);
            targetPosition = target.Position;
            var range = length(targetPosition - source.Position);
            flightTime = weapon.Velocity > .01f ? range / weapon.Velocity : 0f;
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
            PBase = pBase,
            Tracking = Tracking(source),
            Precision = Precision(source),
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
                if (!shot.Committed)
                {
                    shot.Outcome = MakeOutcome(shot, false, false, false, int2.zero, null, now);
                    zone.ShotCommitted.OnNext(shot.Outcome);
                }
                zone.ShotResolved.OnNext(shot.Outcome);
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
    // be -- forgiven by the targeting system's Tracking, likewise frozen at fire. A shot gated to zero at fire
    // (out of arc, out of range, unlocked, undetected) draws nothing here (short-circuit below): the RNG
    // sequence is exactly as if the shot had never queued.
    private static ShotOutcome Commit(Zone zone, PendingShot shot, float now)
    {
        // Cut 6b, 6.1 (Soul finding 6): a shot's dice belong to the shot, not to whatever else happened to
        // draw from the engine-wide shared generator first. A pure function of (zone identity, shot id) --
        // the `| 1u` guards
        // the degenerate zero seed -- so two runs of the same fight from the same galaxy seed roll identically
        // no matter what the UI drew from the shared stream in between. This generator is local and dies with
        // the call: nothing outside Commit may seed or advance a combat draw, and Commit may not read or write
        // a shared stream.
        var random = new Random((zone.CombatSeed * 2654435761u) ^ (uint) shot.ShotId | 1u);
        var p = shot.PBase;
        if (p > 0f && shot.Target != null)
        {
            var elapsed = now - shot.FireTime;
            var predicted = shot.FireTargetPosition + shot.FireTargetVelocity * elapsed;
            var deviation = length((shot.Target.Position - predicted).xz);
            // Cut 5, 5.1: Tracking is always positive now (Tracking() above never returns 0), so the branch
            // that used to turn a Tracking-less shooter's forgiveness into a hard <.01f wall is gone outright.
            var pDeviation = saturate(1f - deviation / shot.Tracking);
            p *= pDeviation;
        }

        var hit = p > 0f && random.NextFloat() < p;
        var cell = int2.zero;
        EquippedItem aimed = null;
        var shielded = false;
        var shieldBroken = false;

        if (hit)
        {
            var hullData = shot.Source.ItemManager.GetData(shot.Target.Hull) as HullData;
            var aimedCells = shot.Aimed != null ? CellsOf(shot.Target, shot.Aimed) : null;
            if (aimedCells != null && aimedCells.Length > 0 && random.NextFloat() < shot.Precision)
            {
                aimed = shot.Aimed;
                cell = aimedCells[random.NextInt(aimedCells.Length)];
            }
            else
            {
                var coords = hullData.Shape.Coordinates;
                cell = coords[random.NextInt(coords.Length)];
            }

            var shield = shot.Target.Shield;
            var shieldActive = shield != null && shield.Item.Active.Value;
            if (shieldActive && shield.CanTakeHit(shot.DamageType, shot.Damage)) shielded = true;
            else if (shieldActive) shieldBroken = true;
        }

        return MakeOutcome(shot, hit, shielded, shieldBroken, cell, aimed, now);
    }

    // R4: the commit is authoritative and immutable from here on -- this only performs what Commit already
    // decided. The one owner of the shield-absorbs-or-hull-takes-it branch (it used to exist seven times,
    // once per Unity effect).
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

        var hitDirection = float2(0, 1);
        var toTarget = (shot.Target.Position - shot.Source.Position).xz;
        if (lengthsq(toTarget) > 1e-6f) hitDirection = normalize(toTarget);

        shot.Target.ApplyHit(shot.Source, shot.Outcome.Cell, shot.DamageSpread, shot.Penetration, shot.Damage, hitDirection);
    }

    private static ShotOutcome MakeOutcome(PendingShot shot, bool hit, bool shielded, bool shieldBroken, int2 cell, EquippedItem aimed, float now)
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
            Aimed = aimed,
            Cell = cell,
            ArrivalIn = max(0f, shot.ArrivalTime - now),
            DamageType = shot.DamageType
        };
    }

    // Cut 4 (docs/fire-control-cut.md): the one splash rule -- an unconditional area effect, not a rolled
    // shot. Nothing here draws: a mine or an airburst round always damages everything it catches, the same
    // as the Physics.OverlapSphere queries this replaces always did. Applies the shield-absorb-or-hull-takes-
    // it branch (F5, docs/stats-and-power-cut.md) per target, then -- for a target the shield didn't fully
    // absorb -- DamageSchematic over the directional half of that target's own hull that faces the blast, the
    // rule moved verbatim from the Splash subscription EntityInstance.cs carried before Cut 3 deleted it
    // (git show b7743789^:Assets/Scripts/Gameplay/EntityInstance.cs), made planar (R7): the blast-to-target
    // direction is measured in the zone's (x,z) plane and rotated into each target's own facing by its
    // Direction, the same rotation Entity.ApplyHit's penetration march uses, in place of a Unity
    // InverseTransformDirection.
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
            var forward = normalize(target.Direction);
            var right = float2(forward.y, -forward.x);
            var localDirection = lengthsq(toTarget) > 1e-6f
                ? normalize(float2(dot(toTarget, right), dot(toTarget, forward)))
                : float2(0, 1);

            var hitShape = new Shape(hullData.Shape.Width, hullData.Shape.Height);
            foreach (var v in hullData.Shape.Coordinates)
                if (dot(normalize((float2) v - hullData.Shape.CenterOfMass), localDirection) < 0)
                    hitShape[v] = true;

            target.DamageSchematic(damage, hitShape);
        }
    }

    // The cells of the target's hull schematic actually occupied by `item` -- GearOccupancy is the one source
    // of truth for where an equipped item's footprint lands, the same table Entity.DamageSchematic reads.
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
    public float PBase;
    public float Tracking;
    public float Precision;

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
    public EquippedItem Aimed;
    public int2 Cell;
    public float ArrivalIn;
    public DamageType DamageType;
}
