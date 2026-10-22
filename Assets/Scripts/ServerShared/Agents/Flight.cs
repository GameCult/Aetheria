using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;
using ContactId = PilotView.ContactId;

// One flight per (zone, side) of piloted ships; a side is a faction, or one unaffiliated ship. The flight alone
// decides what its members do: which hostile each engages (Ship.Target) and what each does otherwise (Agent.Task).
// The agent graph executes those two and decides nothing. The faction's doctrine (FactionDoctrine) holds the numbers.
//
// The flight reads other ships only through its side's PilotView (ruling observation-sensors-only): it holds a contact
// as a handle and never as the ship. The picture is the view's: the union of what the members' sensors hold hostile, never a member.
// A track passes through phases the flight owns, runtime only: None, Hailing (grace running, one hail spoken), Complied
// (slow enough at the grace's end: followed, not engaged) and Engaged. A member still fires only at what its own
// sensors hold (fire control), so the shared picture never lets it fire at what it cannot see.
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
        public AgentTask Baseline;
    }

    private readonly FactionDoctrine _doctrine;
    private readonly GameplaySettings _settings;
    private readonly List<Member> _members = new List<Member>();
    private readonly Dictionary<ContactId, Track> _tracks = new Dictionary<ContactId, Track>();
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
    // What this side knows of other ships; every member's Agent.View.
    public PilotView View { get; } = new PilotView();
    public int Count => _members.Count;
    public bool Contains(Agent agent) => _members.Any(m => m.Agent == agent);

    public Phase PhaseOf(ContactId track) => _tracks.TryGetValue(track, out var t) ? t.Phase : Phase.None;

    // The task a member holds when the flight has nothing to follow: its patrol circuit, or the task a scenario stated.
    public void Join(Agent agent, AgentTask baseline)
    {
        _members.Add(new Member { Agent = agent, Baseline = baseline });
        View.Add(agent.Ship);
        agent.View = View;
        agent.Task = baseline;
    }

    public void Leave(Agent agent)
    {
        if (_members.RemoveAll(m => m.Agent == agent) > 0) View.Remove(agent.Ship);
    }

    public void Update(float dt)
    {
        _time += dt;
        var picture = View.Picture();
        foreach (var gone in _tracks.Keys.Where(t => !picture.Contains(t)).ToList()) _tracks.Remove(gone);

        foreach (var contact in picture)
        {
            if (!_tracks.TryGetValue(contact, out var track)) _tracks[contact] = track = new Track();
            Advance(contact, track);
        }

        foreach (var member in _members) Order(member, picture);
    }

    // A grudge is a hostile IFF override that any member holds on the track.
    private bool Grudged(ContactId contact) => _members.Any(m => View.IffOverride(m.Agent.Ship, contact) == true);

    private bool Engageable(ContactId contact, bool grudge)
    {
        switch (_doctrine.EngageOn)
        {
            case EngageOn.Never: return false;
            case EngageOn.Detection: return true;
            case EngageOn.Identified:
                return grudge || _members.Any(m => View.Info(m.Agent.Ship, contact) >= _settings.TargetGearInfoThreshold);
            case EngageOn.Provoked: return grudge;
            case EngageOn.Trespass: return grudge || _members.Any(m => View.BelievesTrespassing(m.Agent.Ship, contact));
            default: return false;
        }
    }

    private void Advance(ContactId contact, Track track)
    {
        if (track.Phase == Phase.Engaged) return;
        var grudge = Grudged(contact);
        if (!Engageable(contact, grudge))
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
                    if (!string.IsNullOrEmpty(_doctrine.Hail)) Nearest(contact)?.Agent.Ship.Command.Hail(_doctrine.Hail);
                }
                else track.Phase = Phase.Engaged;
                break;
            case Phase.Hailing:
                if (_time >= track.Start + _doctrine.Grace)
                    track.Phase = _doctrine.ComplySpeed > 0 && length(View.SideTrack(contact).Value.Velocity) < _doctrine.ComplySpeed
                        ? Phase.Complied
                        : Phase.Engaged;
                break;
            case Phase.Complied:
                if (length(View.SideTrack(contact).Value.Velocity) >= _doctrine.ComplySpeed) track.Phase = Phase.Engaged;
                break;
        }
    }

    private Member Nearest(ContactId contact)
    {
        var at = View.SideTrack(contact).Value.Position.xz;
        return _members.OrderBy(m => lengthsq(m.Agent.Ship.Position.xz - at)).FirstOrDefault();
    }

    private void Order(Member member, List<ContactId> picture)
    {
        var ship = member.Agent.Ship;

        // Engage: keep the current target while it stays engaged, else the nearest engaged track this member can see.
        var engaged = picture.Where(c => _tracks[c].Phase == Phase.Engaged && View.Sees(ship, c)).ToList();
        if (engaged.Count > 0)
        {
            var current = View.AppliedTargetOf(ship);
            var pick = current != null && engaged.Contains(current.Value)
                ? current.Value
                : engaged.OrderBy(c => lengthsq(ship.Position.xz - View.SideTrack(c).Value.Position.xz)).First();
            View.Designate(ship, pick);
            return;
        }

        // No engaged track in this member's own sensors: it steers on the flight's picture, a bearing and never targeting
        // data (ruling shared-track-bearing-only). That is the hailed and the complied, and an engaged track only a flightmate sees.
        View.Designate(ship, null);
        // Overrides are per member: a flightmate's hostile track is not one this member follows after it has made it friendly.
        var held = picture.Where(c => _tracks[c].Phase != Phase.None && View.IffOverride(ship, c) != false).ToList();
        if (held.Count > 0)
        {
            // An engaged track outranks a hailed one; the held anchor is kept only while nothing outranks it.
            var anyEngaged = held.Any(c => _tracks[c].Phase == Phase.Engaged);
            var heldAnchor = (member.Agent.Task as FollowTask)?.Anchor;
            var anchor = heldAnchor != null && held.Contains(heldAnchor.Value)
                         && (!anyEngaged || _tracks[heldAnchor.Value].Phase == Phase.Engaged)
                ? heldAnchor.Value
                : held.OrderBy(c => _tracks[c].Phase == Phase.Engaged ? 0 : 1)
                    .ThenBy(c => lengthsq(ship.Position.xz - View.SideTrack(c).Value.Position.xz)).First();
            if (!(member.Agent.Task is FollowTask current) || current.Anchor != anchor)
                member.Agent.Task = new FollowTask { Anchor = anchor, Standoff = Standoff(ship) };
        }
        else if (member.Agent.Task != member.Baseline)
            member.Agent.Task = member.Baseline;
    }

    private float Standoff(Ship ship)
    {
        if (Combatant.HoldStandoff > 0) return Combatant.HoldStandoff;
        return 1.1f * ship.Weapons.Select(w => w.Range).DefaultIfEmpty(0f).Max();
    }
}
