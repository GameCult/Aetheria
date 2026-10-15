using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;
using UniRx;
using UnityEngine;

public class ShieldManager : MonoBehaviour, IAbsorbPresenter
{
    public Prototype ShieldPrototype;
    public float CollisionHitDuration = 3;

    private Entity _entity;
    private IDisposable _contacts;

    // Ship-on-ship contact is Zone's (Contacts.Step); this shield only presents it.
    public Entity Entity
    {
        get => _entity;
        set
        {
            _contacts?.Dispose();
            _entity = value;
            _contacts = value?.Zone.Contacted.Subscribe(Present);
        }
    }

    private void OnDestroy() => _contacts?.Dispose();

    private void Present(ContactEvent contact)
    {
        if (contact.A != Entity && contact.B != Entity) return;
        if (Entity.Shield != null && Entity.Shield.Item.Active.Value)
            ShowHit(DrawAhead.Carried(Entity, float3(contact.Point.x, Entity.Position.y, contact.Point.y), ActionGameManager.Clock.Lead).ToUnity(), CollisionHitDuration);
    }

    private ShieldEnvelope _envelope;
    private bool _loggedMissingEnvelope;

    void Awake()
    {
        _envelope = GetComponent<ShieldEnvelope>();
    }

    private void WarnMissingEnvelopeOnce()
    {
        if (_loggedMissingEnvelope) return;
        _loggedMissingEnvelope = true;
        Debug.LogWarning("ShieldManager has no ShieldEnvelope; falling back to its own transform for the hit direction.", this);
    }

    // A shot the shield took ripples it where the shot landed, drawn on the hull as it is drawn. The hull layer is
    // ImpactPresenter's.
    public void Absorb(AbsorbEvent e)
    {
        if (e.Layer != AbsorbLayer.Shield || Entity == null || Entity.Shield == null || !Entity.Shield.Item.Active.Value || !ActionGameManager.DrawsAt(Entity)) return;
        ShowHit(DrawAhead.Carried(Entity, e.Position, ActionGameManager.Clock.Lead).ToUnity(), CollisionHitDuration);
    }

    public void ShowHit(Vector3 point, float duration)
    {
        var shield = ShieldPrototype.Instantiate<ShieldAnimation>();
        // Cut 2 of docs/shield-panel-cut.md: this used to compute
        // normalize(shield.transform.InverseTransformPoint(point)) itself, duplicating
        // FieldDriver's projection against a different transform (Authority B vs A in the map's
        // §1). shield.transform sits at the same place as this ShieldManager's own transform (the
        // Shield prefab instance's pooled visual is parented under it with an identity local
        // offset), so this ShieldManager's ShieldEnvelope is the same envelope. Direction is a pick
        // of direction, not the true surface normal -- ShieldAnimation.Direction keeps its existing
        // meaning (R6's note that both existing consumers get away with normalize(p) because they
        // want a direction, not a normal).
        //
        // Every "Shield" object gets a ShieldEnvelope in this cut, but the operator rigs prefabs by
        // hand on their own schedule (Q5), so until Longinus.prefab/Djinni.prefab are actually
        // edited, every ship in the game has none yet. A missing envelope must therefore behave
        // exactly like the deleted code, not degrade -- a fallback that silently changes behaviour
        // (this used to hard-fail to a fixed direction) is worse than either the old code or a
        // loud failure. So this goes through ShieldEnvelope's own static fallback entry point
        // (one owner, no second copy of the maths) rather than recomputing the projection here.
        if (_envelope == null)
        {
            WarnMissingEnvelopeOnce();
            shield.Direction = ShieldEnvelope.SurfaceDirection(shield.transform, point);
        }
        else
        {
            shield.Direction = _envelope.SurfaceDirection(point);
        }
        shield.Duration = duration;
    }
}
