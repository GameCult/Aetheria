/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// What lets a flight open fire. The flight reads it per track; nothing else does.
public enum EngageOn
{
    // Any hostile the flight's members can see.
    Detection,
    // A hostile whose gear one member has resolved (EntityInfoGathered at or past TargetGearInfoThreshold).
    Identified,
    // Only a hostile that a member holds a hostile IFF override on (a grudge).
    Provoked,
    // A hostile whose presence this faction's zone does not permit.
    Trespass,
    // Never, not even for a grudge.
    Never
}

// A faction's rules of engagement (operator ruling faction-doctrine-typed): flight-level fields plus one RoleDoctrine
// per role. Keys 5-8 are reserved for faction-play-2, keys 9-10 for faction-play-3. A faction with no doctrine gets
// Default, which fights as the agents did before doctrine existed.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FactionDoctrine
{
    [Inspectable, JsonProperty("engageOn"), Key(0)]
    public EngageOn EngageOn;

    // Seconds between a flight hailing a track and opening fire on it. Zero fires at once, with no hail.
    [Inspectable, JsonProperty("grace"), Key(1)]
    public float Grace;

    [Inspectable, JsonProperty("hail"), Key(2)]
    public string Hail;

    // A hailed track slower than this at the end of the grace counts as complying and is followed, not engaged.
    // Zero means nothing complies.
    [Inspectable, JsonProperty("complySpeed"), Key(3)]
    public float ComplySpeed;

    [Inspectable, JsonProperty("combatant"), Key(4)]
    public RoleDoctrine Combatant;

    public static FactionDoctrine Default(GameplaySettings settings) => new FactionDoctrine
    {
        EngageOn = EngageOn.Detection,
        Combatant = new RoleDoctrine
        {
            RangeExponent = settings.AgentRangeExponent,
            MinHitProbability = settings.AgentMinHitProbability
        }
    };
}

// How one role fights. Combatant is the only role so far.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class RoleDoctrine
{
    // Bias of the preferred engagement range towards longer ranges (CombatState's optimum range).
    [Inspectable, JsonProperty("rangeExponent"), Key(0)]
    public float RangeExponent;

    // A weapon fires only when its hit probability reaches this.
    [Inspectable, JsonProperty("minHitProbability"), Key(1)]
    public float MinHitProbability;

    // Distance a flight holds from a track it is hailing or following. Zero means 1.1 x the member's longest weapon range.
    [Inspectable, JsonProperty("holdStandoff"), Key(2)]
    public float HoldStandoff;
}
