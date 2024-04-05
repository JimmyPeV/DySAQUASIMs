using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Simulation3D : MonoBehaviour
{
    //////////////////////////////////////
    /////////////            /////////////
    /////////////   PREFABs  /////////////
    /////////////            /////////////
    //////////////////////////////////////
    
    [SerializeField] private GameObject particleSphere;

    ////////////////////////////////////////
    /////////////              /////////////
    /////////////   VARIABLES  /////////////
    /////////////              /////////////
    ////////////////////////////////////////
    
    [Header ("Simulation Settings")]
    public Vector3 boundsSize = new Vector3(10f, 10f, 10f);
    [SerializeField] public Gradient velocityGradient;
    //public Vector3 boundsRotation;
    private int seed = 1352;
    public float collisionDamping = 1.0f;
    public float gravity;
    [Range(0.1f, 5.0f)] public float targetDensity;
    public float pressureMultiplier;
    

    [Header("Particle Settings")]
    public int particleQuantity;

    public float particleSpacing = 0.5f;
    [Range(0.1f, 1.0f)] public float particleSize = 0.1f;
    public float smoothingRadius = 1.0f;
    [Range(0.1f, 1.0f)] public float mass = 1.0f;
    

    [Header("Particles Instances - Positions - Velocities - Densities")]
    private GameObject[] particleInstances;
    public Vector3[] positions;
    public Vector3[] velocities;
    public float[] densities;
    private float maxObservedVelocity = 0f;


    ////////////////////////////////////////////////////
    /////////////                          /////////////
    /////////////   SIMULATION START/STEP  /////////////
    /////////////                          /////////////
    ////////////////////////////////////////////////////
    
    private void Start() {
        positions = new Vector3[particleQuantity];
        velocities = new Vector3[particleQuantity];
        densities = new float[particleQuantity];

        particleInstances = new GameObject[particleQuantity];
        //CreateParticlesInCube();
        CreateParticlesAtRandom(seed);
    }

    private void Update() {
        SimulationStep(Time.deltaTime);
    
        for (int i = 0; i < particleQuantity; i++) {
            particleInstances[i].transform.position = positions[i];
            maxObservedVelocity = Mathf.Max(maxObservedVelocity, velocities[i].magnitude);
        }
        
        // Asegúrate de que maxObservedVelocity no sea 0 para evitar dividir por 0
        float safeMaxVelocity = Mathf.Max(maxObservedVelocity, 0.0001f);

        for (int i = 0; i < particleQuantity; i++) {
            float velocityFraction = velocities[i].magnitude / safeMaxVelocity;
            Color color = velocityGradient.Evaluate(velocityFraction);
            particleInstances[i].GetComponent<Renderer>().material.color = color;
        }
    }

    void SimulationStep(float deltaTime)
    {

        //Densities & Collisions
        Parallel.For(0, particleQuantity, i =>
        {
            velocities[i] +=  Vector3.down * gravity * deltaTime;
            densities[i] = CalculateDensity(positions[i]);
        });

        //Aplly pressure forces
        Parallel.For(0, particleQuantity, i =>
        {
            Vector3 pressureForce = CalculatePressureForce(i);
            Vector3 pressureAcceleration = pressureForce / densities[i];
            // F = m * a
            // a = F / m
            velocities[i] = pressureAcceleration * deltaTime;
        });       

        //Update positions
        Parallel.For(0, particleQuantity, i =>
        {
            positions[i] += velocities[i] * deltaTime;
            ResolveCollisions(ref positions[i], ref velocities[i]);
        });
    }

    ///////////////////////////////////////////////////
    /////////////                         /////////////
    /////////////   PRESSURE & DENSITIES  /////////////
    /////////////                         /////////////
    ///////////////////////////////////////////////////
    
    private Vector3 CalculatePressureForce(int particleIndex)
    {
        Vector3 pressureForce = Vector3.zero;
        int threadSafeSeed = seed + particleIndex;
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)threadSafeSeed);

        for (int i = 0; i < particleQuantity; i++)
        {
            if(particleIndex == i)
            {
                continue;
            }
            Vector3 offset = positions[i] - positions[particleIndex];
            float dst = offset.magnitude;
            Vector3 dir = dst == 0 ? GetRandomDir(rng) : offset / dst;

            float slope = SmoothingKernelDerivative(smoothingRadius, dst);
            float density = densities[i];
            float sharedPressure = CalculateSharedPressure(density, densities[particleIndex]);
            pressureForce += sharedPressure * dir * slope * mass / density;
        }
        
        return pressureForce;
    }

    private float CalculateSharedPressure(float densityA, float densityB)
    {
        float pressureA = ConvertDensityToPressure(densityA);
        float pressureB = ConvertDensityToPressure(densityB);

        return (pressureA + pressureB) / 2;
    }

    static float SmoothingKernel(float radius, float dst)
    {
        if(dst >= radius) return 0;

        float volume = (Mathf.PI * Mathf.Pow(radius, 4)) / 6;

        return Mathf.Pow(radius - dst, 2) / volume;
    }

    static float SmoothingKernelDerivative(float radius, float dst)
    {
        if(dst >= radius) return 0;

        float scale = 12 / (Mathf.Pow(radius, 4) * Mathf.PI);

        return (dst - radius) * scale;
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

    void UpdateDensities()
    {
        Parallel.For(0, particleQuantity, i =>
        {
            densities[i] = CalculateDensity(positions[i]);
        });
    }

    float ConvertDensityToPressure(float density) //this is more for gas beahviours but it will do for now
    {
        float densityError = density - targetDensity;
        float pressure = densityError * pressureMultiplier;

        return pressure;
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

    //////////////////////////////////////////
    /////////////                /////////////
    /////////////   AUX SYSTEMs  /////////////
    /////////////                /////////////
    //////////////////////////////////////////
    
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

        GameObject gameManager = GameObject.Find("GameManager");

        for (int i = 0; i < particleQuantity; i++)
        {
            float x = rng.NextFloat(-boundsSize.x / 2, boundsSize.x / 2);
            float y = rng.NextFloat(-boundsSize.y / 2, boundsSize.y / 2);
            float z = rng.NextFloat(-boundsSize.z / 2, boundsSize.z / 2);

            positions[i] = new Vector3(x, y, z);

            particleInstances[i] = Instantiate(particleSphere, positions[i], Quaternion.identity, gameManager.transform);
        }
    }

    private Vector3 GetRandomDir(Unity.Mathematics.Random rng)
    {
        float azimuth = rng.NextFloat(0f, 2f * Mathf.PI);
        float polar = rng.NextFloat(0f, Mathf.PI);

        float x = Mathf.Sin(polar) * Mathf.Cos(azimuth);
        float y = Mathf.Sin(polar) * Mathf.Sin(azimuth);
        float z = Mathf.Cos(polar);

        return new Vector3(x, y, z);
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }
}
