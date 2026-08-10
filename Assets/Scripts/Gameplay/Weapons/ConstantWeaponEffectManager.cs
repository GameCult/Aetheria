using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ConstantWeaponEffectManager : MonoBehaviour
{
    public abstract void StartFiring(WeaponData data, EquippedItem item, EntityInstance source, EntityInstance target);
    public abstract void StopFiring(EquippedItem item);
}
