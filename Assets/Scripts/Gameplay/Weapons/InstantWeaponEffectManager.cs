using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InstantWeaponEffectManager : MonoBehaviour
{
    // Cut 3 (docs/fire-control-cut.md): shotId is FireControl.Fire's ShotId (docs/fire-control-cut.md, 0b
    // table) -- the presentation's handle onto Zone.ShotCommitted / ShotResolved for this specific shot.
    // Implementations apply nothing; they perform whatever the shot's outcome says once it resolves.
    public abstract void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target, int shotId);
}