using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InstantWeaponEffectManager : MonoBehaviour
{
    public abstract void Fire(InstantWeapon weapon, EquippedItem item, EntityInstance source, EntityInstance target);
}