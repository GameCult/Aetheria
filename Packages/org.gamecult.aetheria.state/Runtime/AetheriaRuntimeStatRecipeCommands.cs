namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeStatRecipeCommands
    {
        public const string SurfaceId = "aetheria.statRecipes";

        public const string Refresh = "aetheria.stat_recipes.refresh";
        public const string SelectStat = "aetheria.stat_recipes.select_stat";
        public const string AddStat = "aetheria.stat_recipes.add_stat";
        public const string RemoveStat = "aetheria.stat_recipes.remove_stat";
        public const string SetStatName = "aetheria.stat_recipes.set_stat_name";
        public const string SetBaseValue = "aetheria.stat_recipes.set_base_value";
        public const string ToggleCondition = "aetheria.stat_recipes.toggle_condition";
        public const string CycleInfluenceOperation = "aetheria.stat_recipes.cycle_influence_operation";
        public const string SetInfluenceAmount = "aetheria.stat_recipes.set_influence_amount";
        public const string SetInfluenceCurve = "aetheria.stat_recipes.set_influence_curve";
        public const string SetPreviewCondition = "aetheria.stat_recipes.set_preview_condition";

        public static bool IsKnown(string command)
        {
            switch (command ?? "")
            {
                case Refresh:
                case SelectStat:
                case AddStat:
                case RemoveStat:
                case SetStatName:
                case SetBaseValue:
                case ToggleCondition:
                case CycleInfluenceOperation:
                case SetInfluenceAmount:
                case SetInfluenceCurve:
                case SetPreviewCondition:
                    return true;
                default:
                    return false;
            }
        }
    }
}
