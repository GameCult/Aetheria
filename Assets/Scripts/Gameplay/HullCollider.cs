using UnityEngine;

// Cut 4 (docs/fire-control-cut.md): Hit, Splash, SendHit and SendSplash are deleted -- their last callers
// (ConstantLaser, ConstantLightning, ConstantParticleWeapon, HitscanEffect) are gone too (see those
// files and FireControl.Detonate, 12.4(b)'s renamed Splash). Nothing decides or applies damage through a Unity
// collider any more (R8), and no collision callback writes an Entity (cut sim-contact): ship contact is
// Zone's, through Contacts.Step. This component holds the hull's collider for authoring, smokes and click
// picking, and writes nothing.
public class HullCollider : MonoBehaviour
{
    public Entity Entity { get; set; }
}
