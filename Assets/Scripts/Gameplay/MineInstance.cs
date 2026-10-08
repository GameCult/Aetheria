using CultMath;
using CultMath.UnityBridge;
using UnityEngine;
using static CultMath.math;
using Random = UnityEngine.Random;

// How one laid mine looks. Zone owns the mine's every fact (position, arming, trigger, end); this reads them and
// draws, and writes nothing back. The pose is the latest sim body, spun for show; the pulse runs in real time
// (rulings unity-presents-only-r3 and sim-speed-presentation).
public class MineInstance : MonoBehaviour
{
    public MeshRenderer MeshRenderer;
    public int EmissionSubmesh = 2;
    public AnimationCurve EmissionCurve;
    public float ActiveCycleDuration = 1f;
    public float ArmedCycleDuration = .25f;
    public float ActiveEmission = 100f;
    public float ArmedEmission = 1000f;
    public float RotationSpeed = 1f;
    public float GridOffset;
    // The blast's particles; the effect prefab destroys itself when its particle system stops.
    public GameObject HitEffect;
    public float HitEffectScale = 10f;

    private Zone _zone;
    private Mine _mine;
    private Material _material;
    private float _timeOffset;
    private float _pulse;
    private float _emission;

    public void Bind(Zone zone, Mine mine)
    {
        _zone = zone;
        _mine = mine;
        _material = MeshRenderer.materials[EmissionSubmesh];
        _timeOffset = Random.value * 100;
        Place();
    }

    private void Update()
    {
        Place();
        transform.localRotation = Quaternion.Euler(
            sin(Time.time - _timeOffset * RotationSpeed) * 90, 0, cos(Time.time - _timeOffset * RotationSpeed) * 90);

        // None before the mine arms, then slow while it waits and fast once its fuse is lit.
        var armed = _mine.Armed(_zone.Time);
        var counting = _mine.TriggeredAt != null;
        var target = !armed ? 0f : counting ? ArmedEmission : ActiveEmission;
        _emission = lerp(_emission, target, Time.deltaTime * 10);
        _pulse = (_pulse + Time.deltaTime / (counting ? ArmedCycleDuration : ActiveCycleDuration)) % 1;
        _material.SetFloat("_Emission", _emission * EmissionCurve.Evaluate(_pulse));
    }

    private void Place()
    {
        var p = _mine.Body.Position;
        transform.position = new Vector3(p.x, _zone.GetHeight(p) + GridOffset, p.y);
    }

    // The mine left Zone.Mines, which only its blast does: show it where it was drawn last.
    public void Blast()
    {
        var effect = Instantiate(HitEffect, transform.position, Quaternion.identity);
        effect.transform.localScale = Vector3.one * HitEffectScale;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_material) Destroy(_material);
    }
}
