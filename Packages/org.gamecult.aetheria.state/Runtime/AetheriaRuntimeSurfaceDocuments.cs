using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using MessagePack;
using EveSurfaceState = global::Aetheria.State.Documents.EveSurfaceState;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [CultDocument("gamecult.aetheria.runtime_surface", "gamecult.aetheria.runtime_surface.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeSurfaceDocument
    {
        [SerializationConstructor]
        public AetheriaRuntimeSurfaceDocument(
            string providerId,
            string providerKind,
            string title,
            long version,
            string updatedAtUtc,
            AetheriaRuntimeSurfaceTree surface,
            IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> commands)
        {
            ProviderId = providerId ?? "";
            ProviderKind = providerKind ?? "";
            Title = title ?? "";
            Version = version;
            UpdatedAtUtc = updatedAtUtc ?? "";
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            Commands = commands ?? Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>();
        }

        [Key(0)]
        public string ProviderId { get; }

        [Key(1)]
        public string ProviderKind { get; }

        [Key(2)]
        public string Title { get; }

        [Key(3)]
        public long Version { get; }

        [Key(4)]
        public string UpdatedAtUtc { get; }

        [Key(5)]
        public AetheriaRuntimeSurfaceTree Surface { get; }

        [Key(6)]
        public IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> Commands { get; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSurfaceTree
    {
        [SerializationConstructor]
        public AetheriaRuntimeSurfaceTree(
            string id,
            AetheriaRuntimeSurfaceComponent root,
            IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> styles)
        {
            Id = id ?? "";
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Styles = styles ?? Array.Empty<AetheriaRuntimeSurfaceStyleToken>();
        }

        [Key(0)]
        public string Id { get; }

        [Key(1)]
        public AetheriaRuntimeSurfaceComponent Root { get; }

        [Key(2)]
        public IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> Styles { get; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSurfaceComponent
    {
        public AetheriaRuntimeSurfaceComponent(
            string id,
            string kind,
            IReadOnlyDictionary<string, string> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children)
            : this(id, kind, props, children, AetheriaRuntimeSurfaceStateBindings.FromProps(props))
        {
        }

        public AetheriaRuntimeSurfaceComponent(
            string id,
            string kind,
            IReadOnlyDictionary<string, string> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children,
            IReadOnlyList<CultMeshStateBindingDescriptor> stateBindings)
            : this(id, kind, props, children, stateBindings, Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>())
        {
        }

        [SerializationConstructor]
        public AetheriaRuntimeSurfaceComponent(
            string id,
            string kind,
            IReadOnlyDictionary<string, string> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children,
            IReadOnlyList<CultMeshStateBindingDescriptor> stateBindings,
            IReadOnlyList<AetheriaRuntimeEmbeddedDocumentSlot> embeddedDocuments)
        {
            Id = id ?? "";
            Kind = kind ?? "";
            Props = props ?? new Dictionary<string, string>(StringComparer.Ordinal);
            Children = children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>();
            StateBindings = stateBindings ?? Array.Empty<CultMeshStateBindingDescriptor>();
            EmbeddedDocuments = embeddedDocuments ?? Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>();
        }

        [Key(0)]
        public string Id { get; }

        [Key(1)]
        public string Kind { get; }

        [Key(2)]
        public IReadOnlyDictionary<string, string> Props { get; }

        [Key(3)]
        public IReadOnlyList<AetheriaRuntimeSurfaceComponent> Children { get; }

        [Key(4)]
        public IReadOnlyList<CultMeshStateBindingDescriptor> StateBindings { get; }

        [Key(5)]
        public IReadOnlyList<AetheriaRuntimeEmbeddedDocumentSlot> EmbeddedDocuments { get; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEmbeddedDocumentSlot
    {
        [SerializationConstructor]
        public AetheriaRuntimeEmbeddedDocumentSlot(
            string slotId,
            string documentId,
            string schemaId,
            string presentationKind,
            CultMeshRouteHint routeHint)
        {
            SlotId = slotId ?? "";
            DocumentId = documentId ?? "";
            SchemaId = schemaId ?? "";
            PresentationKind = presentationKind ?? "";
            RouteHint = routeHint ?? CultMeshRouteHint.Automatic;
        }

        public AetheriaRuntimeEmbeddedDocumentSlot(
            string slotId,
            string documentId,
            string schemaId,
            string presentationKind)
            : this(slotId, documentId, schemaId, presentationKind, CultMeshRouteHint.Automatic)
        {
        }

        [Key(0)]
        public string SlotId { get; }

        [Key(1)]
        public string DocumentId { get; }

        [Key(2)]
        public string SchemaId { get; }

        [Key(3)]
        public string PresentationKind { get; }

        [Key(4)]
        public CultMeshRouteHint RouteHint { get; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSurfaceStyleToken
    {
        [SerializationConstructor]
        public AetheriaRuntimeSurfaceStyleToken(string name, string value)
        {
            Name = name ?? "";
            Value = value ?? "";
        }

        [Key(0)]
        public string Name { get; }

        [Key(1)]
        public string Value { get; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeSurfaceCommandTemplate
    {
        public const string CultMeshTransport = "cultmesh";

        public AetheriaRuntimeSurfaceCommandTemplate(string command, string label, string transport)
            : this(CultMesh.OperationBindingRecord(
                command,
                label,
                "",
                nameof(CultMeshLocalityKind.Automatic),
                transport).ToBinding())
        {
        }

        [SerializationConstructor]
        public AetheriaRuntimeSurfaceCommandTemplate(CultMeshOperationBindingDescriptor operation)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        [Key(0)]
        public CultMeshOperationBindingDescriptor Operation { get; }

        [IgnoreMember]
        public string Command => Operation.OperationId;

        [IgnoreMember]
        public string Label => Operation.Label;

        [IgnoreMember]
        public string Transport => Operation.RouteHint.Description ?? "";
    }

    public static class AetheriaRuntimeSurfaceDocuments
    {
        public static EveSurfaceState ToEveSurfaceState(AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return new EveSurfaceState
            {
                ProviderId = document.ProviderId,
                ProviderKind = document.ProviderKind,
                Title = document.Title,
                Version = document.Version,
                UpdatedAtUtc = document.UpdatedAtUtc,
                Surface = ToEveSurfaceState(document.Surface),
                Commands = document.Commands
                    .Select(command =>
                    {
                        var record = CultMesh.OperationBindingRecord(command.Operation);
                        return new global::Aetheria.State.Documents.EveCommandTemplate
                        {
                            Command = record.OperationId,
                            Label = record.Label,
                            Transport = record.RouteDescription,
                            SchemaId = record.SchemaId,
                            RouteKind = record.RouteKind,
                            RouteDescription = record.RouteDescription
                        };
                    })
                    .ToArray()
            };
        }

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
                        .Select(style => new EveStyleToken(style.Name, style.Value))
                        .ToArray()),
                state.Commands
                    .Select(command => new EveCommandTemplate(
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
                component.StateBindings.Select(ToCultMeshStateBinding).ToArray(),
                component.EmbeddedDocuments.Select(ToEveEmbeddedDocumentSlot).ToArray());
        }

        private static global::Aetheria.State.Documents.EveSurface ToEveSurfaceState(
            AetheriaRuntimeSurfaceTree surface)
        {
            return new global::Aetheria.State.Documents.EveSurface
            {
                Id = surface.Id,
                Root = ToEveSurfaceState(surface.Root),
                Styles = surface.Styles
                    .Select(style => new global::Aetheria.State.Documents.EveStyleToken
                    {
                        Name = style.Name,
                        Value = style.Value
                    })
                    .ToArray()
            };
        }

        private static global::Aetheria.State.Documents.EveSurfaceComponent ToEveSurfaceState(
            AetheriaRuntimeSurfaceComponent component)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            AetheriaRuntimeSurfaceStateBindings.AddPointerProps(props, component.StateBindings);

            return new global::Aetheria.State.Documents.EveSurfaceComponent
            {
                Id = component.Id,
                Kind = component.Kind,
                Props = props,
                Children = component.Children.Select(ToEveSurfaceState).ToArray(),
                StateBindings = component.StateBindings
                    .Select(binding =>
                    {
                        var record = CultMesh.StateBindingRecord(binding);
                        return new global::Aetheria.State.Documents.EveSurfaceStateBinding
                        {
                            TargetProp = record.TargetProp,
                            PointerId = record.PointerId,
                            SourceId = record.SourceId,
                            SchemaId = record.SchemaId,
                            RouteKind = record.RouteKind,
                            RouteDescription = record.RouteDescription
                        };
                    })
                    .ToArray()
            };
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

        private static EveEmbeddedDocumentSlot ToEveEmbeddedDocumentSlot(AetheriaRuntimeEmbeddedDocumentSlot slot)
        {
            return new EveEmbeddedDocumentSlot(
                slot.SlotId,
                slot.DocumentId,
                slot.SchemaId,
                slot.PresentationKind,
                slot.RouteHint);
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
                component.StateBindings,
                component.EmbeddedDocuments);
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
