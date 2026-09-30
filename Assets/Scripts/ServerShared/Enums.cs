/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

public enum ItemRotation
{
    None = 0,
    CounterClockwise = 1,
    Reversed = 2,
    Clockwise = 3
}

public enum CauseOfDeath
{
    HullDestroyed,
    CockpitDestroyed,
    Heatstroke,
    Hypothermia
}

public enum HitType
{
    Armor,
    Hardpoint,
    Gear,
    Thermal
}

public enum LauncherCaliber
{
    Micro,
    Missile,
    Fighter
}

public enum HardpointType
{
    Hull,
    Tool,
    Thermal,
    Thruster,
    WarpDrive,
    Reactor,
    Radiator,
    Shield,
    Sensors,
    Energy,
    Ballistic,
    Launcher,
    ControlModule,
    AetherDrive
}

public enum WeaponType
{
    ElectromagneticallyPropelled,
    ExplosivelyPropelled,
    Laser,
    Electrostatic,
    ParticleProjection,
    Missile,
    MicroMissile,
    SplitMissile,
    Mine,
    Jet
}

public enum WeaponRange
{
    Melee,
    Short,
    Medium,
    Long
}

public enum WeaponCaliber
{
    Small,
    Medium,
    Large,
    ExtraLarge
}

[Flags]
public enum WeaponFireType
{
    None = 0,
    Direct = 1 << 0,
    Guided = 1 << 1,
    Seeking = 1 << 2,
    Continuous = 1 << 3,
    Charged = 1 << 4
}

[Flags]
public enum WeaponModifiers
{
    None = 0,
    Airburst = 1 << 0,
    Incendiary = 1 << 1,
    ArmorPenetrating = 1 << 2,
    NegativeEntropy = 1 << 3,
    RapidFire = 1 << 4,
    Burst = 1 << 5,
    Cluster = 1 << 6
}

// Cut 12.4(a) (docs/fire-control-cut.md): what a weapon's blast radius (WeaponItemData.BlastRadius) detonates
// as. Null means no blast at all -- WeaponItemData.Fuse is frozen null unless BlastRadius > 0 (FireControl.Fire).
public enum WeaponFuse
{
    Contact,
    Proximity,
    Delayed
}

public enum DamageType
{
    Kinetic,
    Corrosive,
    Electric,
    Thermal,
    Optical,
    Ionizing
}

[Flags]
public enum BodyType
{
    None = 0,
    Asteroid = 1 << 0,
    Planetoid = 1 << 1,
    Planet = 1 << 2,
    GasGiant = 1 << 3,
    Sun = 1 << 4
}

public enum HullType
{
    Ship,
    Station,
    Turret
}

public enum MegaPlacementType
{
    Mass = 0,
    Planets = 1,
    Resources = 2,
    Connected = 3,
    Isolated = 4
}

public enum SimpleCommodityCategory
{
    Minerals = 0,
    Metals = 1,
    Alloys = 2,
    Compounds = 3,
    Organics = 4,
    Ammo = 5,
    Consumer = 6
}

public enum CompoundCommodityCategory
{
    Wearables = 0,
    Consumables = 1,
    Luxuries = 2,
    Tools = 3,
    Manufacturing = 4,
    Assemblies = 5
}

public enum LocationType
{
    Station,
    Asteroid,
    Planet
}

public enum FactionRelationship
{
    Hated = 0, // Kill on sight
    Hostile = 1, // Can enter open areas
    Neutral = 2, // Can dock in open areas, enter secure areas
    Friendly = 3, // Can dock in secure areas, enter critical areas
    Beloved = 4 // Cah dock even in critical areas
}

public enum SecurityLevel
{
    Open = 0,
    Secure = 1,
    Critical = 2
}

public enum TemperatureUnit
{
    Kelvin,
    Celsius,
    Fahrenheit
}

public enum WeaponAudioEvent
{
    Fire,
    Hit,
    Miss
}

public enum ChargedWeaponAudioEvent
{
    Start,
    Stop,
    Fail
}

public enum SpecialAudioParameter
{
    ShipVelocity,
    ChargeLevel,
    TargetLock,
    Intensity,
}

public enum LoopingAudioEvent
{
    Play,
    Stop
}

public enum MusicType
{
    Overworld,
    Combat,
    Boss
}

[Flags]
public enum BrushLayer
{
    Gravity = 1 << 0,
    Displacement = 1 << 1,
    FogHeight = 1 << 2,
    CloudHeight = 1 << 3,
    CloudDepth = 1 << 4
}