/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public class OrbitalEntity : Entity
{
    public Guid OrbitData;
    public SecurityLevel SecurityLevel;
    public float SecurityRadius;
    public LocationStory Story;
    public bool CanTow;
    
    public OrbitalEntity(ItemManager itemManager, Zone zone, EquippableItem hull, Guid orbit, EntitySettings settings) : base(itemManager, zone, hull, settings)
    {
        OrbitData = orbit;
    }

    public override void Update(float delta)
    {
        if (OrbitData != Guid.Empty)
        {
            Position.xz = Zone.GetOrbitPosition(OrbitData);
            Velocity = Zone.GetOrbitVelocity(OrbitData);
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
