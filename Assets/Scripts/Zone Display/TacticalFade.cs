/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// The tactical view's fade (ruling tactical-view-is-zoom-out): after the post stack the scene colour lerps toward the
// background by Strength. The volume's weight is the tactical blend, so Strength stays 1 and the fade is derived from
// camera distance like everything else about the view.
[Serializable]
[PostProcess(typeof(TacticalFadeRenderer), PostProcessEvent.AfterStack, "Aetheria/Tactical Fade", false)]
public sealed class TacticalFade : PostProcessEffectSettings
{
    [Range(0, 1)] public FloatParameter Strength = new FloatParameter { value = 1 };
    public ColorParameter Background = new ColorParameter { value = Color.black };
}

public sealed class TacticalFadeRenderer : PostProcessEffectRenderer<TacticalFade>
{
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int BackgroundId = Shader.PropertyToID("_Background");

    public override void Render(PostProcessRenderContext context)
    {
        var sheet = context.propertySheets.Get(Shader.Find("Aetheria/PostProcessing/TacticalFade"));
        sheet.properties.SetFloat(StrengthId, settings.Strength);
        sheet.properties.SetColor(BackgroundId, settings.Background);
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
    }
}
