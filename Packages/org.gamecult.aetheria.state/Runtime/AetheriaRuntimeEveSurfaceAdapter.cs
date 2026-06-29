using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using EveSurfaceState = global::Aetheria.State.Documents.EveSurfaceState;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeEveSurfaceAdapter
    {
        public static EveSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)
        {
            return ToEveSurfaceDocument(document, null);
        }

        public static EveSurfaceDocument ToEveSurfaceDocument(EveSurfaceState state)
        {
            return ToEveSurfaceDocument(state, null);
        }

        public static EveSurfaceDocument ToEveSurfaceDocument(
            EveSurfaceState state,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var surface = new EveSurfaceDocument(
                state.Type,
                state.Schema,
                state.ProviderId,
                state.ProviderKind,
                state.Title,
                state.Version,
                state.UpdatedAtUtc,
                new EveSurfaceTree(
                    state.Surface.Id,
                    ToEveSurfaceComponent(state.Surface.Root),
                    state.Surface.Styles
                        .Select(style => new GameCult.Eve.Surface.EveStyleToken(style.Name, style.Value))
                        .ToArray()),
                state.Commands
                    .Select(command => new GameCult.Eve.Surface.EveCommandTemplate(
                        ToCultMeshOperationBinding(command)))
                    .ToArray());

            return ResolveStateRefs(surface, stateRefResolver);
        }

        public static EveSurfaceDocument ToEveSurfaceDocument(
            AetheriaRuntimeSurfaceDocument document,
            CultMeshStateRefResolver? stateRefResolver)
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
                    .Select(command => new EveCommandTemplate(command.Operation))
                    .ToArray());

            return ResolveStateRefs(surface, stateRefResolver);
        }

        public static EveSurfaceDocument ResolveStateRefs(
            EveSurfaceDocument surface,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (stateRefResolver == null)
                return surface;

            var resolveStateRef = stateRefResolver.AsFunc();
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
                    ResolveStateRefs(surface.Surface.Root, resolveStateRef),
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
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            AetheriaRuntimeSurfaceStateBindings.AddPointerProps(props, component.StateBindings);
            return new EveSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                component.Children.Select(ToEveSurfaceComponent).ToArray(),
                component.StateBindings.Select(ToCultMeshStateBinding).ToArray());
        }

        private static EveSurfaceComponent ToEveSurfaceComponent(global::Aetheria.State.Documents.EveSurfaceComponent component)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            var stateBindings = (component.StateBindings ?? Array.Empty<global::Aetheria.State.Documents.EveSurfaceStateBinding>())
                .Select(ToRuntimeStateBinding)
                .ToArray();
            AetheriaRuntimeSurfaceStateBindings.AddPointerProps(
                props,
                stateBindings);
            return new EveSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                component.Children.Select(ToEveSurfaceComponent).ToArray(),
                stateBindings.Select(ToCultMeshStateBinding).ToArray());
        }

        private static CultMeshStateBindingDescriptor ToCultMeshStateBinding(
            CultMeshStateBindingDescriptor binding)
        {
            return binding;
        }

        private static CultMeshStateBindingDescriptor ToRuntimeStateBinding(
            global::Aetheria.State.Documents.EveSurfaceStateBinding binding)
        {
            return CultMesh.StateBindingRecord(
                binding.TargetProp,
                binding.PointerId,
                binding.SourceId,
                binding.SchemaId,
                binding.RouteKind,
                binding.RouteDescription).ToBinding();
        }

        private static CultMeshOperationBindingDescriptor ToCultMeshOperationBinding(
            global::Aetheria.State.Documents.EveCommandTemplate command)
        {
            var routeKind = string.IsNullOrWhiteSpace(command.RouteKind)
                ? nameof(CultMeshLocalityKind.Automatic)
                : command.RouteKind;
            var routeDescription = string.IsNullOrWhiteSpace(command.RouteDescription)
                ? command.Transport
                : command.RouteDescription;
            return CultMesh.OperationBindingRecord(
                command.Command,
                command.Label,
                command.SchemaId,
                routeKind,
                routeDescription).ToBinding();
        }

        private static EveSurfaceComponent ResolveStateRefs(
            EveSurfaceComponent component,
            Func<string, string> resolveStateRef)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            ResolvePropRefs(props, resolveStateRef);

            return new EveSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                ResolveStateRefs(component.Children, resolveStateRef),
                component.StateBindings);
        }

        private static IReadOnlyList<EveSurfaceComponent> ResolveStateRefs(
            IReadOnlyList<EveSurfaceComponent> children,
            Func<string, string> resolveStateRef)
        {
            if (children == null || children.Count == 0)
                return Array.Empty<EveSurfaceComponent>();

            var resolved = new EveSurfaceComponent[children.Count];
            for (var index = 0; index < children.Count; index++)
                resolved[index] = ResolveStateRefs(children[index], resolveStateRef);
            return resolved;
        }

        private static void ResolvePropRefs(
            Dictionary<string, string> props,
            Func<string, string> resolveStateRef)
        {
            ResolvePropRef(props, AetheriaRuntimeSurfaceStateRefs.Source, "value", resolveStateRef);

            var refProps = props
                .Where(prop => IsStatePointerProp(prop.Key) &&
                               !string.Equals(prop.Key, AetheriaRuntimeSurfaceStateRefs.Source, StringComparison.Ordinal) &&
                               !string.IsNullOrWhiteSpace(prop.Value))
                .ToArray();

            foreach (var refProp in refProps)
                ResolvePropRef(props, refProp.Key, ResolvePointerValueKey(refProp.Key), resolveStateRef);
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
            Func<string, string> resolveStateRef)
        {
            if (!props.TryGetValue(refKey, out var stateRef) || string.IsNullOrWhiteSpace(stateRef))
                return;

            var resolved = resolveStateRef(stateRef);
            if (!string.IsNullOrWhiteSpace(resolved))
                props[valueKey] = resolved;
        }
    }
}
