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
    public int seed = 0;
    public float collisionDamping = 1.0f;
    public float gravity;
    public int particleQuantity;
    public float particleSpacing = 0.5f;

    [Header("Particle Settings")]
    [Range(0.1f, 1.0f)] public float particleSize = 0.1f;
    [Range(0.1f, 1.0f)] public float smoothingRadius = 1.0f;
    public float mass = 1.0f;
    private GameObject[] particleInstances;
    Vector3[] positions;
    Vector3[] velocities;    

    private void Start() {
        positions = new Vector3[particleQuantity];
        velocities = new Vector3[particleQuantity];

        particleInstances = new GameObject[particleQuantity];
        //CreateParticlesInCube();
        CreateParticlesAtRandom(seed);
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

    /*private float CalculateProperty(Vector3 samplePosition)
    {
        float property = 0;

        for (int i = 0; i < particleQuantity; i++)
        {
            float dst = (positions[i] - samplePosition).magnitude;
            float influence = SmoothingKernel(dst, smoothingRadius);
            float density = CalculateDensity(positions[i]);
            property += particleInstances[i] * mass / density * influence;
        }

        return property;
    }*/

    /*private Vector3 CalculatePropertyGradient(Vector3 samplePosition)
    {
        const float stepSize = 0.001f;
        float deltaX = CalculatePropery(samplePosition + Vector3.right * stepSize) - CalculateProperty(samplePosition);
        float deltaY = CalculatePropery(samplePosition + Vector3.up * stepSize) - CalculateProperty(samplePosition);
        float deltaZ = CalculatePropery(samplePosition + Vector3.forward * stepSize) - CalculateProperty(samplePosition);
    
        Vector3 gradient = new Vector3(deltaX, deltaY, deltaZ) / stepSize;
        return gradient;
    }*/

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

    private void CreateParticlesInCube()
    {
        int particlesPerSide = Mathf.CeilToInt(Mathf.Pow(particleQuantity, 1f / 3f));
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

            particleInstances[i] = Instantiate(particleSphere, positions[i], Quaternion.identity);
        }
    }

    private void CreateParticlesAtRandom(int seed)
    {
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
        particleInstances = new GameObject[particleQuantity];

        for (int i = 0; i < particleQuantity; i++)
        {
            float x = rng.NextFloat(-boundsSize.x / 2, boundsSize.x / 2);
            float y = rng.NextFloat(-boundsSize.y / 2, boundsSize.y / 2);
            float z = rng.NextFloat(-boundsSize.z / 2, boundsSize.z / 2);

            positions[i] = new Vector3(x, y, z);

            particleInstances[i] = Instantiate(particleSphere, positions[i], Quaternion.identity);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }
}
