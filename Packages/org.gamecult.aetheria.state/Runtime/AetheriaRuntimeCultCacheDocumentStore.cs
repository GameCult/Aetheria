using System;
using System.Buffers;
using System.IO;
using GameCult.Caching;
using GameCult.Mesh;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal static class AetheriaRuntimeCultCacheDocumentStore
    {
        public static void WriteEveCommand(string path, AetheriaRuntimeEveCommandDocument document)
        {
            WriteDocument(
                path,
                $"command:gamecult.eve.command.{document.CommandId}.v1",
                AetheriaRuntimeEveCommandDocument.SchemaId,
                "gamecult.eve.command",
                "gamecult.eve.command.v1",
                document.IssuedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeEveCommandDocument ReadEveCommand(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeEveCommandDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeEveCommandDocument.SchemaId));
        }

        public static void WriteDaemonCommand(string path, AetheriaRuntimeDaemonCommandDocument document)
        {
            WriteDocument(
                path,
                $"command:gamecult.aetheria.daemon_command.{document.CommandId}.v1",
                AetheriaRuntimeDaemonSchemas.Command,
                "gamecult.aetheria.daemon_command",
                "gamecult.aetheria.daemon_command.v1",
                document.IssuedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeDaemonCommandDocument ReadDaemonCommand(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonCommandDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.Command));
        }

        public static void WriteDaemonFrame(string path, AetheriaRuntimeDaemonFrameDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.daemon_frame.{document.SessionId}.v1",
                AetheriaRuntimeDaemonSchemas.Frame,
                "gamecult.aetheria.daemon_frame",
                "gamecult.aetheria.daemon_frame.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeDaemonFrameDocument ReadDaemonFrame(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonFrameDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.Frame));
        }

        public static void WriteDaemonSoaView(string path, AetheriaRuntimeDaemonSoaViewDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.daemon_soa_view.{document.SessionId}.v1",
                AetheriaRuntimeDaemonSchemas.SoaView,
                "gamecult.aetheria.daemon_soa_view",
                "gamecult.aetheria.daemon_soa_view.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeDaemonSoaViewDocument ReadDaemonSoaView(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonSoaViewDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.SoaView));
        }

        public static void WriteAssetManifest(string path, AetheriaRuntimeAssetManifestDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.asset_manifest.{document.RunId}.v1",
                AetheriaRuntimeDaemonSchemas.AssetManifest,
                "gamecult.aetheria.asset_manifest",
                "gamecult.aetheria.asset_manifest.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeAssetManifestDocument ReadAssetManifest(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeAssetManifestDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.AssetManifest));
        }

        public static void WriteDaemonProviderAdvertisement(
            string path,
            AetheriaRuntimeDaemonProviderAdvertisementDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.daemon_provider.{document.DaemonId}.v1",
                AetheriaRuntimeDaemonSchemas.ProviderAdvertisement,
                "gamecult.aetheria.daemon_provider_advertisement",
                "gamecult.aetheria.daemon_provider_advertisement.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeDaemonProviderAdvertisementDocument ReadDaemonProviderAdvertisement(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.ProviderAdvertisement));
        }

        public static void WriteDaemonHealth(string path, AetheriaRuntimeDaemonHealthDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.daemon_health.{document.DaemonId}.v1",
                AetheriaRuntimeDaemonSchemas.Health,
                "gamecult.aetheria.daemon_health",
                "gamecult.aetheria.daemon_health.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeDaemonHealthDocument ReadDaemonHealth(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonHealthDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.Health));
        }

        public static void WriteVerseAuthorityPolicy(
            string path,
            AetheriaRuntimeVerseAuthorityPolicyDocument document)
        {
            WriteDocument(
                path,
                AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey,
                AetheriaRuntimeVerseAuthoritySchemas.Policy,
                "gamecult.aetheria.verse_authority_policy",
                "gamecult.aetheria.verse_authority_policy.v1",
                document.UpdatedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeVerseAuthorityPolicyDocument ReadVerseAuthorityPolicy(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeVerseAuthoritySchemas.Policy));
        }

        public static void WriteDaemonCommandBoundary(
            string path,
            AetheriaRuntimeDaemonCommandBoundaryDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.daemon_command_boundary.{document.BoundaryId}.v1",
                AetheriaRuntimeDaemonSchemas.CommandBoundary,
                "gamecult.aetheria.daemon_command_boundary",
                "gamecult.aetheria.daemon_command_boundary.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeDaemonCommandBoundaryDocument ReadDaemonCommandBoundary(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.CommandBoundary));
        }

        public static void WriteStarbridgeSessionSummary(
            string path,
            AetheriaRuntimeStarbridgeSessionSummaryDocument document)
        {
            WriteDocument(
                path,
                $"latest:gamecult.aetheria.starbridge_session_summary.{document.SessionId}.v1",
                AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                "gamecult.aetheria.starbridge_session_summary",
                "gamecult.aetheria.starbridge_session_summary.v1",
                document.PublishedAtUtc,
                MessagePackSerializer.Serialize(document));
        }

        public static AetheriaRuntimeStarbridgeSessionSummaryDocument ReadStarbridgeSessionSummary(string path)
        {
            return MessagePackSerializer.Deserialize<AetheriaRuntimeStarbridgeSessionSummaryDocument>(
                CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary));
        }

        public static void WriteDaemonGameSurface(string path, AetheriaRuntimeSurfaceDocument document)
        {
            WriteDaemonSurface(
                path,
                document,
                AetheriaRuntimeDaemonSchemas.GameSurface,
                "gamecult.aetheria.daemon_game_surface",
                "gamecult.aetheria.daemon_game_surface.v1");
        }

        public static AetheriaRuntimeSurfaceDocument ReadDaemonGameSurface(string path)
        {
            return ReadRuntimeSurfaceDocument(CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.GameSurface));
        }

        public static void WriteDaemonEditorSurface(string path, AetheriaRuntimeSurfaceDocument document)
        {
            WriteDaemonSurface(
                path,
                document,
                AetheriaRuntimeDaemonSchemas.EditorSurface,
                "gamecult.aetheria.daemon_editor_surface",
                "gamecult.aetheria.daemon_editor_surface.v1");
        }

        public static AetheriaRuntimeSurfaceDocument ReadDaemonEditorSurface(string path)
        {
            return ReadRuntimeSurfaceDocument(CultMesh.ReadSingleFileDocumentPayload(path, AetheriaRuntimeDaemonSchemas.EditorSurface));
        }

        private static void WriteDaemonSurface(
            string path,
            AetheriaRuntimeSurfaceDocument document,
            string schema,
            string schemaName,
            string schemaVersion)
        {
            WriteDocument(
                path,
                $"latest:{schemaName}.{document.Surface.Id}.v1",
                schema,
                schemaName,
                schemaVersion,
                document.UpdatedAtUtc,
                WriteRuntimeSurfaceDocument(document));
        }

        private static void WriteDocument(
            string path,
            string key,
            string schemaId,
            string schemaName,
            string schemaVersion,
            string storedAt,
            byte[] payload)
        {
            CultMesh.WriteSingleFileDocumentPayload(
                path,
                new CultRecordKey(key),
                new CultMeshSingleFileDocumentSchema(schemaId, schemaName, schemaVersion),
                storedAt,
                payload);
        }

        private static byte[] WriteRuntimeSurfaceDocument(AetheriaRuntimeSurfaceDocument document)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);

            writer.WriteArrayHeader(7);
            writer.Write(document.ProviderId ?? "");
            writer.Write(document.ProviderKind ?? "");
            writer.Write(document.Title ?? "");
            writer.Write(document.Version);
            writer.Write(document.UpdatedAtUtc ?? "");
            WriteSurfaceTree(ref writer, document.Surface);
            WriteSurfaceCommands(ref writer, document.Commands);
            writer.Flush();

            return buffer.WrittenSpan.ToArray();
        }

        private static void WriteSurfaceTree(ref MessagePackWriter writer, AetheriaRuntimeSurfaceTree tree)
        {
            tree ??= new AetheriaRuntimeSurfaceTree(
                "",
                new AetheriaRuntimeSurfaceComponent(
                    "",
                    "surface",
                    new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                    Array.Empty<AetheriaRuntimeSurfaceComponent>()),
                Array.Empty<AetheriaRuntimeSurfaceStyleToken>());

            writer.WriteArrayHeader(3);
            writer.Write(tree.Id ?? "");
            WriteSurfaceComponent(ref writer, tree.Root);
            writer.WriteArrayHeader(tree.Styles?.Count ?? 0);
            foreach (var style in tree.Styles ?? Array.Empty<AetheriaRuntimeSurfaceStyleToken>())
            {
                writer.WriteArrayHeader(2);
                writer.Write(style.Name ?? "");
                writer.Write(style.Value ?? "");
            }
        }

        private static void WriteSurfaceComponent(ref MessagePackWriter writer, AetheriaRuntimeSurfaceComponent component)
        {
            component ??= new AetheriaRuntimeSurfaceComponent(
                "",
                "",
                new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                Array.Empty<AetheriaRuntimeSurfaceComponent>());

            writer.WriteArrayHeader(5);
            writer.Write(component.Id ?? "");
            writer.Write(component.Kind ?? "");
            writer.WriteMapHeader(component.Props?.Count ?? 0);
            foreach (var prop in component.Props ?? new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal))
            {
                writer.Write(prop.Key ?? "");
                writer.Write(prop.Value ?? "");
            }

            writer.WriteArrayHeader(component.Children?.Count ?? 0);
            foreach (var child in component.Children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>())
            {
                WriteSurfaceComponent(ref writer, child);
            }

            writer.WriteArrayHeader(component.StateBindings?.Count ?? 0);
            foreach (var binding in component.StateBindings ?? Array.Empty<CultMeshStateBindingDescriptor>())
            {
                var record = CultMesh.StateBindingRecord(binding);
                writer.WriteArrayHeader(6);
                writer.Write(record.TargetProp);
                writer.Write(record.PointerId);
                writer.Write(record.SourceId);
                writer.Write(record.SchemaId);
                writer.Write(record.RouteKind);
                writer.Write(record.RouteDescription);
            }
        }

        private static void WriteSurfaceCommands(
            ref MessagePackWriter writer,
            System.Collections.Generic.IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> commands)
        {
            writer.WriteArrayHeader(commands?.Count ?? 0);
            foreach (var command in commands ?? Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>())
            {
                var record = CultMesh.OperationBindingRecord(command.Operation);
                writer.WriteArrayHeader(5);
                writer.Write(record.OperationId);
                writer.Write(record.Label);
                writer.Write(record.SchemaId);
                writer.Write(record.RouteKind);
                writer.Write(record.RouteDescription);
            }
        }

        private static AetheriaRuntimeSurfaceDocument ReadRuntimeSurfaceDocument(byte[] payload)
        {
            var reader = new MessagePackReader(payload);
            var fields = reader.ReadArrayHeader();
            var providerId = ReadFieldString(ref reader, fields, 0, "");
            var providerKind = ReadFieldString(ref reader, fields, 1, "");
            var title = ReadFieldString(ref reader, fields, 2, "");
            var version = ReadFieldInt64(ref reader, fields, 3, 0);
            var updatedAtUtc = ReadFieldString(ref reader, fields, 4, "");
            var surface = ReadFieldSurfaceTree(ref reader, fields, 5);
            var commands = ReadFieldSurfaceCommands(ref reader, fields, 6);
            for (var index = 7; index < fields; index++)
            {
                reader.Skip();
            }

            return new AetheriaRuntimeSurfaceDocument(
                providerId,
                providerKind,
                title,
                version,
                updatedAtUtc,
                surface,
                commands);
        }

        private static AetheriaRuntimeSurfaceTree ReadFieldSurfaceTree(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return new AetheriaRuntimeSurfaceTree(
                    "",
                    new AetheriaRuntimeSurfaceComponent(
                        "",
                        "surface",
                        new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                        Array.Empty<AetheriaRuntimeSurfaceComponent>()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>());
            }

            var treeFields = reader.ReadArrayHeader();
            var id = ReadFieldString(ref reader, treeFields, 0, "");
            var root = ReadFieldSurfaceComponent(ref reader, treeFields, 1);
            var styles = ReadFieldSurfaceStyles(ref reader, treeFields, 2);
            for (var field = 3; field < treeFields; field++)
            {
                reader.Skip();
            }

            return new AetheriaRuntimeSurfaceTree(id, root, styles);
        }

        private static AetheriaRuntimeSurfaceComponent ReadFieldSurfaceComponent(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return new AetheriaRuntimeSurfaceComponent(
                    "",
                    "",
                    new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal),
                    Array.Empty<AetheriaRuntimeSurfaceComponent>());
            }

            var componentFields = reader.ReadArrayHeader();
            var id = ReadFieldString(ref reader, componentFields, 0, "");
            var kind = ReadFieldString(ref reader, componentFields, 1, "");
            var props = ReadFieldStringMap(ref reader, componentFields, 2);
            var children = ReadFieldSurfaceComponents(ref reader, componentFields, 3);
            var stateBindings = ReadFieldSurfaceStateBindings(ref reader, componentFields, 4);
            for (var field = 5; field < componentFields; field++)
            {
                reader.Skip();
            }

            return new AetheriaRuntimeSurfaceComponent(id, kind, props, children, stateBindings);
        }

        private static System.Collections.Generic.IReadOnlyList<CultMeshStateBindingDescriptor> ReadFieldSurfaceStateBindings(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return Array.Empty<CultMeshStateBindingDescriptor>();
            }

            var count = reader.ReadArrayHeader();
            var bindings = new CultMeshStateBindingDescriptor[count];
            for (var item = 0; item < count; item++)
            {
                var bindingFields = reader.ReadArrayHeader();
                var targetProp = ReadFieldString(ref reader, bindingFields, 0, "value");
                var pointerId = ReadFieldString(ref reader, bindingFields, 1, "");
                var sourceId = ReadFieldString(ref reader, bindingFields, 2, "");
                var schemaId = ReadFieldString(ref reader, bindingFields, 3, "");
                var routeKind = ReadFieldString(ref reader, bindingFields, 4, "");
                var routeDescription = ReadFieldString(ref reader, bindingFields, 5, "");
                for (var field = 6; field < bindingFields; field++)
                {
                    reader.Skip();
                }

                bindings[item] = CultMesh.StateBindingRecord(
                    targetProp,
                    pointerId,
                    sourceId,
                    schemaId,
                    routeKind,
                    routeDescription).ToBinding();
            }

            return bindings;
        }

        private static System.Collections.Generic.IReadOnlyList<AetheriaRuntimeSurfaceComponent> ReadFieldSurfaceComponents(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return Array.Empty<AetheriaRuntimeSurfaceComponent>();
            }

            var count = reader.ReadArrayHeader();
            var children = new AetheriaRuntimeSurfaceComponent[count];
            for (var child = 0; child < count; child++)
            {
                children[child] = ReadFieldSurfaceComponent(ref reader, 1, 0);
            }

            return children;
        }

        private static System.Collections.Generic.IReadOnlyDictionary<string, string> ReadFieldStringMap(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return values;
            }

            var count = reader.ReadMapHeader();
            for (var item = 0; item < count; item++)
            {
                values[reader.ReadString() ?? ""] = reader.ReadString() ?? "";
            }

            return values;
        }

        private static System.Collections.Generic.IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> ReadFieldSurfaceStyles(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return Array.Empty<AetheriaRuntimeSurfaceStyleToken>();
            }

            var count = reader.ReadArrayHeader();
            var styles = new AetheriaRuntimeSurfaceStyleToken[count];
            for (var style = 0; style < count; style++)
            {
                var styleFields = reader.ReadArrayHeader();
                var name = ReadFieldString(ref reader, styleFields, 0, "");
                var value = ReadFieldString(ref reader, styleFields, 1, "");
                for (var field = 2; field < styleFields; field++)
                {
                    reader.Skip();
                }

                styles[style] = new AetheriaRuntimeSurfaceStyleToken(name, value);
            }

            return styles;
        }

        private static System.Collections.Generic.IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> ReadFieldSurfaceCommands(
            ref MessagePackReader reader,
            int fields,
            int index)
        {
            if (index >= fields || reader.NextMessagePackType == MessagePackType.Nil)
            {
                if (index < fields)
                    reader.ReadNil();

                return Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>();
            }

            var count = reader.ReadArrayHeader();
            var commands = new AetheriaRuntimeSurfaceCommandTemplate[count];
            for (var command = 0; command < count; command++)
            {
                var commandFields = reader.ReadArrayHeader();
                var name = ReadFieldString(ref reader, commandFields, 0, "");
                var label = ReadFieldString(ref reader, commandFields, 1, "");
                var legacyTransportOrSchema = ReadFieldString(ref reader, commandFields, 2, "");
                var routeKind = commandFields > 3
                    ? ReadFieldString(ref reader, commandFields, 3, "")
                    : nameof(CultMeshLocalityKind.Automatic);
                var routeDescription = commandFields > 4
                    ? ReadFieldString(ref reader, commandFields, 4, "")
                    : legacyTransportOrSchema;
                for (var field = 5; field < commandFields; field++)
                {
                    reader.Skip();
                }

                commands[command] = new AetheriaRuntimeSurfaceCommandTemplate(
                    CultMesh.OperationBindingRecord(
                        name,
                        label,
                        commandFields > 3 ? legacyTransportOrSchema : "",
                        routeKind,
                        routeDescription).ToBinding());
            }

            return commands;
        }

        private static string ReadFieldString(ref MessagePackReader reader, int fields, int index, string fallback)
        {
            return index >= fields ? fallback : reader.ReadString() ?? fallback;
        }

        private static int ReadFieldInt32(ref MessagePackReader reader, int fields, int index, int fallback)
        {
            if (index >= fields)
            {
                return fallback;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return fallback;
            }

            return reader.ReadInt32();
        }

        private static long ReadFieldInt64(ref MessagePackReader reader, int fields, int index, long fallback)
        {
            if (index >= fields)
            {
                return fallback;
            }

            if (reader.NextMessagePackType == MessagePackType.Nil)
            {
                reader.ReadNil();
                return fallback;
            }

            return reader.ReadInt64();
        }
    }
}
