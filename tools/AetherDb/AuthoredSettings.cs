/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using YamlDotNet.RepresentationModel;

// Reads the authored gameplay and generation settings out of Assets/Resources/Settings.asset, the Unity asset whose
// GameSettings ScriptableObject holds plain ServerShared classes. Those classes carry curves and tiers with no
// defaults, so a fixture that invented numbers would measure a game nobody plays; this reads the real ones.
//
// Unity assets are YAML, tags and all, so YamlDotNet does the parsing. What remains here is mapping a parsed node
// onto a settings class by field name: an unknown key goes to Unplaced rather than silently landing somewhere wrong.
public sealed class AuthoredSettings
{
    private readonly YamlMappingNode _root;
    public List<string> Unplaced { get; } = new List<string>();

    private AuthoredSettings(YamlMappingNode root) => _root = root;

    public static AuthoredSettings Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "Assets", "Resources", "Settings.asset");
        if (!File.Exists(path)) throw new FileNotFoundException($"no settings asset at {path}");

        var stream = new YamlStream();
        using (var reader = new StreamReader(path)) stream.Load(reader);

        // A Unity asset is one document per object; the settings live under the MonoBehaviour mapping
        var document = stream.Documents
            .Select(d => d.RootNode as YamlMappingNode)
            .FirstOrDefault(n => n != null && n.Children.Keys.OfType<YamlScalarNode>().Any(k => k.Value == "MonoBehaviour"));
        if (document == null) throw new InvalidDataException($"{path} holds no MonoBehaviour document");

        return new AuthoredSettings((YamlMappingNode) document.Children[new YamlScalarNode("MonoBehaviour")]);
    }

    // Fills a new instance of T from the named subtree, by field name
    public T Read<T>(string subtree) where T : new()
    {
        var node = Find(_root, subtree);
        if (node == null) throw new InvalidDataException($"settings asset has no {subtree} subtree");
        var value = new T();
        Populate(value, node, subtree);
        return value;
    }

    // Prints the parsed shape under a subtree
    public void Dump(string subtree, int maxDepth = 2)
    {
        var node = Find(_root, subtree);
        if (node == null)
        {
            Console.WriteLine($"{subtree}: not found");
            return;
        }
        Console.WriteLine($"{subtree}:");
        DumpNode(node, 1, maxDepth);
    }

    private static void DumpNode(YamlNode node, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var pad = new string(' ', depth * 2);
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var child in mapping.Children)
                {
                    Console.WriteLine($"{pad}{child.Key}: {Describe(child.Value)}");
                    DumpNode(child.Value, depth + 1, maxDepth);
                }
                break;
            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    Console.WriteLine($"{pad}[{i}] {Describe(sequence.Children[i])}");
                    DumpNode(sequence.Children[i], depth + 1, maxDepth);
                }
                break;
        }
    }

    private static string Describe(YamlNode node) => node switch
    {
        YamlScalarNode scalar => scalar.Value,
        YamlSequenceNode sequence => $"<{sequence.Children.Count} items>",
        YamlMappingNode mapping => $"<{mapping.Children.Count} keys>",
        _ => "<?>"
    };

    private static YamlMappingNode Find(YamlNode node, string key)
    {
        if (!(node is YamlMappingNode mapping)) return null;
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalar && scalar.Value == key) return child.Value as YamlMappingNode;
            var deeper = Find(child.Value, key);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private void Populate(object target, YamlMappingNode node, string path)
    {
        foreach (var entry in node.Children)
        {
            if (!(entry.Key is YamlScalarNode key)) continue;
            var field = target.GetType().GetField(key.Value, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                Unplaced.Add($"{path}.{key.Value}");
                continue;
            }

            var converted = Convert(entry.Value, field.FieldType, $"{path}.{key.Value}");
            if (converted != null) field.SetValue(target, converted);
        }
    }

    private object Convert(YamlNode node, Type type, string path)
    {
        if (node is YamlScalarNode scalar)
        {
            if (type == typeof(float)) return float.TryParse(scalar.Value, out var f) ? f : 0f;
            if (type == typeof(int)) return int.TryParse(scalar.Value, out var i) ? i : 0;
            if (type == typeof(uint)) return uint.TryParse(scalar.Value, out var u) ? u : 0u;
            if (type == typeof(bool)) return scalar.Value == "1" || string.Equals(scalar.Value, "true", StringComparison.OrdinalIgnoreCase);
            if (type == typeof(string)) return scalar.Value;
            Unplaced.Add($"{path} (scalar into {type.Name})");
            return null;
        }

        if (node is YamlSequenceNode sequence && type.IsArray)
        {
            var elementType = type.GetElementType();
            var array = Array.CreateInstance(elementType, sequence.Children.Count);
            for (var index = 0; index < sequence.Children.Count; index++)
                array.SetValue(Convert(sequence.Children[index], elementType, $"{path}[{index}]"), index);
            return array;
        }

        if (node is YamlMappingNode mapping)
        {
            if (type == typeof(float3))
                return new float3(Leaf(mapping, "x"), Leaf(mapping, "y"), Leaf(mapping, "z"));

            // A Unity object reference ({fileID, guid}) is an asset nothing here reads
            if (mapping.Children.Keys.OfType<YamlScalarNode>().Any(k => k.Value == "fileID")) return null;

            if (type.IsClass)
            {
                var value = Activator.CreateInstance(type);
                Populate(value, mapping, path);
                return value;
            }
        }

        Unplaced.Add($"{path} (unsupported {node.NodeType} into {type.Name})");
        return null;
    }

    private static float Leaf(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var child)
        && child is YamlScalarNode scalar
        && float.TryParse(scalar.Value, out var value)
            ? value
            : 0f;
}
