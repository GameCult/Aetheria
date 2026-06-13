using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Eve.Surface
{
    public sealed class EveSurfaceDocument
    {
        public EveSurfaceDocument(
            string type,
            string schema,
            string providerId,
            string providerKind,
            string title,
            long version,
            string updatedAtUtc,
            EveSurfaceTree surface,
            IReadOnlyList<EveCommandTemplate> commands)
        {
            Type = type;
            Schema = schema;
            ProviderId = providerId;
            ProviderKind = providerKind;
            Title = title;
            Version = version;
            UpdatedAtUtc = updatedAtUtc;
            Surface = surface;
            Commands = commands;
        }

        public string Type { get; }

        public string Schema { get; }

        public string ProviderId { get; }

        public string ProviderKind { get; }

        public string Title { get; }

        public long Version { get; }

        public string UpdatedAtUtc { get; }

        public EveSurfaceTree Surface { get; }

        public IReadOnlyList<EveCommandTemplate> Commands { get; }
    }

    public sealed class EveSurfaceTree
    {
        public EveSurfaceTree(string id, EveSurfaceComponent root, IReadOnlyList<EveStyleToken> styles)
        {
            Id = id;
            Root = root;
            Styles = styles;
        }

        public string Id { get; }

        public EveSurfaceComponent Root { get; }

        public IReadOnlyList<EveStyleToken> Styles { get; }
    }

    public sealed class EveSurfaceComponent
    {
        public EveSurfaceComponent(
            string id,
            string kind,
            IReadOnlyDictionary<string, string> props,
            IReadOnlyList<EveSurfaceComponent> children)
        {
            Id = id;
            Kind = kind;
            Props = props;
            Children = children;
        }

        public string Id { get; }

        public string Kind { get; }

        public IReadOnlyDictionary<string, string> Props { get; }

        public IReadOnlyList<EveSurfaceComponent> Children { get; }

        public string GetProp(string key, string fallback = "")
        {
            return Props.TryGetValue(key, out var value) ? value : fallback;
        }
    }

    public sealed class EveStyleToken
    {
        public EveStyleToken(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }

    public sealed class EveCommandTemplate
    {
        public EveCommandTemplate(string command, string label, string transport)
        {
            Command = command;
            Label = label;
            Transport = transport;
        }

        public string Command { get; }

        public string Label { get; }

        public string Transport { get; }
    }

    public sealed class EveSurfaceCommandRequest
    {
        public EveSurfaceCommandRequest(
            string providerId,
            string surfaceId,
            string command,
            IReadOnlyDictionary<string, string> payload,
            DateTimeOffset issuedAt,
            string clientId)
        {
            ProviderId = providerId;
            SurfaceId = surfaceId;
            Command = command;
            Payload = payload;
            IssuedAt = issuedAt;
            ClientId = clientId;
        }

        public string ProviderId { get; }

        public string SurfaceId { get; }

        public string Command { get; }

        public IReadOnlyDictionary<string, string> Payload { get; }

        public DateTimeOffset IssuedAt { get; }

        public string ClientId { get; }
    }
}
