using UnityEngine;

// The shared effect prototypes every mod ship is assembled with: the same assets ShipPrefabAuthoring wires into a
// prefab ship. It lives on one Addressable prefab, so a built player finds it by key without the Editor.
public class ShipModTemplate : MonoBehaviour
{
    // The template prefab's .meta GUID, its Addressables key (Assets/Content is an addressable folder).
    public const string Key = "5d1f3a8c7e2b4f60a9c4b7e1d3f2a860";

    // Prefab roots; the assembler takes the component it needs from each.
    public GameObject Shield;
    public GameObject TractorBeam;
    public GameObject Ping;
    public GameObject DestroyEffect;
    public Material Invisible;
    public Material MapIcon;
}
