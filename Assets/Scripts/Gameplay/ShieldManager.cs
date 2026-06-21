using System.Collections.Generic;
using UnityEngine;

public class ShieldManager : MonoBehaviour
{
    public Prototype ShieldPrototype;
    public float CollisionHitDuration = 3;
    public float LootInteractionCooldown = 0.25f;
    public float ShieldCollisionCooldown = 0.1f;
    
    public Entity Entity { get; set; }

    private bool IsShieldActive()
    {
        return Entity?.Shield != null && Entity.Shield.Item.Active.Value;
    }

    private Bounds ShieldBounds()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static float ShieldPlanarRadius(Bounds bounds)
    {
        return Mathf.Max(bounds.extents.x, bounds.extents.z, 0.001f);
    }

    public void ShowHit(Vector3 point, float duration)
    {
        var shield = ShieldPrototype.Instantiate<ShieldAnimation>();
        shield.Direction = shield.transform.InverseTransformPoint(point).normalized;
        shield.Duration = duration;
    }
}
