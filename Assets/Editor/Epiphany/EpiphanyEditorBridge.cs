#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameCult.Epiphany.Unity
{
    public static class EpiphanyEditorBridge
    {
        private const int DefaultMaxObjects = 600;
        private const int DefaultMaxProperties = 100;
        private const int DefaultMaxDependencies = 2000;
        private const string ArtifactDirArg = "-epiphanyArtifactDir";
        private const string OperationArg = "-epiphanyOperation";
        private const string SceneArg = "-epiphanyScene";
        private const string AssetArg = "-epiphanyAsset";
        private const string GuidArg = "-epiphanyGuid";
        private const string MaxObjectsArg = "-epiphanyMaxObjects";
        private const string MaxPropertiesArg = "-epiphanyMaxProperties";
        private const string MaxDependenciesArg = "-epiphanyMaxDependencies";

        private static string currentArtifactDir;

        [MenuItem("Epiphany/Inspect Project")]
        public static void InspectProject()
        {
            RunCommand("inspect-project", CaptureProjectFacts);
        }

        [MenuItem("Epiphany/Check Compilation")]
        public static void CheckCompilation()
        {
            RunCommand("check-compilation", CaptureCompilationFacts);
        }

        [MenuItem("Epiphany/Capture Scene Facts")]
        public static void CaptureSceneFacts()
        {
            RunCommand("scene-facts", delegate { return CaptureSceneFactsPayload(GetScenePathArg()); });
        }

        [MenuItem("Epiphany/Capture Prefab Facts")]
        public static void CapturePrefabFacts()
        {
            RunCommand("prefab-facts", delegate { return CapturePrefabFactsPayload(GetAssetPathArg()); });
        }

        [MenuItem("Epiphany/Capture Serialized Object Facts")]
        public static void CaptureSerializedObjectFacts()
        {
            RunCommand("serialized-object", delegate { return CaptureSerializedObjectFactsPayload(GetAssetPathArg()); });
        }

        [MenuItem("Epiphany/Reference Search")]
        public static void ReferenceSearch()
        {
            RunCommand("reference-search", CaptureReferenceSearchPayload);
        }

        public static void RunProbe()
        {
            string operation = GetArg(OperationArg, "inspect-project");
            switch (operation)
            {
                case "inspect-project":
                    InspectProject();
                    break;
                case "check-compilation":
                    CheckCompilation();
                    break;
                case "scene-facts":
                    CaptureSceneFacts();
                    break;
                case "prefab-facts":
                    CapturePrefabFacts();
                    break;
                case "serialized-object":
                    CaptureSerializedObjectFacts();
                    break;
                case "reference-search":
                    ReferenceSearch();
                    break;
                default:
                    RunCommand(operation, delegate
                    {
                        Dictionary<string, object> payload = BaseResult("unityProbeResult", operation);
                        payload["status"] = "blocked";
                        payload["evidenceSummary"] = "Unknown Epiphany Unity probe operation.";
                        payload["knownOperations"] = new List<object>
                        {
                            "inspect-project",
                            "check-compilation",
                            "scene-facts",
                            "prefab-facts",
                            "serialized-object",
                            "reference-search"
                        };
                        return payload;
                    });
                    EditorApplication.Exit(2);
                    break;
            }
        }

        private static void RunCommand(string operation, Func<Dictionary<string, object>> buildPayload)
        {
            string artifactDir = ResolveArtifactDir(operation);
            currentArtifactDir = artifactDir;
            Directory.CreateDirectory(artifactDir);

            Dictionary<string, object> payload;
            try
            {
                payload = buildPayload();
                if (!payload.ContainsKey("operation"))
                {
                    payload["operation"] = operation;
                }
                if (!payload.ContainsKey("status"))
                {
                    payload["status"] = "passed";
                }
            }
            catch (Exception error)
            {
                payload = BaseResult("unityProbeResult", operation);
                payload["status"] = "failed";
                payload["error"] = error.ToString();
                payload["evidenceSummary"] = "The Epiphany Unity editor bridge failed while gathering editor facts.";
                WriteArtifacts(artifactDir, operation, payload);
                Debug.LogError(error);
                EditorApplication.Exit(1);
                return;
            }

            WriteArtifacts(artifactDir, operation, payload);
            if (StringEquals(payload, "status", "failed"))
            {
                EditorApplication.Exit(1);
            }
        }

        private static Dictionary<string, object> CaptureProjectFacts()
        {
            Dictionary<string, object> payload = BaseResult("unityProjectInspection", "inspect-project");
            payload["status"] = "passed";
            payload["unityVersion"] = Application.unityVersion;
            payload["projectPath"] = ProjectRoot();
            payload["dataPath"] = Application.dataPath;
            payload["isBatchMode"] = Application.isBatchMode;
            payload["isCompiling"] = EditorApplication.isCompiling;
            payload["isUpdating"] = EditorApplication.isUpdating;
            payload["activeScene"] = SceneSummary(SceneManager.GetActiveScene());
            payload["buildScenes"] = BuildScenes();
            payload["renderPipeline"] = RenderPipelineSummary();
            payload["assetDatabase"] = AssetDatabaseSummary();
            payload["evidenceSummary"] = "Unity editor project facts were captured from inside the pinned editor.";
            WriteNamedJson("project-facts.json", payload);
            return payload;
        }

        private static Dictionary<string, object> CaptureCompilationFacts()
        {
            Dictionary<string, object> payload = BaseResult("unityCompilationProbe", "check-compilation");
            bool compiling = EditorApplication.isCompiling;
            bool updating = EditorApplication.isUpdating;
            payload["status"] = compiling || updating ? "pending" : "passed";
            payload["isCompiling"] = compiling;
            payload["isUpdating"] = updating;
            payload["unityVersion"] = Application.unityVersion;
            payload["loadedAssemblies"] = LoadedAssemblySummary();
            payload["evidenceSummary"] = compiling || updating
                ? "The editor bridge ran, but Unity reports compilation or asset update still in progress."
                : "The editor bridge executed inside Unity, which means editor scripts compiled far enough for probes to run.";
            WriteNamedJson("compilation.json", payload);
            return payload;
        }

        private static Dictionary<string, object> CaptureSceneFactsPayload(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                scenePath = DefaultScenePath();
            }
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new InvalidOperationException("No scene path was supplied and no default build scene exists.");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            Dictionary<string, object> payload = BaseResult("unitySceneFacts", "scene-facts");
            payload["status"] = "passed";
            payload["scene"] = SceneSummary(scene);
            payload["rootCount"] = scene.rootCount;
            payload["isDirty"] = scene.isDirty;

            int maxObjects = GetIntArg(MaxObjectsArg, DefaultMaxObjects);
            List<object> objects = new List<object>();
            int totalObjects = 0;
            int missingScripts = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                WalkGameObject(root, objects, ref totalObjects, ref missingScripts, maxObjects);
            }
            payload["totalGameObjectCount"] = totalObjects;
            payload["capturedGameObjectCount"] = objects.Count;
            payload["truncated"] = totalObjects > objects.Count;
            payload["missingScriptCount"] = missingScripts;
            payload["gameObjects"] = objects;
            payload["evidenceSummary"] = "Scene hierarchy, components, prefab links, and serialized fields were captured inside Unity.";
            WriteNamedJson("scene-facts.json", payload);
            return payload;
        }

        private static Dictionary<string, object> CapturePrefabFactsPayload(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException("Prefab facts require -epiphanyAsset Assets/.../Prefab.prefab.");
            }
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                Dictionary<string, object> payload = BaseResult("unityPrefabFacts", "prefab-facts");
                payload["status"] = "passed";
                payload["assetPath"] = assetPath;
                payload["guid"] = AssetDatabase.AssetPathToGUID(assetPath);
                payload["prefabAssetType"] = PrefabUtility.GetPrefabAssetType(root).ToString();
                payload["root"] = ObjectReferenceSummary(root);

                int maxObjects = GetIntArg(MaxObjectsArg, DefaultMaxObjects);
                List<object> objects = new List<object>();
                int totalObjects = 0;
                int missingScripts = 0;
                WalkGameObject(root, objects, ref totalObjects, ref missingScripts, maxObjects);
                payload["totalGameObjectCount"] = totalObjects;
                payload["capturedGameObjectCount"] = objects.Count;
                payload["truncated"] = totalObjects > objects.Count;
                payload["missingScriptCount"] = missingScripts;
                payload["gameObjects"] = objects;
                payload["evidenceSummary"] = "Prefab hierarchy, nested instances, overrides, components, and serialized fields were captured inside Unity.";
                WriteNamedJson("prefab-facts.json", payload);
                return payload;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Dictionary<string, object> CaptureSerializedObjectFactsPayload(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException("Serialized object facts require -epiphanyAsset Assets/... path.");
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Dictionary<string, object> payload = BaseResult("unitySerializedObjectFacts", "serialized-object");
            payload["status"] = "passed";
            payload["assetPath"] = assetPath;
            payload["guid"] = AssetDatabase.AssetPathToGUID(assetPath);
            payload["assetCount"] = assets == null ? 0 : assets.Length;

            List<object> targets = new List<object>();
            if (assets != null)
            {
                foreach (UnityEngine.Object asset in assets)
                {
                    if (asset == null)
                    {
                        continue;
                    }
                    targets.Add(SerializedObjectSummary(asset));
                }
            }

            payload["targets"] = targets;
            payload["evidenceSummary"] = "Serialized Unity object properties were captured through SerializedObject inside the editor.";
            WriteNamedJson("serialized-object-facts.json", payload);
            return payload;
        }

        private static Dictionary<string, object> CaptureReferenceSearchPayload()
        {
            string assetPath = GetArg(AssetArg, null);
            string guid = GetArg(GuidArg, null);
            if (string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(assetPath))
            {
                guid = AssetDatabase.AssetPathToGUID(assetPath);
            }
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("Reference search requires -epiphanyGuid or -epiphanyAsset.");
            }

            string targetPath = AssetDatabase.GUIDToAssetPath(guid);
            int maxDependencies = GetIntArg(MaxDependenciesArg, DefaultMaxDependencies);
            Dictionary<string, object> payload = BaseResult("unityReferenceSearch", "reference-search");
            payload["status"] = "passed";
            payload["targetGuid"] = guid;
            payload["targetPath"] = targetPath;

            List<object> matches = new List<object>();
            int scanned = 0;
            string[] paths = AssetDatabase.GetAllAssetPaths();
            foreach (string path in paths)
            {
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                scanned++;
                string[] dependencies = AssetDatabase.GetDependencies(path, true);
                bool found = false;
                foreach (string dependency in dependencies)
                {
                    if (string.Equals(dependency, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    continue;
                }
                Dictionary<string, object> match = new Dictionary<string, object>();
                match["path"] = path;
                match["guid"] = AssetDatabase.AssetPathToGUID(path);
                match["type"] = AssetTypeName(path);
                matches.Add(match);
                if (matches.Count >= maxDependencies)
                {
                    break;
                }
            }

            payload["scannedAssetCount"] = scanned;
            payload["matchCount"] = matches.Count;
            payload["truncated"] = matches.Count >= maxDependencies;
            payload["matches"] = matches;
            payload["evidenceSummary"] = "Unity asset dependencies were searched through AssetDatabase.";
            WriteNamedJson("reference-search.json", payload);
            return payload;
        }

        private static void WalkGameObject(GameObject gameObject, List<object> output, ref int totalObjects, ref int missingScripts, int maxObjects)
        {
            totalObjects++;
            if (output.Count < maxObjects)
            {
                output.Add(GameObjectSummary(gameObject, ref missingScripts));
            }
            else
            {
                Component[] missingCountOnly = gameObject.GetComponents<Component>();
                foreach (Component component in missingCountOnly)
                {
                    if (component == null)
                    {
                        missingScripts++;
                    }
                }
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                WalkGameObject(transform.GetChild(i).gameObject, output, ref totalObjects, ref missingScripts, maxObjects);
            }
        }

        private static Dictionary<string, object> GameObjectSummary(GameObject gameObject, ref int missingScripts)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["name"] = gameObject.name;
            summary["path"] = GameObjectPath(gameObject);
            summary["activeSelf"] = gameObject.activeSelf;
            summary["activeInHierarchy"] = gameObject.activeInHierarchy;
            summary["tag"] = gameObject.tag;
            summary["layer"] = gameObject.layer;
            summary["scenePath"] = gameObject.scene.path;
            summary["globalObjectId"] = GlobalId(gameObject);
            summary["prefab"] = PrefabSummary(gameObject);

            List<object> components = new List<object>();
            Component[] componentArray = gameObject.GetComponents<Component>();
            foreach (Component component in componentArray)
            {
                if (component == null)
                {
                    missingScripts++;
                    Dictionary<string, object> missing = new Dictionary<string, object>();
                    missing["missing"] = true;
                    components.Add(missing);
                    continue;
                }
                components.Add(ComponentSummary(component));
            }
            summary["components"] = components;
            return summary;
        }

        private static Dictionary<string, object> ComponentSummary(Component component)
        {
            Dictionary<string, object> summary = ObjectReferenceSummary(component);
            Behaviour behaviour = component as Behaviour;
            if (behaviour != null)
            {
                summary["enabled"] = behaviour.enabled;
            }
            Renderer renderer = component as Renderer;
            if (renderer != null)
            {
                List<object> materials = new List<object>();
                foreach (Material material in renderer.sharedMaterials)
                {
                    materials.Add(ObjectReferenceSummary(material));
                }
                summary["sharedMaterials"] = materials;
            }
            summary["serializedProperties"] = SerializedProperties(component, GetIntArg(MaxPropertiesArg, DefaultMaxProperties));
            return summary;
        }

        private static Dictionary<string, object> SerializedObjectSummary(UnityEngine.Object target)
        {
            Dictionary<string, object> summary = ObjectReferenceSummary(target);
            summary["serializedProperties"] = SerializedProperties(target, GetIntArg(MaxPropertiesArg, DefaultMaxProperties));

            GameObject gameObject = target as GameObject;
            if (gameObject != null)
            {
                int missing = 0;
                summary["gameObject"] = GameObjectSummary(gameObject, ref missing);
                summary["missingScriptCount"] = missing;
            }

            return summary;
        }

        private static List<object> SerializedProperties(UnityEngine.Object target, int maxProperties)
        {
            List<object> properties = new List<object>();
            if (target == null)
            {
                return properties;
            }

            try
            {
                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty iterator = serializedObject.GetIterator();
                bool enterChildren = true;
                int count = 0;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    properties.Add(SerializedPropertySummary(iterator));
                    count++;
                    if (count >= maxProperties)
                    {
                        Dictionary<string, object> truncated = new Dictionary<string, object>();
                        truncated["propertyPath"] = "<truncated>";
                        truncated["reason"] = "max property limit reached";
                        truncated["maxProperties"] = maxProperties;
                        properties.Add(truncated);
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                Dictionary<string, object> failed = new Dictionary<string, object>();
                failed["propertyPath"] = "<error>";
                failed["error"] = error.Message;
                properties.Add(failed);
            }
            return properties;
        }

        private static Dictionary<string, object> SerializedPropertySummary(SerializedProperty property)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["propertyPath"] = property.propertyPath;
            summary["displayName"] = property.displayName;
            summary["type"] = property.propertyType.ToString();
            summary["isArray"] = property.isArray;
            summary["hasVisibleChildren"] = property.hasVisibleChildren;
            summary["value"] = SerializedPropertyValue(property);
            return summary;
        }

        private static object SerializedPropertyValue(SerializedProperty property)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        return property.longValue;
                    case SerializedPropertyType.Boolean:
                        return property.boolValue;
                    case SerializedPropertyType.Float:
                        return property.doubleValue;
                    case SerializedPropertyType.String:
                        return property.stringValue;
                    case SerializedPropertyType.Color:
                        return ColorSummary(property.colorValue);
                    case SerializedPropertyType.ObjectReference:
                        return ObjectReferenceSummary(property.objectReferenceValue);
                    case SerializedPropertyType.LayerMask:
                        return property.intValue;
                    case SerializedPropertyType.Enum:
                        return property.enumDisplayNames != null && property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                            ? property.enumDisplayNames[property.enumValueIndex]
                            : property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                    case SerializedPropertyType.Vector2:
                        return VectorSummary(property.vector2Value);
                    case SerializedPropertyType.Vector3:
                        return VectorSummary(property.vector3Value);
                    case SerializedPropertyType.Vector4:
                        return VectorSummary(property.vector4Value);
                    case SerializedPropertyType.Rect:
                        return RectSummary(property.rectValue);
                    case SerializedPropertyType.ArraySize:
                        return property.intValue;
                    case SerializedPropertyType.Character:
                        return property.intValue;
                    case SerializedPropertyType.Bounds:
                        return BoundsSummary(property.boundsValue);
                    case SerializedPropertyType.Quaternion:
                        return QuaternionSummary(property.quaternionValue);
                    default:
                        Dictionary<string, object> unsupported = new Dictionary<string, object>();
                        unsupported["unsupported"] = true;
                        unsupported["propertyType"] = property.propertyType.ToString();
                        return unsupported;
                }
            }
            catch (Exception error)
            {
                Dictionary<string, object> failed = new Dictionary<string, object>();
                failed["error"] = error.Message;
                return failed;
            }
        }

        private static Dictionary<string, object> PrefabSummary(GameObject gameObject)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["assetType"] = PrefabUtility.GetPrefabAssetType(gameObject).ToString();
            summary["instanceStatus"] = PrefabUtility.GetPrefabInstanceStatus(gameObject).ToString();
            UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            summary["source"] = ObjectReferenceSummary(source);
            summary["nearestInstanceRoot"] = ObjectReferenceSummary(PrefabUtility.GetNearestPrefabInstanceRoot(gameObject));
            return summary;
        }

        private static Dictionary<string, object> ObjectReferenceSummary(UnityEngine.Object value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            if (value == null)
            {
                summary["isNull"] = true;
                return summary;
            }

            summary["isNull"] = false;
            summary["name"] = value.name;
            summary["type"] = value.GetType().FullName;
            summary["globalObjectId"] = GlobalId(value);

            string path = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(path))
            {
                summary["assetPath"] = path;
                summary["guid"] = AssetDatabase.AssetPathToGUID(path);
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                summary["scenePath"] = gameObject.scene.path;
                summary["gameObjectPath"] = GameObjectPath(gameObject);
            }

            Component component = value as Component;
            if (component != null)
            {
                summary["scenePath"] = component.gameObject.scene.path;
                summary["gameObjectPath"] = GameObjectPath(component.gameObject);
            }

            return summary;
        }

        private static Dictionary<string, object> SceneSummary(Scene scene)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["name"] = scene.name;
            summary["path"] = scene.path;
            summary["isLoaded"] = scene.isLoaded;
            summary["isDirty"] = scene.isDirty;
            summary["rootCount"] = scene.rootCount;
            summary["buildIndex"] = scene.buildIndex;
            return summary;
        }

        private static List<object> BuildScenes()
        {
            List<object> scenes = new List<object>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["path"] = scene.path;
                item["guid"] = scene.guid.ToString();
                item["enabled"] = scene.enabled;
                scenes.Add(item);
            }
            return scenes;
        }

        private static Dictionary<string, object> RenderPipelineSummary()
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            UnityEngine.Rendering.RenderPipelineAsset pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            summary["defaultRenderPipeline"] = ObjectReferenceSummary(pipeline);
            return summary;
        }

        private static Dictionary<string, object> AssetDatabaseSummary()
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["globalArtifactDependencyVersion"] = AssetDatabase.GlobalArtifactDependencyVersion;
            summary["globalArtifactProcessedVersion"] = AssetDatabase.GlobalArtifactProcessedVersion;
            summary["activeRefreshImportMode"] = AssetDatabase.ActiveRefreshImportMode.ToString();
            return summary;
        }

        private static List<object> LoadedAssemblySummary()
        {
            List<object> assemblies = new List<object>();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (!name.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith("Aetheria", StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith("Cult", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["name"] = name;
                item["fullName"] = assembly.FullName;
                assemblies.Add(item);
            }
            return assemblies;
        }

        private static string DefaultScenePath()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path))
            {
                return active.path;
            }
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    return scene.path;
                }
            }
            return null;
        }

        private static string GetScenePathArg()
        {
            return NormalizeAssetPath(GetArg(SceneArg, null));
        }

        private static string GetAssetPathArg()
        {
            return NormalizeAssetPath(GetArg(AssetArg, null));
        }

        private static string NormalizeAssetPath(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            return value.Replace("\\", "/");
        }

        private static string AssetTypeName(string path)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type == null ? null : type.FullName;
        }

        private static string GameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }
            List<string> names = new List<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string GlobalId(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return null;
            }
            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, object> BaseResult(string kind, string operation)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["kind"] = kind;
            payload["operation"] = operation;
            payload["generatedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            payload["projectPath"] = ProjectRoot();
            payload["unityVersion"] = Application.unityVersion;
            payload["isBatchMode"] = Application.isBatchMode;
            return payload;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        }

        private static string ResolveArtifactDir(string operation)
        {
            string fromArgs = GetArg(ArtifactDirArg, null);
            if (!string.IsNullOrEmpty(fromArgs))
            {
                return Path.GetFullPath(fromArgs);
            }
            string root = Path.Combine(ProjectRoot(), "EpiphanyArtifacts");
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(root, operation + "-" + stamp);
        }

        private static void WriteArtifacts(string artifactDir, string operation, Dictionary<string, object> payload)
        {
            Directory.CreateDirectory(artifactDir);
            payload["artifactDir"] = artifactDir.Replace("\\", "/");

            string resultPath = Path.Combine(artifactDir, "unity-probe-result.json");
            File.WriteAllText(resultPath, MiniJson.Serialize(payload), Encoding.UTF8);

            string markdownPath = Path.Combine(artifactDir, "unity-probe-result.md");
            File.WriteAllText(markdownPath, RenderMarkdown(payload), Encoding.UTF8);
            Debug.Log("Epiphany Unity bridge wrote " + operation + " artifacts to " + artifactDir);
        }

        private static void WriteNamedJson(string fileName, Dictionary<string, object> payload)
        {
            string artifactDir = string.IsNullOrEmpty(currentArtifactDir)
                ? ResolveArtifactDir(payload.ContainsKey("operation") ? Convert.ToString(payload["operation"]) : "probe")
                : currentArtifactDir;
            WriteNamedJsonFile(artifactDir, fileName, payload);
        }

        private static string RenderMarkdown(Dictionary<string, object> payload)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Epiphany Unity Probe Result");
            builder.AppendLine();
            AppendMarkdownValue(builder, "Kind", payload, "kind");
            AppendMarkdownValue(builder, "Operation", payload, "operation");
            AppendMarkdownValue(builder, "Status", payload, "status");
            AppendMarkdownValue(builder, "Unity", payload, "unityVersion");
            AppendMarkdownValue(builder, "Project", payload, "projectPath");
            AppendMarkdownValue(builder, "Generated", payload, "generatedAt");
            AppendMarkdownValue(builder, "Summary", payload, "evidenceSummary");
            builder.AppendLine();
            builder.AppendLine("Raw facts are in `unity-probe-result.json` and operation-specific JSON files when available.");
            return builder.ToString();
        }

        private static void AppendMarkdownValue(StringBuilder builder, string label, Dictionary<string, object> payload, string key)
        {
            object value;
            if (payload.TryGetValue(key, out value) && value != null)
            {
                builder.Append(label).Append(": ").AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }

        private static void WriteNamedJsonFile(string artifactDir, string fileName, Dictionary<string, object> payload)
        {
            Directory.CreateDirectory(artifactDir);
            File.WriteAllText(Path.Combine(artifactDir, fileName), MiniJson.Serialize(payload), Encoding.UTF8);
        }

        private static Dictionary<string, object> ColorSummary(Color value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["r"] = value.r;
            summary["g"] = value.g;
            summary["b"] = value.b;
            summary["a"] = value.a;
            return summary;
        }

        private static Dictionary<string, object> VectorSummary(Vector2 value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["x"] = value.x;
            summary["y"] = value.y;
            return summary;
        }

        private static Dictionary<string, object> VectorSummary(Vector3 value)
        {
            Dictionary<string, object> summary = VectorSummary((Vector2)value);
            summary["z"] = value.z;
            return summary;
        }

        private static Dictionary<string, object> VectorSummary(Vector4 value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["x"] = value.x;
            summary["y"] = value.y;
            summary["z"] = value.z;
            summary["w"] = value.w;
            return summary;
        }

        private static Dictionary<string, object> RectSummary(Rect value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["x"] = value.x;
            summary["y"] = value.y;
            summary["width"] = value.width;
            summary["height"] = value.height;
            return summary;
        }

        private static Dictionary<string, object> BoundsSummary(Bounds value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["center"] = VectorSummary(value.center);
            summary["size"] = VectorSummary(value.size);
            return summary;
        }

        private static Dictionary<string, object> QuaternionSummary(Quaternion value)
        {
            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["x"] = value.x;
            summary["y"] = value.y;
            summary["z"] = value.z;
            summary["w"] = value.w;
            return summary;
        }

        private static bool StringEquals(Dictionary<string, object> payload, string key, string expected)
        {
            object value;
            return payload.TryGetValue(key, out value)
                && string.Equals(Convert.ToString(value), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetArg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return fallback;
        }

        private static int GetIntArg(string name, int fallback)
        {
            string raw = GetArg(name, null);
            int value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static class MiniJson
        {
            public static string Serialize(object value)
            {
                StringBuilder builder = new StringBuilder();
                WriteValue(builder, value);
                builder.AppendLine();
                return builder.ToString();
            }

            private static void WriteValue(StringBuilder builder, object value)
            {
                if (value == null)
                {
                    builder.Append("null");
                    return;
                }
                string asString = value as string;
                if (asString != null)
                {
                    WriteString(builder, asString);
                    return;
                }
                if (value is bool)
                {
                    builder.Append((bool)value ? "true" : "false");
                    return;
                }
                if (IsNumber(value))
                {
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                }
                IDictionary dictionary = value as IDictionary;
                if (dictionary != null)
                {
                    WriteObject(builder, dictionary);
                    return;
                }
                IEnumerable enumerable = value as IEnumerable;
                if (enumerable != null)
                {
                    WriteArray(builder, enumerable);
                    return;
                }
                WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
            }

            private static void WriteObject(StringBuilder builder, IDictionary dictionary)
            {
                builder.Append("{");
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first)
                    {
                        builder.Append(",");
                    }
                    first = false;
                    WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                    builder.Append(":");
                    WriteValue(builder, entry.Value);
                }
                builder.Append("}");
            }

            private static void WriteArray(StringBuilder builder, IEnumerable enumerable)
            {
                builder.Append("[");
                bool first = true;
                foreach (object item in enumerable)
                {
                    if (!first)
                    {
                        builder.Append(",");
                    }
                    first = false;
                    WriteValue(builder, item);
                }
                builder.Append("]");
            }

            private static void WriteString(StringBuilder builder, string value)
            {
                builder.Append('"');
                foreach (char character in value)
                {
                    switch (character)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (character < 32)
                            {
                                builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(character);
                            }
                            break;
                    }
                }
                builder.Append('"');
            }

            private static bool IsNumber(object value)
            {
                return value is byte
                    || value is sbyte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
            }
        }
    }
}
#endif
