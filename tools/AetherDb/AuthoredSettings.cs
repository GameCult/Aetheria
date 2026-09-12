/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;

// Reads the authored gameplay and generation settings out of Assets/Resources/Settings.asset, the Unity asset whose
// GameSettings ScriptableObject holds plain ServerShared classes. Those classes carry curves and tiers with no
// defaults, so a fixture that invented numbers would measure a game nobody plays; this reads the real ones.
//
// The asset is Unity YAML whose leaves under these subtrees are scalars, sequences of mappings, or float3 as x/y/z.
// Parsed by indentation and matched to fields by name, so a renamed or added field simply goes unset rather than
// silently mapping to the wrong thing. Anything it could not place is reported by Unplaced.
public sealed class AuthoredSettings
{
    private readonly Node _root;
    public List<string> Unplaced { get; } = new List<string>();

    private AuthoredSettings(Node root) => _root = root;

    // Prints the parsed tree under a subtree, for checking that sequences landed on the field that declares them
    public void Dump(string subtree, int maxDepth = 3)
    {
        var node = Find(_root, subtree);
        if (node == null)
        {
            Console.WriteLine($"{subtree}: not found");
            return;
        }
        Console.WriteLine($"{subtree}: {node.Children.Count} children, {node.Items.Count} items");
        DumpNode(node, 1, maxDepth);
    }

    private static void DumpNode(Node node, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var pad = new string(' ', depth * 2);
        foreach (var child in node.Children)
        {
            Console.WriteLine($"{pad}{child.Key}: scalar={child.Value.Scalar ?? "-"} children={child.Value.Children.Count} items={child.Value.Items.Count}");
            DumpNode(child.Value, depth + 1, maxDepth);
        }
        for (var i = 0; i < node.Items.Count; i++)
            Console.WriteLine($"{pad}[{i}] scalar={node.Items[i].Scalar ?? "-"} children={node.Items[i].Children.Count}");
    }

    public static AuthoredSettings Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "Assets", "Resources", "Settings.asset");
        if (!File.Exists(path)) throw new FileNotFoundException($"no settings asset at {path}");
        return new AuthoredSettings(Parse(File.ReadAllLines(path)));
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

    private Node Find(Node node, string key)
    {
        if (node.Children.TryGetValue(key, out var found)) return found;
        foreach (var child in node.Children.Values)
        {
            var deeper = Find(child, key);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private void Populate(object target, Node node, string path)
    {
        foreach (var entry in node.Children)
        {
            var field = target.GetType().GetField(entry.Key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                Unplaced.Add($"{path}.{entry.Key}");
                continue;
            }

            var converted = Convert(entry.Value, field.FieldType, $"{path}.{entry.Key}");
            if (converted != null) field.SetValue(target, converted);
        }
    }

    private object Convert(Node node, Type type, string path)
    {
        if (type == typeof(float)) return float.TryParse(node.Scalar, out var f) ? f : 0f;
        if (type == typeof(int)) return int.TryParse(node.Scalar, out var i) ? i : 0;
        if (type == typeof(uint)) return uint.TryParse(node.Scalar, out var u) ? u : 0u;
        if (type == typeof(bool)) return node.Scalar == "1" || string.Equals(node.Scalar, "true", StringComparison.OrdinalIgnoreCase);
        if (type == typeof(string)) return node.Scalar;

        if (type == typeof(float3))
            return float3(Leaf(node, "x"), Leaf(node, "y"), Leaf(node, "z"));

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            var array = Array.CreateInstance(elementType, node.Items.Count);
            for (var index = 0; index < node.Items.Count; index++)
            {
                // A sequence of scalars carries its value on the item itself; a sequence of mappings has children
                var item = node.Items[index];
                var converted = item.Scalar != null && item.Children.Count == 0
                    ? Convert(new Node { Scalar = item.Scalar }, elementType, $"{path}[{index}]")
                    : Convert(item, elementType, $"{path}[{index}]");
                array.SetValue(converted, index);
            }
            return array;
        }

        if (type.IsClass)
        {
            var value = Activator.CreateInstance(type);
            Populate(value, node, path);
            return value;
        }

        Unplaced.Add($"{path} (unsupported type {type.Name})");
        return null;
    }

    private static float Leaf(Node node, string key) =>
        node.Children.TryGetValue(key, out var child) && float.TryParse(child.Scalar, out var value) ? value : 0f;

    private static float3 float3(float x, float y, float z) => new float3(x, y, z);

    // One YAML node: a scalar, a mapping of children, or a sequence of items.
    private sealed class Node
    {
        public string Scalar;
        public Dictionary<string, Node> Children { get; } = new Dictionary<string, Node>();
        public List<Node> Items { get; } = new List<Node>();
    }

    // Indentation-driven parse of the three shapes this asset uses:
    //   key: value / key:            a scalar or a mapping
    //   - value                      a sequence of scalars      (NeutralFactions, MinimapZoomLevels)
    //   - key: value + deeper keys   a sequence of mappings      (Tiers, BodySettingsCollections)
    // A mapping item's first key sits at the dash's own column while its siblings are indented past it, so one
    // frame stands for the whole item and accepts both depths. Inline flow mappings ({fileID: ...}) are Unity
    // object references, which nothing here reads, so they are kept as raw scalars rather than half parsed.
    private static Node Parse(string[] lines)
    {
        var root = new Node();
        // A frame is either a mapping a key opened, or one item of a sequence. The distinction matters because a
        // dash shares its column with its container: an item must pop the previous item without popping the
        // container that holds them both.
        var stack = new List<(int indent, bool isItemFrame, Node node)> { (-1, false, root) };

        foreach (var raw in lines)
        {
            var trimmed = raw.TrimEnd();
            if (trimmed.Length == 0) continue;

            var indent = trimmed.Length - trimmed.TrimStart(' ').Length;
            var content = trimmed.TrimStart(' ');
            if (content.StartsWith("#") || content.StartsWith("%") || content.StartsWith("---")) continue;

            var isItem = content.StartsWith("- ");
            if (isItem) content = content.Substring(2).TrimStart();

            // A key pops every frame at or inside its column. A dash pops frames deeper than its column, plus any
            // item frame at its column (the preceding item of the same sequence), leaving that sequence's container.
            while (stack.Count > 1)
            {
                var top = stack[stack.Count - 1];
                var pop = isItem
                    ? top.indent > indent || (top.indent == indent && top.isItemFrame)
                    : top.indent >= indent;
                if (!pop) break;
                stack.RemoveAt(stack.Count - 1);
            }
            var container = stack[stack.Count - 1].node;

            if (isItem)
            {
                if (content.StartsWith("{"))
                {
                    container.Items.Add(new Node { Scalar = content });
                    continue;
                }

                var item = new Node();
                container.Items.Add(item);
                // The item's own keys sit at this column and deeper, so its frame claims the dash's column
                stack.Add((indent, true, item));
                container = item;

                if (!content.Contains(":"))
                {
                    item.Scalar = content;
                    continue;
                }
            }

            var separator = content.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0) continue;

            var key = content.Substring(0, separator);
            var value = content.Substring(separator + 1).Trim();
            if (value.Length == 0)
            {
                // A key with no value opens a container. Its frame sits past its own column so that a sequence
                // whose dashes share that column still attaches to it rather than to its parent.
                var child = new Node();
                container.Children[key] = child;
                stack.Add((indent, false, child));
            }
            else container.Children[key] = new Node { Scalar = value };
        }

        return root;
    }
}
