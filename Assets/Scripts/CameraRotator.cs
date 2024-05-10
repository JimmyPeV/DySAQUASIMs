using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotator : MonoBehaviour
{
    public Transform target; // Target GameObject around which the camera will rotate
    public float speed; // Speed of rotation

    void Update()
    {
        if (target != null)
        {
            // Rotate around the target at 'speed' degrees per second at a distance defined by the initial offset from the target
            transform.RotateAround(target.position, Vector3.up, speed * Time.deltaTime);
        }
    }
}
