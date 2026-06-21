using UnityEngine;

public class ShieldTest : MonoBehaviour
{
    public float Acceleration;
    private float _velocity;

    // Update is called once per frame
    void Update()
    {
        var previousX = transform.position.x;
        _velocity += -Mathf.Sign(transform.position.x) * Acceleration * Time.deltaTime;
        transform.position += Vector3.right * (_velocity * Time.deltaTime);

        if (Mathf.Sign(transform.position.x) != Mathf.Sign(previousX))
        {
            _velocity = -_velocity;
        }
    }
}
