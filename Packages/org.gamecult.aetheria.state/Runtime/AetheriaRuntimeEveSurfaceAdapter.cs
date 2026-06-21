using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeEveSurfaceAdapter
    {
        public static EveSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)
        {
            return ToEveSurfaceDocument(document, null);
        }

        public static EveSurfaceDocument ToEveSurfaceDocument(
            AetheriaRuntimeSurfaceDocument document,
            Func<string, string>? stateRefResolver)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var surface = new EveSurfaceDocument(
                "surface-state",
                "gamecult.eve.surface.v1",
                document.ProviderId,
                document.ProviderKind,
                document.Title,
                document.Version,
                document.UpdatedAtUtc,
                new EveSurfaceTree(
                    document.Surface.Id,
                    ToEveSurfaceComponent(document.Surface.Root),
                    document.Surface.Styles
                        .Select(style => new EveStyleToken(style.Name, style.Value))
                        .ToArray()),
                document.Commands
                    .Select(command => new EveCommandTemplate(command.Command, command.Label, command.Transport))
                    .ToArray());

            return ResolveStateRefs(surface, stateRefResolver);
        }

        public static EveSurfaceDocument ResolveStateRefs(
            EveSurfaceDocument surface,
            Func<string, string>? stateRefResolver)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (stateRefResolver == null)
                return surface;

            return new EveSurfaceDocument(
                surface.Type,
                surface.Schema,
                surface.ProviderId,
                surface.ProviderKind,
                surface.Title,
                surface.Version,
                surface.UpdatedAtUtc,
                new EveSurfaceTree(
                    surface.Surface.Id,
                    ResolveStateRefs(surface.Surface.Root, stateRefResolver),
                    surface.Surface.Styles),
                surface.Commands);
        }

        public static EveSurfaceDocument EmptySurface(string surfaceId)
        {
            var id = string.IsNullOrWhiteSpace(surfaceId) ? "aetheria.surface.missing" : surfaceId;
            return new EveSurfaceDocument(
                "surface-state",
                "gamecult.eve.surface.v1",
                "aetheria.daemon",
                "daemon",
                "",
                0,
                "",
                new EveSurfaceTree(
                    id,
                    new EveSurfaceComponent(
                        id + ".missing",
                        "surface",
                        new Dictionary<string, string>(StringComparer.Ordinal),
                        Array.Empty<EveSurfaceComponent>()),
                    Array.Empty<EveStyleToken>()),
                Array.Empty<EveCommandTemplate>());
        }

        private static EveSurfaceComponent ToEveSurfaceComponent(AetheriaRuntimeSurfaceComponent component)
        {
            return new EveSurfaceComponent(
                component.Id,
                component.Kind,
                new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
                component.Children.Select(ToEveSurfaceComponent).ToArray());
        }

        private static EveSurfaceComponent ResolveStateRefs(
            EveSurfaceComponent component,
            Func<string, string> stateRefResolver)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            ResolvePropRefs(props, stateRefResolver);

            return new EveSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                ResolveStateRefs(component.Children, stateRefResolver));
        }

        private static IReadOnlyList<EveSurfaceComponent> ResolveStateRefs(
            IReadOnlyList<EveSurfaceComponent> children,
            Func<string, string> stateRefResolver)
        {
            if (children == null || children.Count == 0)
                return Array.Empty<EveSurfaceComponent>();

            var resolved = new EveSurfaceComponent[children.Count];
            for (var index = 0; index < children.Count; index++)
                resolved[index] = ResolveStateRefs(children[index], stateRefResolver);
            return resolved;
        }

        private static void ResolvePropRefs(
            Dictionary<string, string> props,
            Func<string, string> stateRefResolver)
        {
            ResolvePropRef(props, AetheriaRuntimeSurfaceStateRefs.Source, "value", stateRefResolver);

            var refProps = props
                .Where(prop => IsStatePointerProp(prop.Key) &&
                               !string.Equals(prop.Key, AetheriaRuntimeSurfaceStateRefs.Source, StringComparison.Ordinal) &&
                               !string.IsNullOrWhiteSpace(prop.Value))
                .ToArray();

            foreach (var refProp in refProps)
                ResolvePropRef(props, refProp.Key, ResolvePointerValueKey(refProp.Key), stateRefResolver);
        }

        private static bool IsStatePointerProp(string key)
        {
            return key.EndsWith("Ref", StringComparison.Ordinal);
        }

        private static string ResolvePointerValueKey(string refKey)
        {
            return refKey.Substring(0, refKey.Length - "Ref".Length);
        }

        private static void ResolvePropRef(
            Dictionary<string, string> props,
            string refKey,
            string valueKey,
            Func<string, string> stateRefResolver)
        {
            if (!props.TryGetValue(refKey, out var stateRef) || string.IsNullOrWhiteSpace(stateRef))
                return;

            var resolved = stateRefResolver(stateRef);
            if (!string.IsNullOrWhiteSpace(resolved))
                props[valueKey] = resolved;
        }
    }
}
