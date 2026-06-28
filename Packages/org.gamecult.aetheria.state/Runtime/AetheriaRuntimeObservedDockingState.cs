using System;
using System.Linq;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeObservedDockingState
    {
        public AetheriaRuntimeObservedDockingState(
            AetheriaRuntimeCurrentEntityDocument? entity,
            AetheriaRuntimeCurrentDockingDocument docking,
            AetheriaRuntimeStationRefitDocument refit)
        {
            Entity = entity;
            Docking = docking ?? throw new ArgumentNullException(nameof(docking));
            Refit = refit ?? throw new ArgumentNullException(nameof(refit));
        }

        public AetheriaRuntimeCurrentEntityDocument? Entity { get; }
        public AetheriaRuntimeCurrentDockingDocument Docking { get; }
        public AetheriaRuntimeStationRefitDocument Refit { get; }
        public string CurrentEntityKey => !string.IsNullOrWhiteSpace(Entity?.EntityKey)
            ? Entity.EntityKey
            : Docking.CurrentEntityKey ?? "";
        public int CurrentEntityIndex => Entity?.EntityIndex >= 0
            ? Entity.EntityIndex
            : Docking.CurrentEntityIndex;
        public bool IsDocked => Docking.IsDocked || Refit.IsDocked;
        public string DockParentEntityKey => !string.IsNullOrWhiteSpace(Refit.DockParentEntityKey)
            ? Refit.DockParentEntityKey
            : Docking.DockParentEntityKey ?? "";
        public int DockingBayIndex => Refit.DockingBayIndex;

        public bool TryResolveCurrentDockingBayRow(
            out AetheriaRuntimeStationDockingBayRow? dockingBay)
        {
            dockingBay = null;
            if (!Refit.IsDocked || Refit.DockingBayIndex < 0)
                return false;

            dockingBay = (Refit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
                .FirstOrDefault(row => row != null && row.DockingBayIndex == Refit.DockingBayIndex);
            return dockingBay != null;
        }

        public static bool TryCreateCurrent(
            CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument>? entity,
            CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> docking,
            CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> refit,
            out AetheriaRuntimeObservedDockingState? observed)
        {
            observed = null;
            try
            {
                var currentDocking = docking?.Current;
                var currentRefit = refit?.Current;
                if (currentDocking == null || currentRefit == null)
                    return false;

                observed = new AetheriaRuntimeObservedDockingState(
                    entity?.Current,
                    currentDocking,
                    currentRefit);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
