/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// A ship touching a ship (aetheria-release cut sim-contact; ruling unity-presents-only-r3): the simulation owns
// contact between ships. Orbital entities (stations, turrets) sit on the orbital layer above the ship plane and floaters
// below it, so neither touches a ship (ruling aetheria-gravity:orbitals-above-floaters-below). Each hull is a disc about its centre of mass that bounds every occupied cell, and
// two discs that touch while closing along the line between their centres take the normal impulse
// j = -(1 + e)(v_rel . n) / (1/m1 + 1/m2) (ruling ship-bounce-restitution-setting): momentum is conserved, the
// result is frame-independent, and a pair that has separated is not bounced again. e is
// GameplaySettings.ContactRestitution.
// Presentations read Zone.Contacted; no Unity collision callback writes an Entity.
public readonly struct ContactEvent
{
    public readonly Entity A;
    public readonly Entity B;
    public readonly float2 Point;
    // Unit vector from A to B.
    public readonly float2 Normal;

    public ContactEvent(Entity a, Entity b, float2 point, float2 normal)
    {
        A = a;
        B = b;
        Point = point;
        Normal = normal;
    }
}

public static class Contacts
{
    // The farthest corner of any occupied hull cell from the centre of mass the schematic frame uses, in world
    // units: a true bound on how far the hull reaches from Position (the half-diagonal of the bounding box is not,
    // as FireControl.Detonate's comment records). A cell at (x, y) spans x +/- .5 and y +/- .5.
    public static float Radius(Entity e)
    {
        var shape = e.HullData.Shape;
        var com = shape.CenterOfMass;
        var far = 0f;
        foreach (var c in shape.Coordinates)
            foreach (var (dx, dy) in new[] { (-.5f, -.5f), (.5f, -.5f), (-.5f, .5f), (.5f, .5f) })
                far = max(far, length(float2(c.x + dx, c.y + dy) - com));
        return far * e.ItemManager.GameplaySettings.SchematicCellSize;
    }

    // For each unordered pair of the zone's ships, in list order: if their discs touch and are closing along
    // the normal, the impulse moves each one's velocity along the normal only (the tangent components are
    // untouched), and the contact is published. Pairs resolve one after another in list order, each from the
    // velocities the earlier pairs left, so a body touching two others in one tick depends on that order; the
    // total momentum does not. A docked entity (it has a Parent and takes its parent's pose) and an inactive one
    // are not hulls in the zone. No positional push-out.
    public static void Step(Zone zone)
    {
        var entities = zone.Entities.OfType<Ship>().Where(e => e.Active && e.Parent == null).ToArray();
        var radii = entities.Select(Radius).ToArray();
        for (var i = 0; i < entities.Length; i++)
            for (var j = i + 1; j < entities.Length; j++)
            {
                var a = entities[i];
                var b = entities[j];
                var offset = b.Position.xz - a.Position.xz;
                var distance = length(offset);
                // Coincident centres have no normal.
                if (distance <= 0f || distance >= radii[i] + radii[j]) continue;
                var n = offset / distance;
                var closing = dot(n, b.Velocity - a.Velocity);
                if (closing >= 0f) continue;

                var impulse = -(1f + a.ItemManager.GameplaySettings.ContactRestitution) * closing / (1f / a.Mass + 1f / b.Mass);
                a.Velocity -= n * (impulse / a.Mass);
                b.Velocity += n * (impulse / b.Mass);
                zone.Contacted.OnNext(new ContactEvent(a, b, a.Position.xz + n * radii[i], n));
            }
    }
}
