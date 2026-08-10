using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public class UnityGameObjectFormatter : IMessagePackFormatter<GameObject>
{
#if !UNITY_EDITOR
	private static Dictionary<GameObject,string> _paths = new Dictionary<GameObject, string>();
#endif

	public void Serialize(ref MessagePackWriter writer, GameObject value, MessagePackSerializerOptions options)
	{
		if(value==null)
			writer.WriteNil();
		
#if UNITY_EDITOR
		writer.Write(AssetDatabase.GetAssetPath(value));
#else
		string path;
		if (!_paths.TryGetValue(value, out path))
			path = "RUNTIME_SERIALIZER_UNKNOWN_RESOURCE";
		writer.Write(path);
#endif
	}

	public GameObject Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		var path = reader.ReadString();
		if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
		return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
		var resource = Resources.Load<GameObject>(path.Substring("Assets/Resources/".Length).Split('.').First());
		_paths[resource] = path;
		return resource;
#endif
	}
}

public class UnityMaterialFormatter : IMessagePackFormatter<Material>
{
#if !UNITY_EDITOR
	private static Dictionary<Material,string> _paths = new Dictionary<Material, string>();
#endif

	public void Serialize(ref MessagePackWriter writer, Material value, MessagePackSerializerOptions options)
	{
		if(value==null)
			writer.WriteNil();
		
#if UNITY_EDITOR
		writer.Write(AssetDatabase.GetAssetPath(value));
#else
		string path;
		if (!_paths.TryGetValue(value, out path))
			path = "RUNTIME_SERIALIZER_UNKNOWN_RESOURCE";
		writer.Write(path);
#endif
	}

	public Material Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		var path = reader.ReadString();
		if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
		return AssetDatabase.LoadAssetAtPath<Material>(path);
#else
		var resource = Resources.Load<Material>(path.Substring("Assets/Resources/".Length).Split('.').First());
		_paths[resource] = path;
		return resource;
#endif
	}
}

public class UnitySpriteFormatter : IMessagePackFormatter<Sprite>
{
#if !UNITY_EDITOR
	private static Dictionary<Sprite,string> _paths = new Dictionary<Sprite, string>();
#endif

	public void Serialize(ref MessagePackWriter writer, Sprite value, MessagePackSerializerOptions options)
	{
		if(value==null)
			writer.WriteNil();
		
#if UNITY_EDITOR
		writer.Write(AssetDatabase.GetAssetPath(value));
#else
		string path;
		if (!_paths.TryGetValue(value, out path))
			path = "RUNTIME_SERIALIZER_UNKNOWN_RESOURCE";
		writer.Write(path);
#endif
	}

	public Sprite Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		var path = reader.ReadString();
		if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
		var resource = Resources.Load<Sprite>(path.Substring("Assets/Resources/".Length).Split('.').First());
		_paths[resource] = path;
		return resource;
#endif
	}
}

public class UnityTexture2DFormatter : IMessagePackFormatter<Texture2D>
{
#if !UNITY_EDITOR
	private static Dictionary<Texture2D,string> _paths = new Dictionary<Texture2D, string>();
#endif

	public void Serialize(ref MessagePackWriter writer, Texture2D value, MessagePackSerializerOptions options)
	{
		if(value==null)
			writer.WriteNil();
		
#if UNITY_EDITOR
		writer.Write(AssetDatabase.GetAssetPath(value));
#else
		string path;
		if (!_paths.TryGetValue(value, out path))
			path = "RUNTIME_SERIALIZER_UNKNOWN_RESOURCE";
		writer.Write(path);
#endif
	}

	public Texture2D Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		var path = reader.ReadString();
		if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
		return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
		var resource = Resources.Load<Texture2D>(path.Substring("Assets/Resources/".Length).Split('.').First());
		_paths[resource] = path;
		return resource;
#endif
	}
}