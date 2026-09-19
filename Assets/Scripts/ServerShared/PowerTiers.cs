/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

// Cut 5 (docs/stats-and-power-cut.md §1.3, Q5: "four or five tiers, defaulted per behaviour kind"). Five tiers,
// numbered so the lowest number is fed first -- PowerBus's allocation pass walks 0..Count-1 in order. Naming
// them here, once, means IPowerConsumer.DefaultPowerTier implementations read as a sentence ("Radiator is
// Critical") instead of a bare integer repeated eight times.
//
// Assignment, per behaviour kind, and why:
// - Critical: Radiator. This game has no dedicated life-support behaviour, and heat is the closest thing to
//   one -- a starved radiator does not just fail itself, it cascades into wear and eventual shutdown across
//   every other item on the hull (EquippedItem.UpdatePerformance). Feed it first or lose the rest anyway.
// - High: Shield. Hull survival the player is actively trading power for; not a life-support constant, so it
//   sits below Radiator rather than beside it.
// - Medium: Thruster, AetherDrive. Mobility -- needed to hold position, evade, or disengage, but a ship that
//   loses thrust for a tick under brownout is inconvenienced, not dead the way an overheating hull is.
// - Low: ConstantWeapon, InstantWeapon. Offense. The ruling's own framing ("low-priority subsystems starve
//   first") names weapons as the default example of what a reactor throttle should sacrifice first.
// - Utility: Sensor, EnergyDraw. Everything else, including the generic/unclassified draw -- an item that
//   declares power use without asking for a specific tier should not accidentally outrank a named priority.
//
// A tier is a stored player choice once assigned (EquippableItem.PowerTier), not a derivation the bus repeats
// every tick (§1.3 "Derived state: nothing"). These defaults are read exactly once, in EquippedItem's
// constructor, to seed that stored field the first time an item without a chosen tier gets equipped.
public static class PowerTiers
{
    public const int Critical = 0;
    public const int High = 1;
    public const int Medium = 2;
    public const int Low = 3;
    public const int Utility = 4;

    public const int Count = 5;

    // EquippableItem.PowerTier's sentinel for "the player has not chosen one and this item has never been
    // equipped yet" -- distinct from 0 (Critical), which is a real, meaningful choice.
    public const int Unassigned = -1;
}
