using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class AetheriaEveFileSurfacePresenter : MonoBehaviour
    {
        public const string DebugSurfacePathEnvironmentVariable = "AETHERIA_CULTUI_DEBUG_SURFACE_PATH";
        public const string DefaultDebugSurfaceRelativePath = "GameData/cultui-debug-surface.cultui";

        [SerializeField]
        private string surfaceFilePath = "";

        [SerializeField]
        private bool mountOnEnable = true;

        [SerializeField]
        private bool refreshInUpdate = true;

        [SerializeField]
        private float refreshIntervalSeconds = 0.1f;

        private UIDocument? _document;
        private string _mountedPath = "";
        private DateTime _mountedWriteTimeUtc = DateTime.MinValue;
        private long _mountedLength = -1;
        private float _nextRefreshTime;

        public string SurfaceFilePath
        {
            get => surfaceFilePath;
            set => surfaceFilePath = value ?? "";
        }

        private void OnEnable()
        {
            if (mountOnEnable)
                Mount();
        }

        private void Update()
        {
            if (!refreshInUpdate || Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, refreshIntervalSeconds);
            RefreshIfChanged();
        }

        public void Mount()
        {
            var path = ResolveSurfacePath(surfaceFilePath);
            var document = ResolveDocument();
            if (!File.Exists(path))
            {
                MountError(document, $"CultUI debug surface file not found: {path}");
                _mountedPath = "";
                return;
            }

            try
            {
                var surface = AetheriaEveFileSurfaceDocuments.Read(path);
                AetheriaEveUnitySurfaceHost.Render(
                    transform,
                    document,
                    "Aetheria CultUI File Surface",
                    surface,
                    request => Debug.Log(
                        $"CultUI debug file surface command preview: {request.ProviderId}/{request.SurfaceId}/{request.Operation.OperationId}"),
                    CreateChrome());
                CaptureMountedState(path);
            }
            catch (Exception ex)
            {
                MountError(document, $"Failed to read CultUI debug surface file: {ex.Message}");
                _mountedPath = "";
            }
        }

        private void RefreshIfChanged()
        {
            var path = ResolveSurfacePath(surfaceFilePath);
            if (!File.Exists(path))
                return;

            var info = new FileInfo(path);
            if (string.Equals(_mountedPath, path, StringComparison.Ordinal) &&
                _mountedWriteTimeUtc == info.LastWriteTimeUtc &&
                _mountedLength == info.Length)
            {
                return;
            }

            Mount();
        }

        private UIDocument ResolveDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
            return _document;
        }

        private void CaptureMountedState(string path)
        {
            var info = new FileInfo(path);
            _mountedPath = path;
            _mountedWriteTimeUtc = info.LastWriteTimeUtc;
            _mountedLength = info.Length;
        }

        private static void MountError(UIDocument document, string message)
        {
            var root = document.rootVisualElement;
            root.Clear();
            var label = new Label(message);
            label.AddToClassList("aetheria-eve-runtime-error-label");
            root.Add(label);
            Debug.LogWarning(message);
        }

        public static string ResolveSurfacePath(string configuredPath)
        {
            var environmentPath = Environment.GetEnvironmentVariable(DebugSurfacePathEnvironmentVariable);
            var path = string.IsNullOrWhiteSpace(configuredPath) ? environmentPath : configuredPath;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(ProjectRoot(), DefaultDebugSurfaceRelativePath);
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(ProjectRoot(), path));
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static AetheriaEveUnitySurfaceChrome CreateChrome()
        {
            return new AetheriaEveUnitySurfaceChrome
            {
                UseShell = true,
                RootAlignItems = Align.FlexEnd,
                RootJustifyContent = Justify.FlexStart,
                RootPaddingTop = 72f,
                RootPaddingRight = 24f,
                MaxWidth = 420f,
                MinWidth = 360f,
                BackgroundColor = new Color(0.05f, 0.07f, 0.09f, 0.94f),
                BorderColor = new Color(0.35f, 0.64f, 0.74f, 0.82f)
            };
        }
    }

    public static class AetheriaEveFileSurfaceDocuments
    {
        public static EveSurfaceDocument Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("CultUI surface path is required.", nameof(path));

            using var stream = File.OpenRead(path);
            var serializer = new DataContractJsonSerializer(
                typeof(FileSurfaceDocument),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
            var document = (FileSurfaceDocument?)serializer.ReadObject(stream)
                ?? throw new InvalidDataException("CultUI surface file is empty.");

            return ToEveSurfaceDocument(document, path);
        }

        private static EveSurfaceDocument ToEveSurfaceDocument(FileSurfaceDocument document, string path)
        {
            var surface = document.Surface;
            var surfaceId = string.IsNullOrWhiteSpace(surface?.Id)
                ? "aetheria.debug.file_surface"
                : surface!.Id!;
            var root = document.Surface?.Root == null
                ? new EveSurfaceComponent(
                    surfaceId + ".root",
                    "surface",
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    Array.Empty<EveSurfaceComponent>())
                : ToComponent(document.Surface.Root);
            var writeTime = File.GetLastWriteTimeUtc(path);
            var providerId = string.IsNullOrWhiteSpace(document.ProviderId) ? "aetheria.debug" : document.ProviderId!;
            var providerKind = string.IsNullOrWhiteSpace(document.ProviderKind) ? "debug.file_surface" : document.ProviderKind!;
            var updatedAtUtc = string.IsNullOrWhiteSpace(document.UpdatedAtUtc) ? writeTime.ToString("O") : document.UpdatedAtUtc!;
            return new EveSurfaceDocument(
                providerId,
                providerKind,
                document.Title ?? "",
                document.Version <= 0 ? writeTime.Ticks : document.Version,
                updatedAtUtc,
                new EveSurfaceTree(
                    surfaceId,
                    root,
                    (document.Surface?.Styles ?? Array.Empty<FileStyleToken>())
                        .Select(style => new EveStyleToken(style.Name ?? "", style.Value ?? ""))
                        .ToArray()),
                (document.Commands ?? Array.Empty<FileCommand>())
                    .Where(command => !string.IsNullOrWhiteSpace(command.Command))
                    .Select(command => new EveCommandTemplate(CultMesh.OperationBindingRecord(
                        command.Command ?? "",
                        command.Label ?? command.Command ?? "",
                        "",
                        nameof(CultMeshLocalityKind.Automatic),
                        command.Transport ?? "debug-log").ToBinding()))
                    .ToArray());
        }

        private static EveSurfaceComponent ToComponent(FileSurfaceComponent component)
        {
            return new EveSurfaceComponent(
                component.Id ?? "",
                component.Kind ?? "",
                ToProps(component.Props),
                (component.Children ?? Array.Empty<FileSurfaceComponent>())
                    .Select(ToComponent)
                    .ToArray());
        }

        private static Dictionary<string, string> ToProps(IReadOnlyList<FileProp>? props)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (props == null)
                return result;

            foreach (var prop in props)
            {
                var key = prop.Key;
                if (!string.IsNullOrWhiteSpace(key))
                    result[key!] = prop.Value ?? "";
            }

            return result;
        }

