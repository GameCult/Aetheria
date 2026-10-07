/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Random = CultMath.Random;

// Structured simulation seeds: the one place a zone seed, a stream and an ordinal become a generator. Every
// draw is a pure function of its arguments, never of a shared stream, so one roll cannot perturb another.
public static class SimulationDice
{
    public const uint LootStream = 1;
    public const uint MineStream = 2;

    // 9.1 (docs/fire-control-cut.md): fmix32, MurmurHash3's 32-bit finalizer -- xor-shift, multiply,
    // xor-shift, multiply, xor-shift. Diffuses a structured seed (a zone seed XORed with a small,
    // low-bits-only id) across every bit before the first xorshift draw reads it, so two seeds that differ
    // only in their low bits still land on uncorrelated first outputs. Caller-side mixing only --
    // CultMath.Random itself is unchanged. FireControl.Commit's per-shot seed is built on this verbatim.
    public static uint Mix(uint seed)
    {
        seed ^= seed >> 16;
        seed *= 0x85ebca6bu;
        seed ^= seed >> 13;
        seed *= 0xc2b2ae35u;
        seed ^= seed >> 16;
        return seed;
    }

    // The generator for the `ordinal`th roll of `stream` in the zone identified by `zoneSeed`.
    public static Random For(uint zoneSeed, uint stream, uint ordinal)
    {
        var seed = Mix(Mix(zoneSeed ^ Mix(stream)) ^ ordinal);
        return new Random(seed == 0 ? 1u : seed);
    }
}
