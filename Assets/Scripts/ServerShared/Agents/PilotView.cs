using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CultMath;

// What one side's brain may know of other ships. A side is a faction, or one unaffiliated ship (brain-one-per-side-r2);
// its view reads only that side's members: their sensors (VisibleEntities, VisibleEnemies, EntityInfoGathered), their
// IFF overrides and reveal books (ruling observation-sensors-only). A brain never holds another ship: it holds a
// ContactId or a RevealId, which only the view that issued it can resolve, so there is no path from a handle to the
// ship's truth that does not pass the sensors.
//
// A contact's Track (position, velocity, facing) exists only while a member tracks it (its VisibleEntities holds it):
// within a zone step nothing moves during the agent phase, so the track equals what the Entity would answer. The
// fire-control readouts call FireControl's own functions with the resolved subject, so the HUD, the player and the
// brains price one model (A-one-sim). Own-ship reads stay the brain's own.
//
// The view enforces its own rules; it does not trust its callers. Every per-member read answers for a member the view
// holds and nothing else, and every per-contact fire-control readout (Intercept, WeaponReveals, Designated, Refuses,
// Fires) answers only while that member itself tracks the contact: a sidemate's track gives a member no aim, no reveals
// and no shot (per-shooter-fire-control). Only Flight builds a view and sets it on its pilots, and the side-of-one view an
// Agent starts with is its own; the constructor and the setter are internal.
public sealed class PilotView
{
    private readonly List<Ship> _members = new List<Ship>();
    private static Func<ContactId, Entity> _openContact;
    private static Func<RevealId, EquippedItem> _openReveal;

    internal PilotView(params Ship[] members) => _members.AddRange(members);

    private bool Holds(Ship member) => _members.Contains(member);
    private bool Tracks(Ship member, Entity subject) => Holds(member) && subject != null && member.VisibleEntities.Contains(subject);

    internal void Add(Ship member) => _members.Add(member);
    internal void Remove(Ship member) => _members.Remove(member);

