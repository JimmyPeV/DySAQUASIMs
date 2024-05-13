using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotator : MonoBehaviour
{
    public Transform target; // Target object to rotate around
    public float speed = 50.0f; // Rotation speed in degrees per second
    public float distance = 5.0f; // Distance from the target

    private Vector3 _initialOffset;
    private float _angle;

    void Start()
    {
        if (target != null)
        {
            // Calculate initial offset from target
            _initialOffset = transform.position - target.position;
            // Set initial distance based on the current distance
            distance = _initialOffset.magnitude;
        }
    }

    void Update()
    {
        if (target != null)
        {
            // Increment the angle based on speed and time
            _angle += speed * Time.deltaTime;
            // Calculate new position using trigonometric functions
            float x = Mathf.Sin(_angle * Mathf.Deg2Rad) * distance;
            float z = Mathf.Cos(_angle * Mathf.Deg2Rad) * distance;
            // Update camera position
            transform.position = target.position + new Vector3(x, _initialOffset.y, z);
            // Always look at the target
            transform.LookAt(target);
        }
    }
}
