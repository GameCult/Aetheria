/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Gravity : MonoBehaviour
{
    public float GlobalGravityMultiplier;
    public GravityForceMode ForceMode = GravityForceMode.Gradient;
    public float GradientStepSize = .01f;
    public float NormalMultiplier = 100f;
    public List<GravityObject> GravityObjects = new List<GravityObject>();

    private static Gravity _instance;

    public static Gravity Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = (Gravity)FindObjectOfType(typeof(Gravity));
            return _instance;
        }
    }

    public static float GetHeight(Vector2 position)
    {
        float result = 0;
        foreach (var go in Instance.GravityObjects)
        {
            if (((Vector2) go.transform.InverseTransformPoint(position.Flatland())).sqrMagnitude < .25f)
                result = result - GetHeight(go.GravityMaterial, go.Shader,
                             (Vector2) go.transform.InverseTransformPoint(position.Flatland()) + Vector2.one * .5f);
        }

        return result;
    }

    public static Vector2 GetForce(Vector2 position)
    {
        if (Instance.ForceMode == GravityForceMode.Sign)
        {
            var hits = Instance.GravityObjects.Where(go => (go.transform.position.Flatland() - position).sqrMagnitude < go.transform.lossyScale.x)
//            var hits = Physics.RaycastAll(new Ray(new Vector3(position.x, 10, position.y), Vector3.down), 20, LayerMask.GetMask("Displacement"))
                .Where(h => !h.CompareTag("IgnoreGravity"));
        
            Vector2 force = Vector2.zero;
            foreach (var hit in hits)
            {
                force += (new Vector2(hit.transform.position.x, hit.transform.position.z) - position) *
                         GetHeight(hit.GravityMaterial, hit.Shader, hit.transform.InverseTransformPoint(position.Flatland()).Flatland());
            }
            return force * Instance.GlobalGravityMultiplier;
        }

        var normal = GetNormal(position);
        var f = new Vector2(normal.x, normal.z);
        return f * Instance.GlobalGravityMultiplier * f.sqrMagnitude;// * Mathf.Abs(GetHeight(position));
    }

    private static float GetHeight(Material mat, string shader, Vector2 textureCoord)
    {
//        var mat = hit.transform.GetComponent<MeshRenderer>().material;

        switch (shader)
        {
            case "Brushes/Cubic Pulse Brush":
                return mat.GetFloat("_Depth") * CubicPulse(0, .5f, (textureCoord - Vector2.one * .5f).magnitude);
    
            case "Brushes/Power Brush":
                return mat.GetFloat("_Depth") * PowerPulse((textureCoord - Vector2.one * .5f).magnitude * 2, mat.GetFloat("_Power"));
            case "Brushes/Radial Wave Brush":
            {
                float dist = (textureCoord - Vector2.one * .5f).magnitude;
                return mat.GetFloat("_Depth") * PowerPulse(dist * 2, mat.GetFloat("_Power")) *
                       Mathf.Cos(Mathf.Pow(dist*2, mat.GetFloat("_SinePower")) * mat.GetFloat("_Frequency") + mat.GetFloat("_Phase"));
            }
            case "Brushes/Linear Wave Brush":
            {
                float dist = (textureCoord - Vector2.one * .5f).magnitude;
                return mat.GetFloat("_Depth") * PowerPulse(dist * 2, mat.GetFloat("_Power")) *
                       Mathf.Cos((textureCoord.y - .5f) * mat.GetFloat("_Frequency") + mat.GetFloat("_Phase"));
            }
            default:
        
                return 0;
        }
    }

    public static float CubicPulse(float c, float w, float x)
    {
        x = Mathf.Abs(x - c);
        if (x > w) return 0;
        x /= w;
        return 1.0f - x * x * (3.0f - 2.0f * x);
    }

    public static float PowerPulse(float x, float pow)
    {
        x = Mathf.Clamp(x, -1, 1);
        return Mathf.Pow((x + 1) * (1 - x), pow);
    }

    public static Vector3 GetNormal(Vector2 pos, float step, float mul)
    {
        float hL = GetHeight(new Vector2(pos.x - step, pos.y)) * mul;
        float hR = GetHeight(new Vector2(pos.x + step, pos.y)) * mul;
        float hD = GetHeight(new Vector2(pos.x, pos.y - step)) * mul;
        float hU = GetHeight(new Vector2(pos.x, pos.y + step)) * mul;

        // Deduce terrain normal
        Vector3 normal = new Vector3((hL - hR), (hD - hU), 2);
        normal.Normalize();
        return new Vector3(normal.x, normal.z, normal.y);
    }
    
    public static Vector3 GetNormal(Vector2 pos)
    {
        return GetNormal(pos, Instance.GradientStepSize, Instance.NormalMultiplier);
    }
}

public enum GravityForceMode
{
    Sign,
    Gradient
}