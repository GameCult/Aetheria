namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonStateRefs
    {
        public const string Prefix = "aetheria.daemon";

        public const string FrameDaemonId = Prefix + "/frame/daemonId";
        public const string FrameVerseId = Prefix + "/frame/verseId";
        public const string FrameId = Prefix + "/frame/frameId";
        public const string FrameTime = Prefix + "/frame/time";
        public const string FrameStatus = Prefix + "/frame/status";
        public const string FrameObservedCommands = Prefix + "/frame/observedCommands";
        public const string FrameAppliedCommands = Prefix + "/frame/appliedCommands";
        public const string FrameRejectedCommands = Prefix + "/frame/rejectedCommands";

        public const string CurrentRunId = Prefix + "/current/runId";
        public const string CurrentRunLifecycle = Prefix + "/current/runLifecycle";
        public const string CurrentRunTerminalReason = Prefix + "/current/runTerminalReason";
        public const string CurrentRunTerminalFrameId = Prefix + "/current/runTerminalFrameId";
        public const string CurrentZoneIndex = Prefix + "/current/zoneIndex";
        public const string CurrentEntityKey = Prefix + "/current/entityKey";
        public const string CurrentEntityName = Prefix + "/current/entityName";
        public const string CurrentEntityPosition = Prefix + "/current/entityPosition";
        public const string CurrentTargetName = Prefix + "/current/targetName";
        public const string CurrentEquipmentCount = Prefix + "/current/equipmentCount";
        public const string CurrentCargoBayCount = Prefix + "/current/cargoBayCount";
        public const string CurrentWeaponGroupCount = Prefix + "/current/weaponGroupCount";

        public const string CommandBoundaryId = Prefix + "/commands/boundaryId";
        public const string CommandCount = Prefix + "/commands/count";
    }
}
