using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;

public class ShieldManager : MonoBehaviour
{
    public Prototype ShieldPrototype;
    public float CollisionHitDuration = 3;
    
    public Entity Entity { get; set; }

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
        shield.Direction = normalize(shield.transform.InverseTransformPoint(point));
        shield.Duration = duration;
    }
}
