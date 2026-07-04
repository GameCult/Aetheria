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
    public static class AetheriaRuntimeSurfaceStateRefs
    {
        public const string Source = "stateRef";
        public const string Value = "valueRef";
        public const string Label = "labelRef";
        public const string Format = "stateFormat";

        public static (string Key, string Value) SourceRef(string reference)
        {
            return (Source, reference ?? "");
        }

        public static (string Key, string Value) ValueRef(string reference)
        {
            return (Value, reference ?? "");
        }

        public static (string Key, string Value) LabelRef(string reference)
        {
            return (Label, reference ?? "");
        }

        public static (string Key, string Value) FormatRef(string format)
        {
            return (Format, format ?? "");
        }
    }

    public static class AetheriaRuntimeSurfaceStateBindings
    {
        public const string PropPrefix = "cultmesh.statePointer.";
        public const string PointerIdSuffix = ".pointerId";
        public const string SourceIdSuffix = ".sourceId";
        public const string SchemaIdSuffix = ".schemaId";
        public const string RouteKindSuffix = ".routeKind";
        public const string RouteDescriptionSuffix = ".routeDescription";

        public static CultMeshStateBindingDescriptor ForDaemonStateRef(
            string targetProp,
            string stateRef,
            string schemaId = AetheriaRuntimeDaemonSchemas.Frame)
        {
            return new CultMeshStateBindingDescriptor(
                targetProp,
                ToPointerId(stateRef),
                stateRef,
                schemaId,
                new CultMeshRouteHint(
                    CultMeshLocalityKind.SharedMemory,
                    "daemon-published CultCache state"));
        }

        public static IReadOnlyList<CultMeshStateBindingDescriptor> FromProps(
            IReadOnlyDictionary<string, string> props)
        {
            if (props == null || props.Count == 0)
                return Array.Empty<CultMeshStateBindingDescriptor>();

            var bindings = new List<CultMeshStateBindingDescriptor>();
            AddPropBinding(bindings, "value", Get(props, AetheriaRuntimeSurfaceStateRefs.Source));
            foreach (var prop in props)
            {
                if (string.IsNullOrWhiteSpace(prop.Value) ||
                    string.Equals(prop.Key, AetheriaRuntimeSurfaceStateRefs.Source, StringComparison.Ordinal) ||
                    !prop.Key.EndsWith("Ref", StringComparison.Ordinal))
                {
                    continue;
                }

                AddPropBinding(bindings, prop.Key.Substring(0, prop.Key.Length - "Ref".Length), prop.Value);
            }

            return bindings;
        }

        public static void AddPointerProps(
            IDictionary<string, string> props,
            IReadOnlyList<CultMeshStateBindingDescriptor> bindings)
        {
            if (props == null || bindings == null)
                return;

            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.PointerId))
                    continue;

                var record = CultMesh.StateBindingRecord(binding);
                var prefix = PropPrefix + record.TargetProp;
                props[prefix + PointerIdSuffix] = record.PointerId;
                props[prefix + SourceIdSuffix] = record.SourceId;
                props[prefix + SchemaIdSuffix] = record.SchemaId;
                props[prefix + RouteKindSuffix] = record.RouteKind;
                props[prefix + RouteDescriptionSuffix] = record.RouteDescription;
            }
        }

        private static void AddPropBinding(
            List<CultMeshStateBindingDescriptor> bindings,
            string targetProp,
            string stateRef)
        {
            if (string.IsNullOrWhiteSpace(stateRef))
                return;

            var schemaId = stateRef.StartsWith(AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix + "/", StringComparison.Ordinal)
                ? AetheriaRuntimeDaemonSchemas.CurrentEntity
                : AetheriaRuntimeDaemonSchemas.Frame;
            bindings.Add(ForDaemonStateRef(targetProp, stateRef, schemaId));
        }

        private static string Get(IReadOnlyDictionary<string, string> props, string key)
        {
            return props.TryGetValue(key, out var value) ? value : "";
        }

        private static string ToPointerId(string stateRef)
        {
            if (string.IsNullOrWhiteSpace(stateRef))
                return "";

            var chars = stateRef
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '.')
                .ToArray();
            var pointerId = new string(chars).Trim('.');
            while (pointerId.Contains("..", StringComparison.Ordinal))
                pointerId = pointerId.Replace("..", ".", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(pointerId) ? "aetheria.state.unknown" : pointerId;
        }
    }

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
            IReadOnlyList<AetheriaRuntimeEmbeddedDocumentSlot> embeddedDocuments,
            IReadOnlyDictionary<string, string>? layout = null,
            IReadOnlyDictionary<string, string>? style = null)
        {
            Id = id ?? "";
            Kind = kind ?? "";
            Props = props ?? new Dictionary<string, string>(StringComparer.Ordinal);
            Children = children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>();
            StateBindings = stateBindings ?? Array.Empty<CultMeshStateBindingDescriptor>();
            EmbeddedDocuments = embeddedDocuments ?? Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>();
            Layout = layout ?? new Dictionary<string, string>(StringComparer.Ordinal);
            Style = style ?? new Dictionary<string, string>(StringComparer.Ordinal);
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

        [Key(6)]
        public IReadOnlyDictionary<string, string> Layout { get; }

        [Key(7)]
        public IReadOnlyDictionary<string, string> Style { get; }
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
        public static EveUiSurfaceDocument ToPortableSurface(AetheriaRuntimeSurfaceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return new EveUiSurfaceDocument(
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
        }

        public static AetheriaRuntimeSurfaceDocument FromPortableSurface(EveUiSurfaceDocument document)
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
                        command.Operation))
                    .ToArray());
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)
        {
            return ToEveSurfaceDocument(document, null);
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(EveUiSurfaceDocument document)
        {
            return ToEveSurfaceDocument(document, null);
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(
            EveUiSurfaceDocument document,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return ResolveStateRefs(document, stateRefResolver);
        }

        public static EveUiSurfaceDocument ToEveSurfaceDocument(
            AetheriaRuntimeSurfaceDocument document,
            CultMeshStateRefResolver? stateRefResolver)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return ResolveStateRefs(ToPortableSurface(document), stateRefResolver);
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
                component.EmbeddedDocuments.Select(ToEveEmbeddedDocumentSlot).ToArray(),
                new Dictionary<string, string>(component.Layout, StringComparer.Ordinal),
                new Dictionary<string, string>(component.Style, StringComparer.Ordinal));
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

        private static CultMeshStateBindingDescriptor FromPortableStateBinding(
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
                component.EmbeddedDocuments,
                component.Layout,
                component.Style);
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

        private static AetheriaRuntimeSurfaceComponent FromPortableComponent(EveUiSurfaceComponent component)
        {
            return new AetheriaRuntimeSurfaceComponent(
                component.Id,
                component.Kind,
                new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
                component.Children.Select(FromPortableComponent).ToArray(),
                component.StateBindings.Select(FromPortableStateBinding).ToArray(),
                component.EmbeddedDocuments.Select(FromPortableEmbeddedDocumentSlot).ToArray(),
                new Dictionary<string, string>(component.Layout, StringComparer.Ordinal),
                new Dictionary<string, string>(component.Style, StringComparer.Ordinal));
        }

        private static AetheriaRuntimeEmbeddedDocumentSlot FromPortableEmbeddedDocumentSlot(
            EveUiEmbeddedDocumentSlot slot)
        {
            return new AetheriaRuntimeEmbeddedDocumentSlot(
                slot.SlotId,
                slot.DocumentId,
                slot.SchemaId,
                slot.PresentationKind,
                slot.RouteHint);
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
