using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Simulation3D : MonoBehaviour
{
    ///            ///
    /// PREFABs    ///
    ///            ///
    [SerializeField] private GameObject particleSphere;
    ///            ///
    /// VARIABLES  ///
    ///            ///
    [Header ("Simulation Settings")]
    public Vector3 boundsSize = new Vector3(10f, 10f, 10f);
    public float collisionDamping = 1.0f;
    public float gravity;
    public int particleQuantity;
    public float particleSpacing = 0.5f;

    [Header("Particle Settings")]
    [Range(0.1f, 1.0f)] public float particleSize = 0.1f;
    [Range(0.1f, 1.0f)] public float smoothingRadius = 1.0f;
    private GameObject[] particleInstances;
    Vector3[] positions;
    Vector3[] velocities;    

    private void Start() {
        positions = new Vector3[particleQuantity];
        velocities = new Vector3[particleQuantity];

        //round up the particle quantity per side
        int particlesPerSide = Mathf.RoundToInt(Mathf.Pow(particleQuantity, 1f / 3f));
        float spacing = particleSize * 2 + particleSpacing;

        for (int i = 0; i < particleQuantity; i++)
        {
            int z = i / (particlesPerSide * particlesPerSide);
            int idx = i % (particlesPerSide * particlesPerSide);
            int y = idx / particlesPerSide;
            int x = idx % particlesPerSide;

            float posX = (x - particlesPerSide / 2f + 0.5f) * spacing;
            float posY = (y - particlesPerSide / 2f + 0.5f) * spacing;
            float posZ = (z - particlesPerSide / 2f + 0.5f) * spacing;

            positions[i] = new Vector3(posX, posY, posZ);
        }

        particleInstances = new GameObject[particleQuantity];
        for (int i = 0; i < particleQuantity; i++) {
            particleInstances[i] = Instantiate(particleSphere, positions[i], Quaternion.identity);
        }
    }

    private void Update() {
        for(int i = 0; i < positions.Length; i++){
            velocities[i] += Vector3.down * gravity * Time.deltaTime;
            positions[i] += velocities[i] * Time.deltaTime;
            ResolveCollisions(ref positions[i], ref velocities[i]);

            if (particleInstances[i] != null) {
                particleInstances[i].transform.position = positions[i];
            }
        }
        
    }
    private float SmoothingKernel(float radius, float dst)
    {
        float volume = Mathf.PI * Mathf.Pow(radius, 8) / 4;
        float value = Mathf.Max(0, radius - dst);

        return Mathf.Pow(value, 3) / volume;
    }

    private float CalculateDensity(Vector3 thisParticle)
    {
        float density = 0;
        const float mass = 1;

        foreach (Vector3 position in positions)
        {
            float dst = (position - thisParticle).magnitude;
            float influence = SmoothingKernel(smoothingRadius, dst);
            density += mass * influence;
        }

        return density;
    }

    private void ResolveCollisions(ref Vector3 position, ref Vector3 velocity)
    {
        Vector3 adjustedBounds = boundsSize - (Vector3.one * particleSize * 2);
        Vector3 halfBounds = adjustedBounds / 2;

        if(Mathf.Abs(position.x) > halfBounds.x){
            position.x = halfBounds.x * Mathf.Sign(position.x);
            velocity.x *= -1 * collisionDamping;
        }
        if(Mathf.Abs(position.y) > halfBounds.y){
            position.y = halfBounds.y * Mathf.Sign(position.y);
            velocity.y *= -1 * collisionDamping;
        }
        if(Mathf.Abs(position.z) > halfBounds.z){
            position.z = halfBounds.z * Mathf.Sign(position.z);
            velocity.z *= -1 * collisionDamping;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }
}
