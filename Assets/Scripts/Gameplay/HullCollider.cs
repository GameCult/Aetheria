using UnityEngine;

public class HullCollider : MonoBehaviour
{
    private Renderer[] _renderers;

    public Entity Entity { get; set; }

    public Bounds YmirBounds => CalculateBounds(transform.position);

    public float YmirPlanarRadius => Mathf.Max(YmirBounds.extents.x, YmirBounds.extents.z, 0.001f);

    public void RefreshYmirBoundsSource()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void Awake()
    {
        RefreshYmirBoundsSource();
    }

    private void OnEnable()
    {
        if (_renderers == null || _renderers.Length == 0)
            RefreshYmirBoundsSource();
    }

    private Bounds CalculateBounds(Vector3 fallbackCenter)
    {
        if (_renderers == null)
            RefreshYmirBoundsSource();

        if (_renderers == null || _renderers.Length == 0)
            return new Bounds(fallbackCenter, Vector3.one);

        var firstRenderer = FirstLiveRenderer();
        if (firstRenderer == null)
            return new Bounds(fallbackCenter, Vector3.one);

        var bounds = firstRenderer.bounds;
        for (var i = 0; i < _renderers.Length; i++)
        {
            var renderer = _renderers[i];
            if (renderer == null || renderer == firstRenderer)
                continue;

            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    private Renderer FirstLiveRenderer()
    {
        for (var i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                return _renderers[i];
        }

        return null;
    }

}