    // A contact of the view, equal to itself across absences from a member's picture: equality is by reference.
    public readonly struct ContactId : IEquatable<ContactId>
    {
        private readonly Entity _subject;

        static ContactId() => _openContact = id => id._subject;
        internal ContactId(Entity subject) => _subject = subject;

        public bool Equals(ContactId other) => ReferenceEquals(_subject, other._subject);
        public override bool Equals(object obj) => obj is ContactId other && Equals(other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(_subject);
        public static bool operator ==(ContactId a, ContactId b) => a.Equals(b);
        public static bool operator !=(ContactId a, ContactId b) => !a.Equals(b);
    }

    // A revealed item of a contact, to aim at; the live item stays behind the handle (ruling contact-reads-book-aim-reads-live).
    public readonly struct RevealId : IEquatable<RevealId>
    {
        private readonly EquippedItem _item;

        static RevealId() => _openReveal = id => id._item;
        internal RevealId(EquippedItem item) => _item = item;

        public bool Equals(RevealId other) => ReferenceEquals(_item, other._item);
        public override bool Equals(object obj) => obj is RevealId other && Equals(other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(_item);
        public static bool operator ==(RevealId a, RevealId b) => a.Equals(b);
        public static bool operator !=(RevealId a, RevealId b) => !a.Equals(b);
    }

    public readonly struct Track
    {
        public readonly float3 Position;
        public readonly float2 Velocity;
        public readonly float2 Direction;

        public Track(float3 position, float2 velocity, float2 direction)
        {
            Position = position;
            Velocity = velocity;
            Direction = direction;
        }
    }

    // For fixtures, and for the one place the stage states what a pilot already knows (ScenarioStage.Approach seeds an
    // anchor handle for a FollowTask before the zone exists: staged knowledge, ruling stage-states-pilot-knowledge). A
    // brain never calls this; the sensors, not the handle, decide whether the view answers about the contact.
    internal static ContactId Handle(Entity subject) => new ContactId(subject);

    private static Entity Subject(ContactId id)
    {
        if (_openContact == null) RuntimeHelpers.RunClassConstructor(typeof(ContactId).TypeHandle);
        return _openContact(id);
    }

    private static EquippedItem Item(RevealId id)
    {
        if (_openReveal == null) RuntimeHelpers.RunClassConstructor(typeof(RevealId).TypeHandle);
        return _openReveal(id);
    }

    // The side's contacts: its members' VisibleEnemies in member order, each once, never a member of the side.
    public List<ContactId> Picture()
    {
        var picture = new List<ContactId>();
        var seen = new HashSet<Entity>();
        foreach (var member in _members)
            foreach (var enemy in member.VisibleEnemies)
                if (!_members.Any(m => ReferenceEquals(m, enemy)) && seen.Add(enemy)) picture.Add(new ContactId(enemy));
        return picture;
    }

    public bool Sees(Ship member, ContactId contact) => Holds(member) && member.VisibleEnemies.Contains(Subject(contact));

    // The member's own track of the contact, or null while it does not track it.
    public Track? TrackOf(Ship member, ContactId contact)
    {
        var subject = Subject(contact);
        if (!Tracks(member, subject)) return null;
        return new Track(subject.Position, subject.Velocity, subject.Direction);
    }

    // The track of the first member, in order, that tracks the contact (question side-shared-kinematics).
    public Track? SideTrack(ContactId contact)
    {
        foreach (var member in _members)
        {
            var track = TrackOf(member, contact);
            if (track != null) return track;
        }

        return null;
    }

    public bool? IffOverride(Ship member, ContactId contact) => Holds(member) ? member.IffOverride(Subject(contact)) : null;

    public float? Info(Ship member, ContactId contact) =>
        Holds(member) && member.EntityInfoGathered.TryGetValue(Subject(contact), out var info) ? info : (float?) null;

    public bool BelievesTrespassing(Ship member, ContactId contact) => Holds(member) && member.BelievesTrespassing(Subject(contact));

    // The member's pending designation when set, else last step's applied target.
    public ContactId? TargetOf(Ship member)
    {
        if (!Holds(member)) return null;
        var subject = (member.Command.Designated ?? member.Target.Value).Entity;
        return subject == null ? (ContactId?) null : new ContactId(subject);
    }

    public ContactId? AppliedTargetOf(Ship member)
    {
        if (!Holds(member)) return null;
        var subject = member.Target.Value.Entity;
        return subject == null ? (ContactId?) null : new ContactId(subject);
    }

    // The weapons the member believes the contact has, in the contact's equipment order; Aimable is FireControl.IsRevealed.
    // Empty unless the member tracks the contact.
    public List<(RevealId Id, EquippableItemData Claimed, bool Aimable)> WeaponReveals(Ship member, ContactId contact)
    {
        var subject = Subject(contact);
        if (!Tracks(member, subject)) return new List<(RevealId, EquippableItemData, bool)>();
        return member.RevealedWeapons(subject)
            .Select(r => (new RevealId(r.Item), r.Claimed, FireControl.IsRevealed(member, r.Item))).ToList();
    }

    // The next four answer for a member that tracks the contact: otherwise not designated, refused, no shot, no intercept.
    public bool Designated(Ship member, Weapon weapon, ContactId contact)
    {
        var subject = Subject(contact);
        return Tracks(member, subject) && FireControl.Designated(weapon, member, subject, out _);
    }

    public bool Refuses(Ship member, Weapon weapon, ContactId contact)
    {
        var subject = Subject(contact);
        return !Tracks(member, subject) || FireControl.Refuses(weapon, member, subject);
    }

    public bool Fires(Ship member, Weapon weapon, ContactId contact, float minHitProbability, RevealId? aim)
    {
        var subject = Subject(contact);
        return Tracks(member, subject) && FireControl.AgentFires(weapon, member, subject, minHitProbability,
            aim == null ? AimTarget.None : AimTarget.OfItem(Item(aim.Value)));
    }

    // Where to point to hit the contact with the weapon: the shared prediction, at the height the contact rides; null
    // unless the member tracks the contact.
    public float3? Intercept(Ship member, Weapon weapon, ContactId contact)
    {
        var subject = Subject(contact);
        if (!Tracks(member, subject)) return null;
        var predicted = FireControl.PredictedIntercept(weapon, member, subject);
        predicted.y = subject.RideHeight(predicted.xz);
        return predicted;
    }

    // Designate the contact, or nothing for null, when that is not already the member's applied target.
    public void Designate(Ship member, ContactId? contact)
    {
        if (!Holds(member)) return;
        var wanted = contact == null ? TargetRef.None : new TargetRef(Subject(contact.Value));
        if (!member.Target.Value.Equals(wanted)) member.Command.Designate(wanted);
    }

    public void SelectItem(Ship member, RevealId? reveal)
    {
        if (Holds(member)) member.Command.SelectItem(reveal == null ? null : Item(reveal.Value));
    }
}
