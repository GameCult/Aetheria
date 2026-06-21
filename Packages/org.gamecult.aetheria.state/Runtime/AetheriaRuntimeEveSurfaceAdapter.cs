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
            if (document == null) throw new ArgumentNullException(nameof(document));

            return new EveSurfaceDocument(
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
    }
}
