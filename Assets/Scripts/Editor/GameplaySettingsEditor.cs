using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameSettings))]
public class GameSettingsEditor : Editor
{
    private bool _showIcons;
    private bool _showWeaponTypeIcons;
    private bool _showWeaponModifierIcons;
    private bool _showWeaponFiringTypeIcons;
    private bool _showWeaponCaliberIcons;
    private bool _showWeaponRangeIcons;
    private bool _showProperties;
    private bool _showDatabase;

//    private ScriptableItem _prefab;
    
    public override void OnInspectorGUI()
    {
        var data = (GameSettings) target;
        
        serializedObject.Update();

        DrawDefaultInspector();

        var hardpointNames = Enum.GetNames(typeof(HardpointType));
        if(data.ItemIcons.Length!=hardpointNames.Length)
            Array.Resize(ref data.ItemIcons, hardpointNames.Length);

        if (GUILayout.Button("Hardpoint Icons", EditorStyles.toolbarButton))
            _showIcons = !_showIcons;
        
        if(_showIcons)
            for (var i = 0; i < data.ItemIcons.Length; i++)
                data.ItemIcons[i] = (Sprite) EditorGUILayout.ObjectField(hardpointNames[i], data.ItemIcons[i], typeof(Sprite), false);

        var modifierNames = Enum.GetNames(typeof(WeaponModifiers));
        if(data.WeaponModifierIcons.Length!=modifierNames.Length)
            Array.Resize(ref data.WeaponModifierIcons, modifierNames.Length);

        if (GUILayout.Button("Weapon Modifier Icons", EditorStyles.toolbarButton))
            _showWeaponModifierIcons = !_showWeaponModifierIcons;
        
        if(_showWeaponModifierIcons)
            for (var i = 0; i < data.WeaponModifierIcons.Length; i++)
                data.WeaponModifierIcons[i] = (Sprite) EditorGUILayout.ObjectField(modifierNames[i], data.WeaponModifierIcons[i], typeof(Sprite), false);

        var weaponTypeNames = Enum.GetNames(typeof(WeaponType));
        if(data.WeaponTypeIcons.Length!=weaponTypeNames.Length)
            Array.Resize(ref data.WeaponTypeIcons, weaponTypeNames.Length);

        if (GUILayout.Button("Weapon Type Icons", EditorStyles.toolbarButton))
            _showWeaponTypeIcons = !_showWeaponTypeIcons;
        
        if(_showWeaponTypeIcons)
            for (var i = 0; i < data.WeaponTypeIcons.Length; i++)
                data.WeaponTypeIcons[i] = (Sprite) EditorGUILayout.ObjectField(weaponTypeNames[i], data.WeaponTypeIcons[i], typeof(Sprite), false);

        var weaponCaliberNames = Enum.GetNames(typeof(WeaponCaliber));
        if(data.WeaponCaliberIcons.Length!=weaponCaliberNames.Length)
            Array.Resize(ref data.WeaponCaliberIcons, weaponCaliberNames.Length);

        if (GUILayout.Button("Weapon Caliber Icons", EditorStyles.toolbarButton))
            _showWeaponCaliberIcons = !_showWeaponCaliberIcons;
        
        if(_showWeaponCaliberIcons)
            for (var i = 0; i < data.WeaponCaliberIcons.Length; i++)
                data.WeaponCaliberIcons[i] = (Sprite) EditorGUILayout.ObjectField(weaponCaliberNames[i], data.WeaponCaliberIcons[i], typeof(Sprite), false);

        var weaponFireTypeNames = Enum.GetNames(typeof(WeaponFireType));
        if(data.WeaponFireTypeIcons.Length!=weaponFireTypeNames.Length)
            Array.Resize(ref data.WeaponFireTypeIcons, weaponFireTypeNames.Length);

        if (GUILayout.Button("Weapon Type Icons", EditorStyles.toolbarButton))
            _showWeaponFiringTypeIcons = !_showWeaponFiringTypeIcons;
        
        if(_showWeaponFiringTypeIcons)
            for (var i = 0; i < data.WeaponFireTypeIcons.Length; i++)
                data.WeaponFireTypeIcons[i] = (Sprite) EditorGUILayout.ObjectField(weaponFireTypeNames[i], data.WeaponFireTypeIcons[i], typeof(Sprite), false);

        var weaponRangeNames = Enum.GetNames(typeof(WeaponRange));
        if(data.WeaponRangeIcons.Length!=weaponRangeNames.Length)
            Array.Resize(ref data.WeaponRangeIcons, weaponRangeNames.Length);

        if (GUILayout.Button("Weapon Range Icons", EditorStyles.toolbarButton))
            _showWeaponRangeIcons = !_showWeaponRangeIcons;
        
        if(_showWeaponRangeIcons)
            for (var i = 0; i < data.WeaponRangeIcons.Length; i++)
                data.WeaponRangeIcons[i] = (Sprite) EditorGUILayout.ObjectField(weaponRangeNames[i], data.WeaponRangeIcons[i], typeof(Sprite), false);

        serializedObject.ApplyModifiedProperties();
    }
}