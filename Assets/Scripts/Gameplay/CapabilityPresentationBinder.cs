using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

// Wires one CapabilityEvents source to whatever presenter components live on this object or its
// children, subscribing each presenter only to the event kinds it implements (docs/headless-
// playground-cut.md fork L: "presentation is chosen per item, independently of capability... one
// presentation... can visualize any subset of an item's capabilities"). This binder has no knowledge
// of FieldDriver or any other concrete presentation, and no knowledge of what publishes events --
// today the FieldShieldTest scene calls Bind() directly; a real capability owner will do the same
// later without this binder changing.
public class CapabilityPresentationBinder : MonoBehaviour
{
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
    private Func<int, Transform> _grabTargetResolver;
    private Func<int, Vector3> _grabInitialVelocityResolver;

    // Subscribes every presenter interface implemented by components on this object or its children to
    // the matching stream of `events`. `grabTargetResolver`/`grabInitialVelocityResolver` resolve a
    // GrabEvent's plain target handle to the live Transform/velocity a grab presenter needs; the owner
    // of `events` supplies them (the test scene today; the real pickup capability's caller later).
    public void Bind(CapabilityEvents events, Func<int, Transform> grabTargetResolver = null,
        Func<int, Vector3> grabInitialVelocityResolver = null)
    {
        Unbind();
        if (events == null) return;

        _grabTargetResolver = grabTargetResolver;
        _grabInitialVelocityResolver = grabInitialVelocityResolver;

        foreach (var presenter in GetComponentsInChildren<IAbsorbPresenter>(true))
        {
            var p = presenter;
            _subscriptions.Add(events.Absorb.Subscribe(e => p.Absorb(e)));
        }

        foreach (var presenter in GetComponentsInChildren<IGrabPresenter>(true))
        {
            var p = presenter;
            _subscriptions.Add(events.Grab.Subscribe(e =>
            {
                var target = _grabTargetResolver?.Invoke(e.TargetHandle);
                var velocity = _grabInitialVelocityResolver?.Invoke(e.TargetHandle) ?? Vector3.zero;
                p.Grab(e, target, velocity);
            }));
        }

        foreach (var presenter in GetComponentsInChildren<IMeleePresenter>(true))
        {
            var p = presenter;
            _subscriptions.Add(events.Melee.Subscribe(e => p.Melee(e)));
        }

        foreach (var presenter in GetComponentsInChildren<IThrustPresenter>(true))
        {
            var p = presenter;
            _subscriptions.Add(events.Thrust.Subscribe(e => p.Thrust(e)));
        }
    }

    public void Unbind()
    {
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        _grabTargetResolver = null;
        _grabInitialVelocityResolver = null;
    }

    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();
}
