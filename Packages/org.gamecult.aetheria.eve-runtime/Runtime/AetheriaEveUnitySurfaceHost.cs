using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
using GameCult.Mesh;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCult.Aetheria.EveRuntime
{
    public sealed class AetheriaEveUnitySurfaceChrome
    {
        public float RootPaddingTop = 16f;
        public Align RootAlignItems = Align.Center;
        public Justify RootJustifyContent = Justify.FlexStart;
        public PickingMode RootPickingMode = PickingMode.Ignore;
        public float RootPaddingLeft = 0f;
        public float RootPaddingRight = 0f;
        public float RootPaddingBottom = 0f;
        public Color RootBackgroundColor = new Color(0f, 0f, 0f, 0f);

        public float Width = 0f;
        public float MinWidth = 420f;
        public float MaxWidth = 760f;
        public float MaxHeight = 0f;
        public float FlexGrow = 0f;
        public FlexDirection FlexDirection = FlexDirection.Column;
        public float PaddingLeft = 16f;
        public float PaddingRight = 16f;
        public float PaddingTop = 12f;
        public float PaddingBottom = 12f;
        public float BorderRadius = 8f;
        public float BorderWidth = 1f;
        public Color BackgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        public Color BorderColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        public PickingMode ShellPickingMode = PickingMode.Position;
        public bool UseShell = true;
    }

    public static class AetheriaEveUnitySurfaceHost
    {
        public static UIDocument Render(
            Transform owner,
            UIDocument document,
            string hostName,
            EveSurfaceDocument surface,
            Action<EveSurfaceCommandRequest> commandHandler,
            AetheriaEveUnitySurfaceChrome chrome,
            CultMeshStateRefResolver stateRefResolver = null,
            int sortingOrder = 1000)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (commandHandler == null)
                throw new ArgumentNullException(nameof(commandHandler));

            document = ResolveDocument(owner, document, hostName, sortingOrder);
            document.gameObject.SetActive(true);

            var root = document.rootVisualElement;
            ConfigureRoot(root, chrome);

            var effectiveStateRefResolver = stateRefResolver ?? (
                ContainsStateRefs(surface)
                    ? CreateDefaultStateRefResolver()
                    : null);
            surface = AetheriaRuntimeEveSurfaceAdapter.ResolveStateRefs(surface, effectiveStateRefResolver);
            var lowerer = new EveUiToolkitSurfaceLowerer();
            if (chrome.UseShell)
            {
                var shell = CreateShell(chrome);
                root.Add(shell);
                shell.Add(lowerer.Lower(surface, commandHandler));
            }
            else
            {
                root.Add(lowerer.Lower(surface, commandHandler));
            }

            return document;
        }

        public static UIDocument RenderRuntime(
            Transform owner,
            UIDocument document,
            string hostName,
            AetheriaRuntimeSurfaceDocument surface,
            Action<EveSurfaceCommandRequest> commandHandler,
            AetheriaEveUnitySurfaceChrome chrome,
            CultMeshStateRefResolver stateRefResolver = null,
            int sortingOrder = 1000)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            return Render(
                owner,
                document,
                hostName,
                AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(surface),
                commandHandler,
                chrome,
                stateRefResolver,
                sortingOrder);
        }

        public static void Hide(UIDocument document)
        {
            if (document == null)
                return;

            document.rootVisualElement.Clear();
            document.gameObject.SetActive(false);
        }

        public static void DestroyDocument(UIDocument document)
        {
            if (document == null)
                return;

            UnityEngine.Object.Destroy(document.gameObject);
        }

        private static UIDocument ResolveDocument(
            Transform owner,
            UIDocument document,
            string hostName,
            int sortingOrder)
        {
            if (document != null)
                return document;

            var host = new GameObject(string.IsNullOrWhiteSpace(hostName) ? "Aetheria Eve Surface" : hostName);
            host.transform.SetParent(owner, false);
            host.layer = owner.gameObject.layer;
            document = host.AddComponent<UIDocument>();
            document.sortingOrder = sortingOrder;
            host.SetActive(false);
            return document;
        }

        private static void ConfigureRoot(VisualElement root, AetheriaEveUnitySurfaceChrome chrome)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.style.alignItems = chrome.RootAlignItems;
            root.style.justifyContent = chrome.RootJustifyContent;
            root.style.paddingLeft = chrome.RootPaddingLeft;
            root.style.paddingTop = chrome.RootPaddingTop;
            root.style.paddingRight = chrome.RootPaddingRight;
            root.style.paddingBottom = chrome.RootPaddingBottom;
            if (chrome.RootBackgroundColor.a > 0f)
                root.style.backgroundColor = chrome.RootBackgroundColor;
            root.pickingMode = chrome.RootPickingMode;
        }

        private static VisualElement CreateShell(AetheriaEveUnitySurfaceChrome chrome)
        {
            var shell = new VisualElement();
            shell.style.flexDirection = chrome.FlexDirection;
            shell.style.flexGrow = chrome.FlexGrow;
            if (chrome.Width > 0f)
                shell.style.width = chrome.Width;
            shell.style.minWidth = chrome.MinWidth;
            shell.style.maxWidth = chrome.MaxWidth;
            if (chrome.MaxHeight > 0f)
                shell.style.maxHeight = chrome.MaxHeight;
            shell.style.backgroundColor = chrome.BackgroundColor;
            shell.style.borderTopLeftRadius = chrome.BorderRadius;
            shell.style.borderTopRightRadius = chrome.BorderRadius;
            shell.style.borderBottomLeftRadius = chrome.BorderRadius;
            shell.style.borderBottomRightRadius = chrome.BorderRadius;
            shell.style.paddingLeft = chrome.PaddingLeft;
            shell.style.paddingRight = chrome.PaddingRight;
            shell.style.paddingTop = chrome.PaddingTop;
            shell.style.paddingBottom = chrome.PaddingBottom;
            shell.style.borderLeftWidth = chrome.BorderWidth;
            shell.style.borderRightWidth = chrome.BorderWidth;
            shell.style.borderTopWidth = chrome.BorderWidth;
            shell.style.borderBottomWidth = chrome.BorderWidth;
            shell.style.borderLeftColor = chrome.BorderColor;
            shell.style.borderRightColor = chrome.BorderColor;
            shell.style.borderTopColor = chrome.BorderColor;
            shell.style.borderBottomColor = chrome.BorderColor;
            shell.pickingMode = chrome.ShellPickingMode;
            return shell;
        }

        private static bool ContainsStateRefs(EveSurfaceDocument surface)
        {
            return surface?.Surface?.Root != null && ContainsStateRefs(surface.Surface.Root);
        }

        private static bool ContainsStateRefs(EveSurfaceComponent component)
        {
            if (component.Props != null &&
                component.Props.Any(prop =>
                    !string.IsNullOrWhiteSpace(prop.Value) &&
                    (string.Equals(prop.Key, AetheriaRuntimeSurfaceStateRefs.Source, StringComparison.Ordinal) ||
                     prop.Key.EndsWith("Ref", StringComparison.Ordinal))))
            {
                return true;
            }

            foreach (var child in component.Children)
            {
                if (ContainsStateRefs(child))
                    return true;
            }

            return false;
        }

        private static CultMeshStateRefResolver CreateDefaultStateRefResolver()
        {
            var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory, "");
            if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
                return null;

            return AetheriaUnityRuntimeClientProvider.EveSurfaceStateRefResolver(
                stateBoot,
                "unity-eve-surface-host");
        }

    }
}
