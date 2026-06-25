using System;
using System.Threading.Tasks;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaUi
    {
        private readonly AetheriaClient _client;

        internal AetheriaUi(AetheriaClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<CultMeshOperationReceipt> InputSettingsAsync(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string? clientId = null,
            bool flush = true)
        {
            return await _client
                .SubmitInputSettingsCommandAsync(command, body, clientId, flush)
                .ConfigureAwait(false);
        }

        public async Task<CultMeshOperationReceipt> SaveLoadoutTemplateAsync(
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string? clientId = null,
            bool flush = true)
        {
            return await _client
                .SubmitLoadoutTemplateCommandAsync(loadoutTemplate, clientId, flush)
                .ConfigureAwait(false);
        }

        public async Task<CultMeshOperationReceipt> SurfaceCommandAsync(
            EveSurfaceCommandRequest request,
            string? clientId = null,
            bool flush = true)
        {
            return await _client
                .SubmitKnownSurfaceCommandAsync(request, clientId, flush)
                .ConfigureAwait(false);
        }
    }
}
