/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public class OrbitalEntity : Entity
{
    public string OrbitKey = "";
    public SecurityLevel SecurityLevel;
    public float SecurityRadius;
    public LocationStory Story;
    public bool CanTow;
    
    public OrbitalEntity(ItemManager itemManager, Zone zone, EquippableItem hull, string orbitKey, EntitySettings settings) : base(itemManager, zone, hull, settings)
    {
        OrbitKey = orbitKey ?? "";
    }

    public override void Update(float delta)
    {
        if (!string.IsNullOrWhiteSpace(OrbitKey))
        {
            Position.xz = Zone.GetOrbitPosition(OrbitKey);
            Velocity = Zone.GetOrbitVelocity(OrbitKey);
        }
        
        base.Update(delta);
    }

    public bool IsSecureArea
    {
        get
        {
            if (SecurityRadius < 1) return false;
            if (Faction == null) return false;
            return !IsPresencePermitted(Zone.Galaxy.FactionRelationships[Faction], SecurityLevel);
        }
    }
}
