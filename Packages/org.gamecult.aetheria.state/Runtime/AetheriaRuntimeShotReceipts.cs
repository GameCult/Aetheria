using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeShotReceipts
    {
        public const int RetainedReceiptCount = 256;

        public static void Append(AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeShotReceiptCommit receipt)
        {
            if (run == null || receipt == null || string.IsNullOrWhiteSpace(receipt.ShotId)) return;
            var receipts = (run.ShotReceipts ?? Array.Empty<AetheriaRuntimeShotReceiptCommit>())
                .Where(value => value != null && !string.Equals(value.ShotId, receipt.ShotId, StringComparison.Ordinal))
                .Append(receipt)
                .OrderBy(value => value.FrameId)
                .ThenBy(value => value.ShotId, StringComparer.Ordinal)
                .ToArray();
            run.ShotReceipts = receipts.Length <= RetainedReceiptCount
                ? receipts
                : receipts.Skip(receipts.Length - RetainedReceiptCount).ToArray();
        }
    }
}
