using UnityEngine;
using CultMath.UnityBridge;

// Cut 4 (docs/fire-control-cut.md): Hit, Splash, SendHit and SendSplash are deleted -- their last callers
// (ConstantLaser, ConstantLightning, ConstantParticleWeapon, HitscanEffect, Mine) are gone too (see those
// files and FireControl.Splash). Nothing decides or applies damage through a Unity collider any more (R8):
// this component is now purely a physics surface for ship-on-ship collision impulse and Mine's own proximity
// arming query (Q7), neither of which is hit detection.
public class HullCollider : MonoBehaviour
{
    public Entity Entity { get; set; }

    private void OnCollisionEnter(Collision other)
    {
        Entity.Velocity += other.impulse.ToCultMath().xz / Time.deltaTime;
    }
}
