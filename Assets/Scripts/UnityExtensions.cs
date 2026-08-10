using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MessagePack;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

using static Unity.Mathematics.math;

public static class UnitySceneExtensions
{
	public static T FindInParents<T>(this GameObject go) where T : Component
	{
		if (go == null) return null;
		var comp = go.GetComponent<T>();

		if (comp != null)
			return comp;

		Transform t = go.transform.parent;
		while (t != null && comp == null)
		{
			comp = t.gameObject.GetComponent<T>();
			t = t.parent;
		}
		return comp;
	}

	public static bool ContainsWorldPoint(this RectTransform rectTransform, Vector3 point)
	{
		Vector2 localMousePosition = rectTransform.InverseTransformPoint(point);
		return rectTransform.rect.Contains(localMousePosition);
	}
	
	public static void BroadcastAll(string fun, System.Object msg) {
		GameObject[] gos = (GameObject[])GameObject.FindObjectsOfType(typeof(GameObject));
		foreach (GameObject go in gos) {
			if (go && go.transform.parent == null) {
				go.gameObject.BroadcastMessageExt<MonoBehaviour>(fun, msg, SendMessageOptions.DontRequireReceiver);
			}
		}
	}
	
	public static void BroadcastMessageExt<T>(this GameObject go, string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
	{
		var monoList = new List<T>();
		go.GetComponentsInChildren(true, monoList);
		//monoList.Add();
		foreach (var component in monoList)
		{
			// try
			// {
				Type type = component.GetType();

				MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance |
				                                               BindingFlags.NonPublic |
				                                               BindingFlags.Public |
				                                               BindingFlags.Static);

				if(method!=null)
					method.Invoke(component, new[] { value });
			// }
			// catch (Exception e)
			// {
			// 	//Re-create the Error thrown by the original SendMessage function
			// 	if (options == SendMessageOptions.RequireReceiver)
			// 		Debug.LogError("SendMessage " + methodName + " has no receiver!");
			//
			// 	Debug.LogError(e.Message);
			// }
		}
	}
	
	public static Vector3 Flatland(this Vector2 v, float y = 0) => new Vector3(v.x,y,v.y);
	public static Vector2 Flatland(this Vector3 v) => new Vector2(v.x,v.z);
	
	// public static Rect ScreenSpaceRect(this RectTransform transform)
	// {
	// 	var size= Vector2.Scale(transform.rect.size, transform.lossyScale);
	// 	var x= transform.position.x + transform.anchoredPosition.x;
	// 	var y= Screen.height - transform.position.y - transform.anchoredPosition.y;
 //
	// 	return new Rect(x, y, size.x, size.y);
	// }
	
	private static Vector3[] _worldCorners = new Vector3[4];

	public static Bounds GetBounds(this RectTransform transform, float expand = 0)
	{
		transform.GetWorldCorners(_worldCorners);
		var bounds = new Bounds(_worldCorners[0], Vector3.zero);
		for(int i = 1; i < 4; ++i)
		{
			bounds.Encapsulate(_worldCorners[i]);
		}
		bounds.size += new Vector3(expand, expand, 0);
		return bounds;
	}
	public static Rect ScreenSpaceRect(this RectTransform transform, float expand = 0)
	{
		var bounds = GetBounds(transform, expand);
		return new Rect(bounds.min, bounds.size);
	}
}
