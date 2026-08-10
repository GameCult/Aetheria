/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
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

    private Rigidbody _body;

	// Use this for initialization
	void Start () {
	    _body = GetComponent<Rigidbody>();
	}
	
	// Update is called once per frame
	void LateUpdate () {
        var pos = new Vector2(transform.position.x,transform.position.z);

        if(SnapPosition)
		    transform.position = new Vector3(pos.x, Gravity.GetHeight(pos) + Offset,pos.y);
	    if (SnapRotation)
	    {
            var normal = Gravity.GetNormal(pos, GravityGradientStep, NormalMultiplier);
	        var forward = Vector3.Cross(transform.right, normal);
            transform.rotation = Quaternion.LookRotation(forward, normal);
	    }

	    if (ApplyForces)
	    {
	        //var currentVelocity = _body.velocity;
	        var force = Gravity.GetForce(pos) * ForceScale;
            _body.AddForce(force.x,0,force.y,ForceMode.Acceleration);
	    }
    }
}
