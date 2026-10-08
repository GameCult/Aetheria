using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;

// One flight per (zone, faction) of piloted ships; unaffiliated ships form one flight of their own. The flight alone
// decides what its members do: which hostile each engages (Ship.Target) and what each does otherwise (Agent.Task).
// The agent graph executes those two and decides nothing. The faction's doctrine (FactionDoctrine) holds the numbers.
//
// The picture is the union of the members' VisibleEnemies. A track passes through phases the flight owns, runtime only:
// None, Hailing (grace running, one hail spoken), Complied (slow enough at the grace's end: followed, not engaged) and
// Engaged. A member still fires only at what its own VisibleEntities hold (FireControl), so the shared picture never
// lets it fire at what it cannot see.
public class Flight
{
    public enum Phase { None, Hailing, Complied, Engaged }

    private class Track
    {
        public Phase Phase;
        public double Start;
    }

    private class Member
    {
        public Agent Agent;
        public PatrolOrbitsTask Patrol;
    }

    private readonly FactionDoctrine _doctrine;
    private readonly GameplaySettings _settings;
    private readonly List<Member> _members = new List<Member>();
    private readonly Dictionary<Entity, Track> _tracks = new Dictionary<Entity, Track>();
    private double _time;

    // No doctrine fights as FactionDoctrine.Default; a doctrine that leaves a role unauthored gets that role's default.
    public Flight(FactionDoctrine doctrine, GameplaySettings settings)
    {
        var fallback = FactionDoctrine.Default(settings);
        _doctrine = doctrine ?? fallback;
        _settings = settings;
        Combatant = _doctrine.Combatant ?? fallback.Combatant;
    }

    public RoleDoctrine Combatant { get; }
    public int Count => _members.Count;
    public bool Contains(Agent agent) => _members.Any(m => m.Agent == agent);

    public Phase PhaseOf(Entity track) => _tracks.TryGetValue(track, out var t) ? t.Phase : Phase.None;

    public void Join(Agent agent, PatrolOrbitsTask circuit)
    {
        _members.Add(new Member { Agent = agent, Patrol = circuit });
        agent.Task = circuit;
    }

    public void Leave(Agent agent) => _members.RemoveAll(m => m.Agent == agent);

    public void Update(float dt)
    {
        _time += dt;
        var picture = _members.SelectMany(m => m.Agent.Ship.VisibleEnemies).Distinct().ToList();
        foreach (var gone in _tracks.Keys.Where(t => !picture.Contains(t)).ToList()) _tracks.Remove(gone);

        foreach (var entity in picture)
        {
            if (!_tracks.TryGetValue(entity, out var track)) _tracks[entity] = track = new Track();
            Advance(entity, track);
        }

        foreach (var member in _members) Order(member, picture);
    }

    // A grudge is a hostile IFF override that any member holds on the track.
    private bool Grudged(Entity entity) => _members.Any(m => m.Agent.Ship.IffOverride(entity) == true);

    private bool Engageable(Entity entity, bool grudge)
    {
        switch (_doctrine.EngageOn)
        {
            case EngageOn.Never: return false;
            case EngageOn.Detection: return true;
            case EngageOn.Identified:
                return grudge || _members.Any(m => m.Agent.Ship.EntityInfoGathered.TryGetValue(entity, out var info) &&
                                                   info >= _settings.TargetGearInfoThreshold);
            case EngageOn.Provoked: return grudge;
            case EngageOn.Trespass: return grudge || !(entity.PresencePermitted?.Value ?? true);
            default: return false;
        }
    }

    private void Advance(Entity entity, Track track)
    {
        if (track.Phase == Phase.Engaged) return;
        var grudge = Grudged(entity);
        if (!Engageable(entity, grudge))
        {
            track.Phase = Phase.None;
            return;
        }

        // A grudge is already a quarrel: no grace to give.
        if (grudge)
        {
            track.Phase = Phase.Engaged;
            return;
        }

        switch (track.Phase)
        {
            case Phase.None:
                if (_doctrine.Grace > 0)
                {
                    track.Phase = Phase.Hailing;
                    track.Start = _time;
                    if (!string.IsNullOrEmpty(_doctrine.Hail)) Nearest(entity)?.Agent.Ship.SetMessage(_doctrine.Hail);
                }
                else track.Phase = Phase.Engaged;
                break;
            case Phase.Hailing:
                if (_time >= track.Start + _doctrine.Grace)
                    track.Phase = _doctrine.ComplySpeed > 0 && length(entity.Velocity) < _doctrine.ComplySpeed
                        ? Phase.Complied
                        : Phase.Engaged;
                break;
            case Phase.Complied:
                if (length(entity.Velocity) >= _doctrine.ComplySpeed) track.Phase = Phase.Engaged;
                break;
        }
    }

    private Member Nearest(Entity entity) =>
        _members.OrderBy(m => lengthsq(m.Agent.Ship.Position.xz - entity.Position.xz)).FirstOrDefault();

    private void Order(Member member, List<Entity> picture)
    {
        var ship = member.Agent.Ship;
        var seen = ship.VisibleEnemies;

        // Engage: keep the current target while it stays engaged, else the nearest engaged track this member can see.
        var engaged = picture.Where(e => _tracks[e].Phase == Phase.Engaged && seen.Contains(e)).ToList();
        if (engaged.Count > 0)
        {
            var current = ship.Target.Value.Entity;
            var pick = current != null && engaged.Contains(current)
                ? current
                : engaged.OrderBy(e => lengthsq(ship.Position.xz - e.Position.xz)).First();
            Aim(ship, pick);
            return;
        }

        Aim(ship, null);
        var held = picture.Where(e => _tracks[e].Phase == Phase.Hailing || _tracks[e].Phase == Phase.Complied).ToList();
        if (held.Count > 0)
        {
            var anchor = member.Agent.Task is FollowTask follow && held.Contains(follow.Anchor)
                ? follow.Anchor
                : held.OrderBy(e => lengthsq(ship.Position.xz - e.Position.xz)).First();
            if (!(member.Agent.Task is FollowTask current) || current.Anchor != anchor)
                member.Agent.Task = new FollowTask { Anchor = anchor, Standoff = Standoff(ship) };
        }
        else if (member.Agent.Task != member.Patrol)
            member.Agent.Task = member.Patrol;
    }

    private static void Aim(Ship ship, Entity target)
    {
        var wanted = target == null ? TargetRef.None : new TargetRef(target);
        if (!ship.Target.Value.Equals(wanted)) ship.SetTarget(wanted);
    }

    private float Standoff(Ship ship)
    {
        if (Combatant.HoldStandoff > 0) return Combatant.HoldStandoff;
        return 1.1f * ship.Weapons.Select(w => w.Range).DefaultIfEmpty(0f).Max();
    }
}
