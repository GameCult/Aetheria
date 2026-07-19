namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeTractorMechanics
    {
        public const double PowerRampPerSecond = 2;
        public const double ActivationThreshold = 0.01;
        public const double Radius = 25;
        public const double Traction = 25;
        public const double Distance = 75;
        // Collection is an XZ gameplay proximity, not a Ymir contact outcome.
        // The current 20-unit ship and 5-unit pickup circles touch at 25, so
        // keep the collection envelope outside that collision shell.
        public const double CollectionDistance = 26;
        public const double RejectionKick = 25;
    }
}
