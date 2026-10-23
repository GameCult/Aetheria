using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class ProjectileManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    // The rounds this manager has in the air by shot id, told of the sim's commit and verdict by one subscription per zone: a
    // gun that fires hundreds of rounds in a step costs the dictionary an entry per round and nothing else (no subscription,
    // closure or disposable per round).
    private readonly Dictionary<int, Projectile> _live = new Dictionary<int, Projectile>();
    private readonly CompositeDisposable _listening = new CompositeDisposable();
    private Zone _zone;

    // The round is drawn by DrawAhead from what the simulation publishes, so there is nothing to aim, jitter
    // or inherit here: spread is priced by FireControl, and the sim's round inherits nothing from its shooter.
    // A round the sim fired inside the step (rounds per step) is first seen where its shooter's barrel was at that moment:
    // the barrel as it is now, carried back along the shooter's motion for the time since.
    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target, int shotId)
    {
        var zone = source.Entity.Zone;
        if (!zone.TryGetShot(shotId, out var shot)) return;
        Listen(zone);
        var age = (float) (zone.ExactTime - shot.FireTime);
        var velocity = source.Entity.Velocity;
        var p = ProjectilePrototype.Instantiate<Projectile>();
        _live[shotId] = p;
        p.Launch(this, zone, shot, source.GetBarrel(item.Hardpoint).position - new Vector3(velocity.x, 0, velocity.y) * age);
        p.Trail.Clear();
    }

    internal void Forget(int shotId) => _live.Remove(shotId);

    private void Listen(Zone zone)
    {
        if (_zone == zone) return;
        _listening.Clear();
        _live.Clear();
        _zone = zone;
        zone.ShotCommitted.Subscribe(outcome => { if (_live.TryGetValue(outcome.ShotId, out var p)) p.Committed(outcome); }).AddTo(_listening);
        zone.ShotResolved.Subscribe(outcome => { if (_live.TryGetValue(outcome.ShotId, out var p)) p.Resolve(outcome); }).AddTo(_listening);
    }

    void OnDestroy() => _listening.Dispose();
}