#pragma warning disable 0649
        [DataContract]
        private sealed class FileSurfaceDocument
        {
            [DataMember(Name = "schema")]
            public string? Schema;

            [DataMember(Name = "providerId")]
            public string? ProviderId;

            [DataMember(Name = "providerKind")]
            public string? ProviderKind;

            [DataMember(Name = "title")]
            public string? Title;

            [DataMember(Name = "version")]
            public long Version;

            [DataMember(Name = "updatedAtUtc")]
            public string? UpdatedAtUtc;

            [DataMember(Name = "surface")]
            public FileSurfaceTree? Surface;

            [DataMember(Name = "commands")]
            public FileCommand[]? Commands;
        }

        [DataContract]
        private sealed class FileSurfaceTree
        {
            [DataMember(Name = "id")]
            public string? Id;

            [DataMember(Name = "root")]
            public FileSurfaceComponent? Root;

            [DataMember(Name = "styles")]
            public FileStyleToken[]? Styles;
        }

        [DataContract]
        private sealed class FileSurfaceComponent
        {
            [DataMember(Name = "id")]
            public string? Id;

            [DataMember(Name = "kind")]
            public string? Kind;

            [DataMember(Name = "props")]
            public FileProp[]? Props;

            [DataMember(Name = "children")]
            public FileSurfaceComponent[]? Children;
        }

        [DataContract]
        private sealed class FileProp
        {
            [DataMember(Name = "key")]
            public string? Key;

            [DataMember(Name = "value")]
            public string? Value;
        }

        [DataContract]
        private sealed class FileStyleToken
        {
            [DataMember(Name = "name")]
            public string? Name;

            [DataMember(Name = "value")]
            public string? Value;
        }

        [DataContract]
        private sealed class FileCommand
        {
            [DataMember(Name = "command")]
            public string? Command;

            [DataMember(Name = "label")]
            public string? Label;

            [DataMember(Name = "transport")]
            public string? Transport;
        }
#pragma warning restore 0649
    }
}
