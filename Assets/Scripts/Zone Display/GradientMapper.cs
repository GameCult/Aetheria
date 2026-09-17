/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using static CultMath.math;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GradientMapper : MonoBehaviour
{
    [Header("Gradient map parameters")] public Vector2Int GradientMapDimensions = new Vector2Int(32, 1);
    public Gradient Gradient;

    [Header("Enable testing")] public bool Testing = false;

    private MeshRenderer _meshRenderer;
    private Material _material;

    [HideInInspector] public Texture2D Texture;

    public static int TotalMaps = 0;

    private void OnEnable()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer == null)
        {
            Debug.LogWarning("No mesh renderer on this game object! Removing GradientMapper");
            DestroyImmediate(this);
        }
        else
        {
            _material = _meshRenderer.sharedMaterial;
        }
    }

    void Update()
    {
        if (Testing)
        {
            ApplyGradient(Gradient);
        }
    }

    public void ApplyGradient(Gradient gradient)
    {
        Texture = new Texture2D(GradientMapDimensions.x, GradientMapDimensions.y);
        Texture.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < GradientMapDimensions.x; x++)
        {
            for (int y = 0; y < GradientMapDimensions.y; y++)
            {
                Texture.SetPixel(x, y, gradient.Evaluate( max((float)x / GradientMapDimensions.x,(float)y / GradientMapDimensions.y)));
            }
        }

        Texture.Apply();
        if (_material.HasProperty("_ColorRamp"))
        {
            _material.SetTexture("_ColorRamp", Texture);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GradientMapper))]
public class GradientMapperEditor : Editor
{
    private int _subfolder;
    private string[] Subfolders = {"Sun", "Gas Giant"};

    public override void OnInspectorGUI()
    {
        GradientMapper gradientMapper = target as GradientMapper;
        DrawDefaultInspector();
        if (gradientMapper.Testing)
        {
            EditorGUILayout.HelpBox("Testing is active.", MessageType.Warning, true);
        }

        _subfolder = EditorGUILayout.Popup("Subfolder", _subfolder, Subfolders);

        if (GUILayout.Button("Make Gradient Map"))
        {
            gradientMapper.Testing = false;
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Gradients"))
            {
                AssetDatabase.CreateFolder("Assets/Resources/", "Gradients");
                AssetDatabase.SaveAssets();
            }

            if (!Directory.Exists($"{Application.dataPath}Resources/Gradients/{Subfolders[_subfolder]}"))
            {
                Directory.CreateDirectory($"{Application.dataPath}/Resources/Gradients/{Subfolders[_subfolder]}");
                GradientMapper.TotalMaps = 0;
            }
            else
            {
                GradientMapper.TotalMaps = Directory.GetFiles(Application.dataPath + "/Resources/Gradients/").Length;
            }

            byte[] bytes = gradientMapper.Texture.EncodeToPNG();
            
            string fileName;
            do fileName = $"{Application.dataPath}/Resources/Gradients/{Subfolders[_subfolder] + '/'}gradient_{GradientMapper.TotalMaps++}.png";
            while (File.Exists(fileName));

            File.WriteAllBytes(fileName, bytes);
            AssetDatabase.Refresh();

            Debug.Log("Gradient map saved!");
        }
    }
}
#endif