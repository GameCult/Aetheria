using System;

// The one-shot catalog commands (pd-gear, launch-suppressor, inspection-scanner, ...) author gear at the prices they were written
// with, a hundred times the shipped catalog's (cut gear-money-rescale: Equippable and Consumable prices were divided by 100 once and
// the commands keep their literals). A test that sets a command's landed design against the shipped record compares the price
// through this, the transform the rescale applied: half away from zero, never below 1.
internal static class GearPrices
{
    public const int Divisor = 100;

    public static int Rescaled(int price) => Math.Max(1, (int) Math.Round(price / (double) Divisor, MidpointRounding.AwayFromZero));
}
