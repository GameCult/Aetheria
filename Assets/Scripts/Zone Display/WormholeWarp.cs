/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// The screen-space effect of wormhole travel: lensing, swirl and a chromatic split toward the wormhole. Display only:
// Ship.WormholeWarp is the one input and nothing here writes sim state.
[Serializable]
[PostProcess(typeof(WormholeWarpRenderer), PostProcessEvent.AfterStack, "Aetheria/Wormhole Warp")]
public sealed class WormholeWarp : PostProcessEffectSettings
{
    [Tooltip("Where the wormhole is on screen, in viewport uv.")]
    public Vector2Parameter Center = new Vector2Parameter { value = new Vector2(.5f, .5f) };

    [Range(0, 1), Tooltip("How far into the warp the screen is.")]
    public FloatParameter Strength = new FloatParameter { value = 0 };

    public override bool IsEnabledAndSupported(PostProcessRenderContext context) =>
        enabled.value && Strength.value > 0;
}

[UnityEngine.Scripting.Preserve]
internal sealed class WormholeWarpRenderer : PostProcessEffectRenderer<WormholeWarp>
{
    // The shader sits under a Resources folder so a player build always carries it; Shader.Find finds it by name.
    private static bool _missingLogged;

    public override void Render(PostProcessRenderContext context)
    {
        var shader = Shader.Find("Hidden/Aetheria/WormholeWarp");
        if (shader == null)
        {
            if (!_missingLogged) Debug.LogError("WormholeWarp shader missing from the build");
            _missingLogged = true;
            context.command.BuiltinBlit(context.source, context.destination);
            return;
        }
        var sheet = context.propertySheets.Get(shader);
        sheet.properties.SetVector("_Center", settings.Center.value);
        sheet.properties.SetFloat("_Strength", settings.Strength.value);
        sheet.properties.SetFloat("_Aspect", (float)context.screenWidth / context.screenHeight);
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
    }
}

// Holds the warp volume for one transition: from entering the wormhole through the exit animation on the same ship.
// It reads the ship and the live camera and writes only the volume.
public sealed class WormholeWarpPresenter : MonoBehaviour
{
    private Ship _ship;
    private PostProcessVolume _volume;
    private WormholeWarp _settings;
    private bool _seenAnimation;

    public static void Begin(Ship ship, int layer, float priority)
    {
        var presenter = new GameObject("Wormhole Warp").AddComponent<WormholeWarpPresenter>();
        presenter._ship = ship;
        presenter._settings = ScriptableObject.CreateInstance<WormholeWarp>();
        presenter._settings.enabled.Override(true);
        presenter._volume = PostProcessManager.instance.QuickVolume(layer, priority, presenter._settings);
    }

    private void LateUpdate()
    {
        if (!_ship.WormholeAnimationInProgress)
        {
            if (_seenAnimation) Destroy(gameObject);
            return;
        }
        _seenAnimation = true;
        var camera = Camera.main;
        if (camera == null) return;
        // The wormhole the ship is at in this half, read from the ship each frame.
        var wormhole = _ship.WormholePosition;
        var viewport = camera.WorldToViewportPoint(new Vector3(wormhole.x, 0, wormhole.y));
        _settings.Center.value = new Vector2(viewport.x, viewport.y);
        _settings.Strength.value = _ship.WormholeWarp;
    }

    private void OnDestroy()
    {
        if (_volume != null) RuntimeUtilities.DestroyVolume(_volume, true, true);
    }
}
