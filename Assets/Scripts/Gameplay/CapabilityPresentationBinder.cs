using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

// Wires one CapabilityEvents source to whatever presenter components live on this object or its
// children, subscribing each presenter only to the event kinds it implements (docs/headless-
// playground-cut.md fork L: "presentation is chosen per item, independently of capability... one
// presentation... can visualize any subset of an item's capabilities"). This binder has no knowledge
// of FieldDriver or any other concrete presentation, and no knowledge of what publishes events --
// EntityInstance binds every entity's events, and the FieldShieldTest scene binds its stand-in sim's the same way.
public class CapabilityPresentationBinder : MonoBehaviour
{
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

    // Subscribes every presenter interface implemented by components on this object or its children to
    // the matching stream of `events`.
    public void Bind(CapabilityEvents events)
    {
        Unbind();
        if (events == null) return;

        foreach (var presenter in GetComponentsInChildren<IAbsorbPresenter>(true))
        {
            var p = presenter;
            _subscriptions.Add(events.Absorb.Subscribe(e => p.Absorb(e)));
        }

        foreach (var presenter in GetComponentsInChildren<IGrabPresenter>(true))
        {
            var p = presenter;
            _subscriptions.Add(events.Grab.Subscribe(e => p.Grab(e)));
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
    }

    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();
}
