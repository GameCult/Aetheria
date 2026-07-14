using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimePickupContactReceipts
    {
        public static bool Contains(AetheriaRuntimeRunCheckpointCommit run, string factId)
        {
            return Find(run, factId) != null;
        }

        public static AetheriaRuntimePickupContactReceiptCommit? Find(
            AetheriaRuntimeRunCheckpointCommit run,
            string factId) =>
            run == null || string.IsNullOrWhiteSpace(factId)
                ? null
                : (run.PickupContactReceipts ?? Array.Empty<AetheriaRuntimePickupContactReceiptCommit>())
                .FirstOrDefault(value => value != null &&
                    string.Equals(value.FactId, factId, StringComparison.Ordinal));

        public static void Append(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimePickupContactReceiptCommit receipt)
        {
            if (run == null || receipt == null || string.IsNullOrWhiteSpace(receipt.FactId))
                throw new ArgumentException("Pickup contact receipts require a Ymir fact id.", nameof(receipt));
            if (Contains(run, receipt.FactId))
                return;

            run.PickupContactReceipts =
                (run.PickupContactReceipts ?? Array.Empty<AetheriaRuntimePickupContactReceiptCommit>())
                .Append(receipt)
                .ToArray();
        }
    }
}
