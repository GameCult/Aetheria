using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;
using UnityEngine;

public class ShieldManager : MonoBehaviour
{
    public Prototype ShieldPrototype;
    public float CollisionHitDuration = 3;

    public Entity Entity { get; set; }

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

    private void OnCollisionEnter(Collision other)
    {
        var otherShield = other.collider.GetComponent<ShieldManager>();
        if (!otherShield)
        {
            var gridObject = other.collider.GetComponent<GridObject>();
            if (gridObject)
            {
                var itemPickup = other.collider.GetComponent<ItemPickup>();
                var mine = other.collider.GetComponent<Mine>();
                if (itemPickup)
                {
                    if (Entity.CargoBays.Any(c => c.TryStore(itemPickup.Item)))
                    {
                        // TODO: Pickup notification!
                        Destroy(itemPickup.gameObject);
                    }
                    else
                    {
                        // TODO: Pickup failed notification!
                        var cp = other.GetContact(0);
                        gridObject.Velocity += cp.normal * 25;
                        Debug.Log("Attempted item pickup, but no space in cargo bay!");
                    }
                }
                else if (mine)
                {
                    mine.Explode();
                }
                else
                {
                    Debug.Log("Shield collision occurred with grid object, but other collider isn't a mine or an item pickup!");
                }
            }
            else
                Debug.Log("Shield collision occurred, but other collider isn't a shield or a grid object!");
            return;
        }
        var contact = other.GetContact(0);
        var normal = normalize(float2(contact.normal.x, contact.normal.z));
        var tangent = normal.Rotate(ItemRotation.CounterClockwise);
        var v1n = dot(normal, Entity.Velocity);
        var v1t = dot(tangent, Entity.Velocity);
        var v2n = dot(normal, otherShield.Entity.Velocity);
        //var v2t = dot(tangent, otherShield.Entity.Velocity);

        var v1np = PostCollisionVelocity(v1n, Entity.Mass, v2n, otherShield.Entity.Mass);
        Entity.Velocity = tangent * v1t + normal * v1np;
        if(Entity.Shield != null && Entity.Shield.Item.Active.Value) ShowHit(contact.point, CollisionHitDuration);
    }

    private float PostCollisionVelocity(float v1, float m1, float v2, float m2)
    {
        return (v1 * (m1 - m2) + m2 * v2) / (m1 + m2);
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
