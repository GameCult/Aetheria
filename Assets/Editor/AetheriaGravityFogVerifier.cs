using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aetheria.Editor
{
    internal static class AetheriaGravityFogVerifier
    {
        internal static void VerifyCameraProjection(Shader shader, bool requireAuthoredSource)
        {
            if (shader == null)
                throw new ArgumentNullException(nameof(shader));
            var shaderPath = AssetDatabase.GetAssetPath(shader);
            var source = string.IsNullOrWhiteSpace(shaderPath) || !File.Exists(shaderPath)
                ? ""
                : File.ReadAllText(shaderPath);
            if (string.IsNullOrWhiteSpace(source))
            {
                if (requireAuthoredSource)
                    throw new InvalidOperationException("Gravity-fog authored shader source is unavailable for verification.");
                return;
            }

            const string forwardRay = "float3(i.vsray, 1.0)";
            var first = source.IndexOf(forwardRay, StringComparison.Ordinal);
            var second = first < 0
                ? -1
                : source.IndexOf(forwardRay, first + forwardRay.Length, StringComparison.Ordinal);
            if (second < 0)
                throw new InvalidOperationException(
                    "Gravity-fog raymarching and temporal reprojection must both use the historical positive-Z Unity shader camera transform.");

            const string directHistoryDensity = "saturate(currSample.a) * _CompositeOpacity";
            if (source.IndexOf(directHistoryDensity, StringComparison.Ordinal) < 0 ||
                source.IndexOf("unpack(currSample.a", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException(
                    "Gravity-fog composite must consume the temporal history's direct density alpha without unpacking it as a raymarch payload.");
        }
    }
}
