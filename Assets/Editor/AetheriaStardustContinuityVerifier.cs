using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Aetheria.Editor
{
    internal static class AetheriaStardustContinuityVerifier
    {
        private const int Span = 16;
        private const int GroupSize = 128;
        private const float Spacing = 6f;

        [StructLayout(LayoutKind.Sequential)]
        private struct Particle
        {
            public Vector3 Position;
            public Vector3 Color;
            public float Size;
        }

        internal static void VerifyTemporalDitherMaterial(Material material, bool requireAuthoredSource)
        {
            if (material == null || material.shader == null)
                throw new ArgumentNullException(nameof(material));
            if (!material.HasProperty("_DitheringTex"))
                throw new InvalidOperationException("Stardust render material has no temporal dither texture port.");
            var shaderPath = AssetDatabase.GetAssetPath(material.shader);
            var source = string.IsNullOrWhiteSpace(shaderPath) || !File.Exists(shaderPath)
                ? ""
                : File.ReadAllText(shaderPath);
            if (string.IsNullOrWhiteSpace(source))
            {
                if (requireAuthoredSource)
                    throw new InvalidOperationException("Stardust authored shader source is unavailable for verification.");
                return;
            }
            var required = new[]
            {
                "_FrameNumber",
                "SAMPLE_TEXTURE2D",
                "clip(alpha - dither",
                "Blend One Zero",
                "ZWrite On"
            };
            foreach (var token in required)
                if (!source.Contains(token, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Stardust render shader lost required temporal coverage token '{token}'.");
            if (source.Contains("_AlphaClip", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Stardust render shader regressed to a fixed alpha threshold instead of temporal coverage.");
        }

        internal static void VerifyOneCellShift(ComputeShader program)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (!SystemInfo.supportsComputeShaders)
                throw new InvalidOperationException("Stardust continuity proof requires compute-shader support.");

            var kernel = program.FindKernel("UpdateParticles");
            using var particles = new ComputeBuffer(Span * Span, 7 * sizeof(float), ComputeBufferType.Structured);
            var field = ConstantTexture(new Color(0.5f, 0.5f, 0.5f, 1f));
            var emptyField = ConstantTexture(Color.clear);
            var positiveXField = PositiveXTexture();
            var tint = ConstantTexture(Color.white);
            var hue = ConstantTexture(Color.white);
            try
            {
                program.EnableKeyword("FLOW_GLOBAL");
                program.EnableKeyword("NOISE_SLOPE");
                program.SetBuffer(kernel, "particles", particles);
                program.SetTexture(kernel, "_NebulaSurfaceHeight", field);
                program.SetTexture(kernel, "_NebulaTint", tint);
                program.SetTexture(kernel, "HueTexture", hue);
                program.SetVector("_NebulaSurfaceHeight_TexelSize", new Vector4(0.25f, 0.25f, 4f, 4f));
                program.SetVector("_NebulaTint_TexelSize", new Vector4(0.25f, 0.25f, 4f, 4f));
                program.SetVector("_Time", new Vector4(0.625f, 12.5f, 25f, 37.5f));
                program.SetFloat("time", 12.5f);
                program.SetFloat("period", 2f);
                program.SetFloat("spacing", Spacing);
                program.SetFloat("ceilingHeight", 0f);
                program.SetFloat("floorHeight", -10f);
                program.SetFloat("heightExponent", 3f);
                program.SetFloat("maximumSize", 0.75f);
                program.SetFloat("minimumSize", 0.25f);
                program.SetFloat("minHeadroom", 25f);
                program.SetFloat("maxHeadroom", 100f);
                program.SetFloat("_FlowScale", 512f);
                program.SetFloat("_FlowAmplitude", 15f);
                program.SetFloat("_FlowScroll", 0.3125f);
                program.SetFloat("_DynamicLodHigh", 7f);
                program.SetFloat("_DynamicLodLow", 2f);
                program.SetInt("span", Span);

                program.SetFloat("_FlowAmplitude", 0f);
                program.SetTexture(kernel, "_NebulaSurfaceHeight", emptyField);
                var flat = DispatchAndRead(program, kernel, particles, 0f, 0f);
                program.SetTexture(kernel, "_NebulaSurfaceHeight", positiveXField);
                var oriented = DispatchAndRead(program, kernel, particles, 0f, 0f);
                AssertPositiveWorldXSamplesPositiveTextureX(flat, oriented);

                program.SetTexture(kernel, "_NebulaSurfaceHeight", field);
                program.SetFloat("_FlowAmplitude", 15f);
                var before = DispatchAndRead(program, kernel, particles, 0f, 0f);
                AssertWholeBlockRemapIsInvisible(
                    before,
                    DispatchAndRead(program, kernel, particles, Spacing, 0f),
                    1,
                    0);
                AssertWholeBlockRemapIsInvisible(
                    before,
                    DispatchAndRead(program, kernel, particles, 0f, Spacing),
                    0,
                    1);
                AssertWholeBlockRemapIsInvisible(
                    before,
                    DispatchAndRead(program, kernel, particles, Spacing, Spacing),
                    1,
                    1);
                Debug.Log("Aetheria Stardust continuity: 705 overlapping world cells remained bit-identical " +
                          "across X, Y, and diagonal whole-buffer remaps.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(field);
                UnityEngine.Object.DestroyImmediate(emptyField);
                UnityEngine.Object.DestroyImmediate(positiveXField);
                UnityEngine.Object.DestroyImmediate(tint);
                UnityEngine.Object.DestroyImmediate(hue);
            }
        }

        private static Particle[] DispatchAndRead(
            ComputeShader program,
            int kernel,
            ComputeBuffer particles,
            float gridCenterX,
            float gridCenterY)
        {
            program.SetVector("_GridTransform", new Vector4(
                gridCenterX,
                gridCenterY,
                Span * Spacing,
                Span * Spacing));
            program.Dispatch(kernel, Mathf.CeilToInt((float)(Span * Span) / GroupSize), 1, 1);
            var result = new Particle[Span * Span];
            particles.GetData(result);
            return result;
        }

        private static void AssertWholeBlockRemapIsInvisible(
            Particle[] before,
            Particle[] after,
            int cellDeltaX,
            int cellDeltaY)
        {
            var half = Span / 2;
            var sameSlot = half * Span + half;
            if (BitwiseEqual(before[sameSlot], after[sameSlot]))
                throw new InvalidOperationException(
                    "Stardust continuity fixture did not reassign the sampled buffer slot to a new world cell.");

            var minimumWorldX = Math.Max(-half, -half + cellDeltaX);
            var maximumWorldX = Math.Min(half - 1, half - 1 + cellDeltaX);
            var minimumWorldY = Math.Max(-half, -half + cellDeltaY);
            var maximumWorldY = Math.Min(half - 1, half - 1 + cellDeltaY);
            for (var worldY = minimumWorldY; worldY <= maximumWorldY; worldY++)
            for (var worldX = minimumWorldX; worldX <= maximumWorldX; worldX++)
            {
                var beforeIndex = (worldY + half) * Span + worldX + half;
                var afterIndex = (worldY - cellDeltaY + half) * Span +
                                 (worldX - cellDeltaX) + half;
                if (!BitwiseEqual(before[beforeIndex], after[afterIndex]))
                    throw new InvalidOperationException(
                        $"Stardust cell ({worldX},{worldY}) changed while crossing one spatial cell; " +
                        $"buffer slots {beforeIndex} and {afterIndex} must describe the same stateless particle.");
            }
        }

        private static void AssertPositiveWorldXSamplesPositiveTextureX(
            Particle[] flat,
            Particle[] oriented)
        {
            const int positiveWorldCellX = 3;
            const int worldCellY = 0;
            var index = (worldCellY + Span / 2) * Span + positiveWorldCellX + Span / 2;
            var displacement = oriented[index].Position.y - flat[index].Position.y;
            if (displacement > -0.75f)
                throw new InvalidOperationException(
                    $"Stardust positive-world X sampled the wrong side of the typed gravity viewport; " +
                    $"expected at least one unit of downward displacement, observed {displacement:R}.");
        }

        private static bool BitwiseEqual(Particle left, Particle right) =>
            Bits(left.Position.x) == Bits(right.Position.x) &&
            Bits(left.Position.y) == Bits(right.Position.y) &&
            Bits(left.Position.z) == Bits(right.Position.z) &&
            Bits(left.Color.x) == Bits(right.Color.x) &&
            Bits(left.Color.y) == Bits(right.Color.y) &&
            Bits(left.Color.z) == Bits(right.Color.z) &&
            Bits(left.Size) == Bits(right.Size);

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        private static Texture2D ConstantTexture(Color color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[16];
            for (var index = 0; index < pixels.Length; index++) pixels[index] = color;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D PositiveXTexture()
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = x >= size / 2 ? Color.white : Color.clear;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
