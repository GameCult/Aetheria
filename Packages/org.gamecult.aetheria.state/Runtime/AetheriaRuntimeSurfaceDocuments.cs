using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Mesh;
using MessagePack;
using EveUiCommandTemplate = GameCult.Eve.Surface.EveCommandTemplate;
using EveUiEmbeddedDocumentSlot = GameCult.Eve.Surface.EveEmbeddedDocumentSlot;
using EveUiStyleToken = GameCult.Eve.Surface.EveStyleToken;
using EveUiSurfaceComponent = GameCult.Eve.Surface.EveSurfaceComponent;
using EveUiSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;
using EveUiSurfaceTree = GameCult.Eve.Surface.EveSurfaceTree;

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
        public static GameCult.Mesh.EveSurfaceDocument ToPortableSurface(AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return new GameCult.Mesh.EveSurfaceDocument(
                document.ProviderId,
                document.ProviderKind,
                document.Title,
                document.Version,
                document.UpdatedAtUtc,
                new GameCult.Mesh.EveSurfaceTree(
                    document.Surface.Id,
                    ToPortableComponent(document.Surface.Root),
                    document.Surface.Styles
                        .Select(style => new GameCult.Mesh.EveSurfaceStyleToken(style.Name, style.Value))
                        .ToArray()),
                document.Commands
                    .Select(command => new GameCult.Mesh.EveSurfaceCommandTemplate(command.Operation))
                    .ToArray());
        }

        public static AetheriaRuntimeSurfaceDocument FromPortableSurface(GameCult.Mesh.EveSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return new AetheriaRuntimeSurfaceDocument(
                document.ProviderId,
                document.ProviderKind,
                document.Title,
                document.Version,
                document.UpdatedAtUtc,
                new AetheriaRuntimeSurfaceTree(
                    document.Surface.Id,
                    FromPortableComponent(document.Surface.Root),
                    document.Surface.Styles
                        .Select(style => new AetheriaRuntimeSurfaceStyleToken(style.Name, style.Value))
                        .ToArray()),
                document.Commands
                    .Select(command => new AetheriaRuntimeSurfaceCommandTemplate(
                        FromPortableOperation(command.Operation)))
                    .ToArray());
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)
        {
            return ToEveSurfaceDocument(document, null);
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(GameCult.Mesh.EveSurfaceDocument document)
        {
            return ToEveSurfaceDocument(document, null);
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(
            GameCult.Mesh.EveSurfaceDocument document,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return ToEveSurfaceDocument(FromPortableSurface(document), stateRefResolver);
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(
            AetheriaRuntimeSurfaceDocument document,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var surface = new EveUiSurfaceDocument(
                "surface-state",
                "gamecult.eve.surface.v1",
                document.ProviderId,
                document.ProviderKind,
                document.Title,
                document.Version,
                document.UpdatedAtUtc,
                new EveUiSurfaceTree(
                    document.Surface.Id,
                    ToEveSurfaceComponent(document.Surface.Root),
                    document.Surface.Styles
                        .Select(style => new EveUiStyleToken(style.Name, style.Value))
                        .ToArray()),
                document.Commands
                    .Select(command => new EveUiCommandTemplate(command.Operation))
                    .ToArray());

            return ResolveStateRefs(surface, stateRefResolver);
        }

        public static EveUiSurfaceDocument ResolveStateRefs(
            EveUiSurfaceDocument surface,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (stateRefResolver == null)
                return surface;

            var resolveStateRef = stateRefResolver.AsFunc();
            return new EveUiSurfaceDocument(
                surface.Type,
                surface.Schema,
                surface.ProviderId,
                surface.ProviderKind,
                surface.Title,
                surface.Version,
                surface.UpdatedAtUtc,
                new EveUiSurfaceTree(
                    surface.Surface.Id,
                    ResolveStateRefs(surface.Surface.Root, resolveStateRef),
                    surface.Surface.Styles),
                surface.Commands);
        }

        public static EveUiSurfaceDocument EmptySurface(string surfaceId)
        {
            var id = string.IsNullOrWhiteSpace(surfaceId) ? "aetheria.surface.missing" : surfaceId;
            return new EveUiSurfaceDocument(
                "surface-state",
                "gamecult.eve.surface.v1",
                "aetheria.daemon",
                "daemon",
                "",
                0,
                "",
                new EveUiSurfaceTree(
                    id,
                    new EveUiSurfaceComponent(
                        id + ".missing",
                        "surface",
                        new Dictionary<string, string>(StringComparer.Ordinal),
                        Array.Empty<EveUiSurfaceComponent>()),
                    Array.Empty<EveUiStyleToken>()),
                Array.Empty<EveUiCommandTemplate>());
        }

        private static EveUiSurfaceComponent ToEveSurfaceComponent(AetheriaRuntimeSurfaceComponent component)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            AetheriaRuntimeSurfaceStateBindings.AddPointerProps(props, component.StateBindings);
            return new EveUiSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                component.Children.Select(ToEveSurfaceComponent).ToArray(),
                component.StateBindings.Select(ToCultMeshStateBinding).ToArray(),
                component.EmbeddedDocuments.Select(ToEveEmbeddedDocumentSlot).ToArray());
        }

        private static EveUiEmbeddedDocumentSlot ToEveEmbeddedDocumentSlot(AetheriaRuntimeEmbeddedDocumentSlot slot)
        {
            return new EveUiEmbeddedDocumentSlot(
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

        private static EveUiSurfaceComponent ResolveStateRefs(
            EveUiSurfaceComponent component,
            Func<string, string> resolveStateRef)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            ResolvePropRefs(props, resolveStateRef);

            return new EveUiSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                ResolveStateRefs(component.Children, resolveStateRef),
                component.StateBindings,
                component.EmbeddedDocuments);
        }

        private static IReadOnlyList<EveUiSurfaceComponent> ResolveStateRefs(
            IReadOnlyList<EveUiSurfaceComponent> children,
            Func<string, string> resolveStateRef)
        {
            if (children == null || children.Count == 0)
                return Array.Empty<EveUiSurfaceComponent>();

            var resolved = new EveUiSurfaceComponent[children.Count];
            for (var index = 0; index < children.Count; index++)
                resolved[index] = ResolveStateRefs(children[index], resolveStateRef);
            return resolved;
        }

        private static AetheriaRuntimeSurfaceComponent FromPortableComponent(GameCult.Mesh.EveSurfaceComponent component)
        {
            return new AetheriaRuntimeSurfaceComponent(
                component.Id,
                component.Kind,
                new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
                component.Children.Select(FromPortableComponent).ToArray(),
                component.StateBindings.Select(FromPortableStateBinding).ToArray(),
                component.EmbeddedDocuments.Select(FromPortableEmbeddedDocumentSlot).ToArray());
        }

        private static AetheriaRuntimeEmbeddedDocumentSlot FromPortableEmbeddedDocumentSlot(
            GameCult.Mesh.EveEmbeddedDocumentSlot slot)
        {
            return new AetheriaRuntimeEmbeddedDocumentSlot(
                slot.SlotId,
                slot.DocumentId,
                slot.SchemaId,
                slot.PresentationKind,
                FromPortableRoute(slot.RouteHint));
        }

        private static CultMeshStateBindingDescriptor FromPortableStateBinding(
            GameCult.Mesh.EveSurfaceStateBinding binding)
        {
            return CultMesh.StateBindingRecord(
                binding.TargetProp,
                binding.PointerId,
                binding.SourceId,
                binding.SchemaId,
                binding.RouteHint.Kind,
                binding.RouteHint.Description).ToBinding();
        }

        private static CultMeshOperationBindingDescriptor FromPortableOperation(
            GameCult.Mesh.EveSurfaceOperationBinding operation)
        {
            return CultMesh.OperationBindingRecord(
                operation.OperationId,
                operation.Label,
                operation.SchemaId,
                operation.RouteHint.Kind,
                operation.RouteHint.Description).ToBinding();
        }

        private static CultMeshRouteHint FromPortableRoute(GameCult.Mesh.EveSurfaceRouteHint route)
        {
            return new CultMeshRouteRecord(route.Kind, route.Description).ToRoute(CultMeshRouteHint.Automatic);
        }

        private static GameCult.Mesh.EveSurfaceComponent ToPortableComponent(AetheriaRuntimeSurfaceComponent component)
        {
            var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
            AetheriaRuntimeSurfaceStateBindings.AddPointerProps(props, component.StateBindings);

            return new GameCult.Mesh.EveSurfaceComponent(
                component.Id,
                component.Kind,
                props,
                component.Children.Select(ToPortableComponent).ToArray(),
                component.StateBindings
                    .Select(GameCult.Mesh.EveSurfaceStateBinding.FromDescriptor)
                    .ToArray(),
                component.EmbeddedDocuments
                    .Select(ToPortableEmbeddedDocumentSlot)
                    .ToArray());
        }

        private static GameCult.Mesh.EveEmbeddedDocumentSlot ToPortableEmbeddedDocumentSlot(
            AetheriaRuntimeEmbeddedDocumentSlot slot)
        {
            return new GameCult.Mesh.EveEmbeddedDocumentSlot(
                slot.SlotId,
                slot.DocumentId,
                slot.SchemaId,
                slot.PresentationKind,
                GameCult.Mesh.EveSurfaceRouteHint.FromRoute(slot.RouteHint));
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
