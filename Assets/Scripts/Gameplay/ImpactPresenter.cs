using System.Collections.Generic;
using CultMath.UnityBridge;
using UnityEngine;

// How one entity's landed impacts look and feel: the hull reaction at the drawn impact point, and the player's camera
// kick. The simulation publishes each impact once on the entity's Capabilities (FireControl); Feedback.Cue decides how
// hard it reads and whether the camera feels it, DrawAhead.Carried decides where it is drawn; this decides nothing.
// The shield ripple is ShieldManager's. Everything here is display-only and runs in real time.
public class ImpactPresenter : MonoBehaviour, IAbsorbPresenter
{
    public Entity Entity;

    private readonly List<Prototype> _live = new List<Prototype>();

    public void Absorb(AbsorbEvent e)
    {
        var feedback = ImpactFeedback.Instance;
        if (feedback == null || Entity == null) return;

        var manager = ActionGameManager.Instance;
        var cue = Feedback.Cue(e, Entity.EquippedHull.Data.Durability,
            manager != null && Entity == manager.CurrentEntity,
            ActionGameManager.PlayerSettings.GraphicsSettings.ScreenShake, feedback.IntensityScale);
        var point = DrawAhead.Carried(Entity, cue.Position, ActionGameManager.Clock.Lead).ToUnity();

        if (e.Layer == AbsorbLayer.Hull && feedback.HullEffect != null)
        {
            _live.RemoveAll(live => live == null || !live.gameObject.activeSelf);
            var effect = feedback.HullEffect.Instantiate<Prototype>();
            var size = Mathf.Max(feedback.MinEffectScale, cue.Intensity);
            effect.transform.SetParent(transform, false);
            effect.transform.position = point;
            effect.transform.localScale = Vector3.one * (size / Mathf.Max(transform.lossyScale.x, 1e-3f));
            _live.Add(effect);
        }

        if (cue.Shake > 0f)
            feedback.Kick(point, new Vector3(cue.Direction.x, 0f, cue.Direction.z), cue.Shake);
    }

    // A live effect is a pooled child of this instance; hand it back before the instance takes it down with it.
    private void OnDestroy()
    {
        foreach (var live in _live)
            if (live != null && live.gameObject.activeSelf) live.ReturnToPool();
    }
}
