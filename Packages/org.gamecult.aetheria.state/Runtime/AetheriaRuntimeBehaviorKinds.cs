using System;
using System.Collections.Generic;

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeBehaviorKinds
    {
        public const string StatModifier = "StatModifier";
        public const string GuidedWeapon = "GuidedWeapon";
        public const string Launcher = "Launcher";
        public const string InstantWeapon = "InstantWeapon";
        public const string ConstantWeapon = "ConstantWeapon";
        public const string ChargedWeapon = "ChargedWeapon";
        public const string AutoWeapon = "AutoWeapon";
        public const string DeployableWeapon = "DeployableWeapon";
        public const string AetherDrive = "AetherDrive";
        public const string Thruster = "Thruster";
        public const string VelocityConversion = "VelocityConversion";
        public const string VelocityLimit = "VelocityLimit";
        public const string Reflector = "Reflector";
        public const string Visibility = "Visibility";

        private static readonly ISet<string> WeaponKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            GuidedWeapon,
            Launcher,
            InstantWeapon,
            ConstantWeapon,
            ChargedWeapon,
            AutoWeapon,
            DeployableWeapon
        };

        public static bool IsWeapon(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind) && WeaponKinds.Contains(kind);
        }
    }
}
