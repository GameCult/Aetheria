/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// A hull touching a hull (aetheria-release cut sim-contact; ruling unity-presents-only-r3): the simulation owns
// contact between entities. Each hull is a disc about its centre of mass that bounds every occupied cell, and
// two discs that touch while closing along the line between their centres exchange their normal velocity.
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

    // The elastic exchange of one body's normal velocity: v1 and m1 the body's own, v2 and m2 the other's.
    private static float PostCollisionVelocity(float v1, float m1, float v2, float m2)
    {
        return (v1 * (m1 - m2) + m2 * v2) / (m1 + m2);
    }

    // For each unordered pair of the zone's entities, in list order: if their discs touch and are closing along
    // the normal, each one's normal velocity is replaced (both from the velocities the pair had on entering) and
    // its tangent component kept, and the contact is published. A docked entity (it has a Parent and takes its
    // parent's pose) and an inactive one are not hulls in the zone. No positional push-out.
    public static void Step(Zone zone)
    {
        var entities = zone.Entities.Where(e => e.Active && e.Parent == null).ToArray();
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
                var va = dot(n, a.Velocity);
                var vb = dot(n, b.Velocity);
                if (vb - va >= 0f) continue;

                var tangent = float2(-n.y, n.x);
                var vaNew = PostCollisionVelocity(va, a.Mass, vb, b.Mass);
                var vbNew = PostCollisionVelocity(vb, b.Mass, va, a.Mass);
                a.Velocity = n * vaNew + tangent * dot(tangent, a.Velocity);
                b.Velocity = n * vbNew + tangent * dot(tangent, b.Velocity);
                zone.Contacted.OnNext(new ContactEvent(a, b, a.Position.xz + n * radii[i], n));
            }
    }
}
