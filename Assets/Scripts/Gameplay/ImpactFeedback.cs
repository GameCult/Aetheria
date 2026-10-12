using Cinemachine;
using UnityEngine;

// Presentation tuning for landed impacts (ImpactPresenter reads it, nothing in the simulation does): the hull
// reaction's effect prefab and size, how strongly a hit's share of the hull reads (Feedback.Cue's intensityScale),
// and the camera impulse. A scene component, so the scene is where it is tuned; absent in menus and test scenes,
// where ImpactPresenter does nothing.
public class ImpactFeedback : MonoBehaviour
{
    public static ImpactFeedback Instance { get; private set; }

    // A scene instance of a pooled hit effect (Prototype); a hit spawns one under the struck EntityInstance.
    public Prototype HullEffect;

    // Multiplies a hit's share of the receiver's max durability before it saturates to 0..1 (Feedback.Cue).
    public float IntensityScale = 10f;

    // The effect's scale never drops below this, so the lightest hit still reads.
    public float MinEffectScale = .3f;

    public CinemachineImpulseDefinition Impulse = new CinemachineImpulseDefinition();

    // Impulses raised since the scene loaded; the batchmode smoke reads it.
    public int KicksRaised { get; private set; }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Raises one camera impulse of the given force, travelling along `direction` from `position`.
    public void Kick(Vector3 position, Vector3 direction, float force)
    {
        KicksRaised++;
        Impulse.CreateEvent(position, direction.normalized * force);
    }
}
