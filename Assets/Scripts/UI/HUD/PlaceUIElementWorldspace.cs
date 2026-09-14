using UnityEngine;
using CultMath;
using static CultMath.math;
using static Noise1D;

/// <summary>
/// Place an UI element to a world position
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PlaceUIElementWorldspace : MonoBehaviour
{
    public float NoiseAmplitude;
    public float NoiseFrequency;
    public Canvas Canvas;
    public float BorderPixels = 10;
    public bool PointAtTarget;
    public Vector2 ScreenSpaceOffset;
    public float EdgeMargin = .1f;

    public Vector3 Target;

    private RectTransform _rectTransform;
    private Transform _cameraTransform;
    private float _noiseOffset;
    private RectTransform _canvasRectTransform;
    private Camera _mainCamera;
    private Quaternion _startRotation;

    void Start()
    {
        // Get the rect transform
        _rectTransform = GetComponent<RectTransform>();
        _cameraTransform = Camera.main.transform;
        _canvasRectTransform = Canvas.GetComponent<RectTransform>();
        _mainCamera = Camera.main;
        _startRotation = transform.rotation;
    }

    private void OnEnable()
    {
        _noiseOffset = UnityEngine.Random.Range(-100f, 100f);
    }

    /// <summary>
    /// Move the UI element to the world position
    /// </summary>
    void LateUpdate()
    {
        _noiseOffset += Time.deltaTime * NoiseFrequency;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_mainCamera, Target);
        var toTarget = (Target - _mainCamera.transform.position).normalized;
        
        screenPoint *= Mathf.Sign(Vector3.Dot(_cameraTransform.forward, Target - _cameraTransform.position));
        screenPoint += new Vector2(noise(_noiseOffset), noise(10 + _noiseOffset)) * NoiseAmplitude;
        var clampedPoint = new Vector2(
            Mathf.Clamp(screenPoint.x, BorderPixels, Screen.width - BorderPixels),
            Mathf.Clamp(screenPoint.y, BorderPixels, Screen.height - BorderPixels));
        var toEdge = _mainCamera.ScreenPointToRay(clampedPoint);
        var edgeLerp = smoothstep(1 - EdgeMargin, 1, dot(toEdge.direction, toTarget));
        if (PointAtTarget)
        {
            transform.rotation = Quaternion.Slerp(
                Quaternion.LookRotation(Vector3.forward, (screenPoint - clampedPoint).normalized), 
                _startRotation, edgeLerp);
        }
        clampedPoint += ScreenSpaceOffset * edgeLerp;
        _rectTransform.anchoredPosition = clampedPoint / Canvas.scaleFactor - _canvasRectTransform.sizeDelta / 2f;
    }
}