using System;
using System.Linq;
using ShieldField;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode probe for the shield-panel Cut 1 landing (docs/shield-panel-cut.md).
    /// No NUnit coverage exists for these files (Tests.asmdef cannot see
    /// Assembly-CSharp — see the map's Q7), so this is the check that stands in
    /// for a unit test on two Cut 1 rules that a compile pass alone cannot prove:
    ///
    ///   1. The shader and compute shader carry zero compile errors on built-in.
    ///   2. Penrose cell count actually scales with panelRadius (D18) instead of
    ///      being a fixed function of the (now-removed) authored iteration count.
    ///
    /// Run via: Unity -batchmode -projectPath &lt;repo&gt; -executeMethod
    ///          Aetheria.EditorTools.ShieldPanelCut1Verify.Run -quit -logFile &lt;path&gt;
    /// Exits with a non-zero code if either rule is violated, so the log alone is
    /// not the only signal — a CI runner can gate on the exit code too.
    /// </summary>
    public static class ShieldPanelCut1Verify
    {
        public static void Run()
        {
            bool ok = true;
            ok &= CheckShaderMessages();
            ok &= CheckComputeMessages();
            ok &= CheckPenroseScalesWithRadius();

            Debug.Log(ok
                ? "[ShieldPanelCut1Verify] PASS"
                : "[ShieldPanelCut1Verify] FAIL — see errors above");

            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool CheckShaderMessages()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Shaders/Compute/ShieldField/ShieldPanel.shader");
            if (shader == null)
            {
                Debug.LogError("[ShieldPanelCut1Verify] ShieldPanel.shader not found.");
                return false;
            }

            bool ok = true;

            // The first query can observe the shader mid-compile and return a stale/empty
            // message set; warm it up before trusting the result.
            _ = ShaderUtil.GetShaderMessageCount(shader);
            _ = ShaderUtil.ShaderHasError(shader);
            var shaderMsgs = ShaderUtil.GetShaderMessages(shader);

            foreach (var m in shaderMsgs)
            {
                if (m.severity == ShaderCompilerMessageSeverity.Error)
                {
                    Debug.LogError($"[ShieldPanelCut1Verify] Shader error: {m.message} ({m.file}:{m.line})");
                    ok = false;
                }
                else
                {
                    Debug.Log($"[ShieldPanelCut1Verify] Shader warning: {m.message} ({m.file}:{m.line})");
                }
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogError("[ShieldPanelCut1Verify] ShieldPanel.shader has compile errors (ShaderHasError).");
                ok = false;
            }

            Debug.Log($"[ShieldPanelCut1Verify] shader supported={!ShaderUtil.ShaderHasError(shader)} messages={shaderMsgs.Length}");
            return ok;
        }

        static bool CheckComputeMessages()
        {
            var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Shaders/Compute/ShieldField/ShieldSim.compute");
            if (cs == null)
            {
                Debug.LogError("[ShieldPanelCut1Verify] ShieldSim.compute not found.");
                return false;
            }

            bool ok = true;
            var msgs = ShaderUtil.GetComputeShaderMessages(cs);
            foreach (var m in msgs)
            {
                if (m.severity == ShaderCompilerMessageSeverity.Error)
                {
                    Debug.LogError($"[ShieldPanelCut1Verify] Compute shader error: {m.message} ({m.file}:{m.line})");
                    ok = false;
                }
            }
            Debug.Log($"[ShieldPanelCut1Verify] compute messages={msgs.Length}, errors={msgs.Count(m => m.severity == ShaderCompilerMessageSeverity.Error)}");
            return ok;
        }

        static bool CheckPenroseScalesWithRadius()
        {
            const float cellSize = 0.35f;
            float[] radii = { 1.5f, 10f };
            int[] penroseCounts = new int[radii.Length];
            int[] hexCounts = new int[radii.Length];

            for (int i = 0; i < radii.Length; i++)
            {
                var pSoup = Tilings.Generate(TilingKind.PenroseP3, radii[i], cellSize, 1337);
                var pMerged = TilingBuilder.MergeRhombs(pSoup);
                var pData = TilingBuilder.Build(pMerged, weldEps: Mathf.Max(cellSize * 0.02f, 1e-4f),
                                                clipRadius: radii[i], seed: 1337);
                penroseCounts[i] = pData.CellCount;

                var hSoup = Tilings.Generate(TilingKind.Hex, radii[i], cellSize, 1337);
                var hData = TilingBuilder.Build(hSoup, weldEps: Mathf.Max(cellSize * 0.02f, 1e-4f),
                                                clipRadius: radii[i], seed: 1337);
                hexCounts[i] = hData.CellCount;

                Debug.Log($"[ShieldPanelCut1Verify] radius={radii[i]} cellSize={cellSize} " +
                          $"PenroseP3(merged) cells={penroseCounts[i]}  Hex cells={hexCounts[i]}");
            }

            bool ok = penroseCounts[0] != penroseCounts[1];
            if (!ok)
                Debug.LogError($"[ShieldPanelCut1Verify] D18 not fixed: Penrose cell count identical " +
                                $"({penroseCounts[0]}) at radius {radii[0]} and {radii[1]}.");
            else if (penroseCounts[1] < penroseCounts[0])
                Debug.LogError($"[ShieldPanelCut1Verify] D18 regression: Penrose cell count DECREASED " +
                                $"from {penroseCounts[0]} (r={radii[0]}) to {penroseCounts[1]} (r={radii[1]}).");
            else
                Debug.Log("[ShieldPanelCut1Verify] D18 fixed: Penrose cell count increases with panelRadius.");

            return ok && penroseCounts[1] > penroseCounts[0];
        }
    }
}
