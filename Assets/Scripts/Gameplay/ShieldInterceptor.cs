using System.Collections.Generic;
using UnityEngine;
using CultMath.UnityBridge;
using ShieldField;

// Cut 3 of docs/shield-panel-cut.md (R2): the shield is an interceptor. It converts each absorbed
// hit into a hard-light panel posed on the ship's ShieldEnvelope -- it decides nothing about the
// rule (R3/R6: the hits it shows are cosmetic, fire control decides what actually happened).
//
// Adds no pool: Assets/Scripts/Prototype.cs already is one (Instantiate<T>/ReturnToPool), and
// ShieldManager.ShowHit already uses it for exactly this short-lived per-hit shape.
public class ShieldInterceptor : MonoBehaviour, IAbsorbPresenter
{
    public ShieldEnvelope Envelope;
    public Prototype PanelPrototype;      // a prefab carrying one ShieldPanel (Cut 4 rigs it)
    public int MaxLivePanels = 12;        // §2.3: ~0.12 ms CPU issuance per live panel
    public float PanelRadius = 2f;        // the SHIELD ITEM's size, not the hull's (R2)
    public float EnergyScale = 1f;
    [Range(0f, 2f)] public float ReuseFraction = 1f;
    [Tooltip("Margin added on top of growDuration + shardLife before a panel with no further " +
             "hits is released back to the pool (§3: a fixed lifetime, not a GPU readback).")]
    public float DeathMargin = 0.25f;
    public FracturePattern[] PatternByDamageType;

    struct LivePanel
    {
        public ShieldPanel Panel;
        public Prototype Proto;
        public Vector3 Position;
        public float SpawnedAt;
        public float DieAt;
    }

    readonly List<LivePanel> _live = new(16);

    void Awake()
    {
        // Cut 4 rigs the scene; this is the one push Cut 3 owns so the prototype's tiling is built
        // at the item's size (R2: size is the item's, not the hull's) rather than whatever radius
        // happens to be authored on the prefab. It only lands correctly if the prototype's own
        // ShieldPanel has not already run its first OnEnable/Rebuild -- see the Cut 4 recipe note
        // on starting PanelPrototype's GameObject inactive in the scene.
        if (PanelPrototype != null)
        {
            var proto = PanelPrototype.GetComponent<ShieldPanel>();
            if (proto != null) proto.panelRadius = PanelRadius;
        }
        _live.Capacity = Mathf.Max(_live.Capacity, MaxLivePanels);
    }

    void Update()
    {
        // Release timed-out panels. A GPU readback of "is anything still visible" would stall the
        // pipeline every frame for every live panel (§3's cost note); a fixed lifetime, started at
        // arm time, is the honest and cheap alternative -- same tradeoff the map makes for Death in
        // ShieldPanel itself.
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            if (Time.time < _live[i].DieAt) continue;
            _live[i].Proto.ReturnToPool();
            _live.RemoveAt(i);
        }
    }

    public int LiveCount => _live.Count;

    /// <summary>Cut 3 verification surface (Q7: no NUnit path for these files) -- lets the
    /// batchmode probe step and read back a specific live panel's simulation without duplicating
    /// the interceptor's own spawn/reuse/pose logic.</summary>
    public ShieldPanel DebugPanelAt(int i) => _live[i].Panel;

    // IAbsorbPresenter. The event is enough (map §"The event is enough"): project the event's
    // world-space position onto the envelope, pose a panel there with the envelope's true surface
    // normal, strike whichever panel owns that point.
    public void Absorb(AbsorbEvent e)
    {
        if (Envelope == null || PanelPrototype == null) return;

        Vector3 worldPos = e.Position.ToUnity();
        Vector3 intercept = Envelope.ProjectToSurface(worldPos);
        Vector3 dir = e.Direction.ToUnity();
        float energy = e.Magnitude * EnergyScale;
        var pattern = PatternFor(e.DamageType);

        int reuseIndex = FindReusable(intercept);
        if (reuseIndex >= 0)
        {
            // Reuse: a second hit landing inside a living panel strikes THAT panel rather than
            // spawning a new one -- this is the rule that makes temper erosion (ShieldSim.compute's
            // multi-hit budget) visible at all. No ResetSim here: that would wipe the very state
            // this rule exists to preserve.
            _live[reuseIndex].Panel.Hit(intercept, dir, energy, pattern);
            return;
        }

        ShieldPanel panel;
        Prototype panelProto;
        if (_live.Count >= MaxLivePanels)
        {
            // Pool exhausted: recycle the oldest live panel in place rather than drop the strike
            // (one event, one strike) or grow past the §2.3 frame budget. The oldest is the one
            // furthest through its own fade. Reused directly (not routed through
            // Prototype.ReturnToPool + Instantiate) because the interceptor already holds the
            // exact instance to reassign; a round trip through the pool's free list would only add
            // SetParent/SetActive churn for the same outcome.
            int oldest = OldestIndex();
            panel = _live[oldest].Panel;
            panelProto = _live[oldest].Proto;
            _live.RemoveAt(oldest);
        }
        else
        {
            panelProto = PanelPrototype.Instantiate<Prototype>();
            panel = panelProto.GetComponent<ShieldPanel>();
        }

        var normal = Envelope.SurfaceNormal(intercept);
        var t = panel.transform;
        t.position = intercept;
        t.rotation = Quaternion.LookRotation(normal);
        // Scale is never touched here: R2/Cut 3 require it stay 1, which is a property of the
        // prototype's own authored transform and its parent chain (Cut 4's rig), not something
        // this component enforces at pose time -- parenting under the envelope's non-uniform
        // Shield-prefab scale would skew every shard (§ Cut 3 "Scale must be 1").

        panel.ResetSim();
        panel.Hit(intercept, dir, energy, pattern);

        _live.Add(new LivePanel
        {
            Panel = panel,
            Proto = panelProto,
            Position = intercept,
            SpawnedAt = Time.time,
            DieAt = Time.time + panel.growDuration + panel.shardLife + DeathMargin
        });
    }

    int FindReusable(Vector3 point)
    {
        float threshold = PanelRadius * ReuseFraction;
        float threshold2 = threshold * threshold;
        int best = -1;
        float bestDist2 = float.MaxValue;
        for (int i = 0; i < _live.Count; i++)
        {
            float d2 = (_live[i].Position - point).sqrMagnitude;
            if (d2 < threshold2 && d2 < bestDist2)
            {
                bestDist2 = d2;
                best = i;
            }
        }
        return best;
    }

    int OldestIndex()
    {
        int oldest = 0;
        float oldestTime = float.MaxValue;
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i].SpawnedAt < oldestTime)
            {
                oldestTime = _live[i].SpawnedAt;
                oldest = i;
            }
        }
        return oldest;
    }

    FracturePattern PatternFor(DamageType t)
    {
        int i = (int)t;
        if (PatternByDamageType != null && i >= 0 && i < PatternByDamageType.Length)
            return PatternByDamageType[i];
        return FracturePattern.Radial;
    }
}
