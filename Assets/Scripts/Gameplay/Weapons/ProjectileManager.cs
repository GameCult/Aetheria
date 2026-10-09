using UnityEngine;

public class ProjectileManager : InstantWeaponEffectManager
{
    public Prototype ProjectilePrototype;

    // The round is drawn on the line the simulation flies it (FireControl.RoundAt), so there is nothing to aim, jitter
    // or inherit here: spread is priced by FireControl, and the sim's round inherits nothing from its shooter.
    public override void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target, int shotId)
    {
        var zone = source.Entity.Zone;
        if (!zone.TryGetShot(shotId, out var shot)) return;
        var p = ProjectilePrototype.Instantiate<Projectile>();
        p.Launch(zone, shot, source.GetBarrel(item.Hardpoint).position);
        p.Trail.Clear();
    }
}
