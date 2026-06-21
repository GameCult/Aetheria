/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using UnityEngine;

public class SnapToGrid : MonoBehaviour
{
    public bool SnapPosition = false;
    public bool SnapRotation = false;
    public bool ApplyForces = false;

    // public float RotationDamping = .5f;
    // public float PositionDamping = .1f;
    public float ForceScale = 1;
    public float Offset = 0;
    public float NormalMultiplier = 100;
    public float GravityGradientStep = .01f;

    private Vector2 _velocity;
	
	// Update is called once per frame
	void LateUpdate () {
        var pos = new Vector2(transform.position.x,transform.position.z);

	    if (ApplyForces)
	    {
	        var force = Gravity.GetForce(pos) * ForceScale;
	        _velocity += force * Time.deltaTime;
	        pos += _velocity * Time.deltaTime;
	        transform.position = new Vector3(pos.x, transform.position.y, pos.y);
	    }

        if(SnapPosition)
		    transform.position = new Vector3(pos.x, Gravity.GetHeight(pos) + Offset,pos.y);
	    if (SnapRotation)
	    {
            var normal = Gravity.GetNormal(pos, GravityGradientStep, NormalMultiplier);
            var forward = Vector3.Cross(transform.right, normal);
            transform.rotation = Quaternion.LookRotation(forward, normal);
	    }
    }
}
