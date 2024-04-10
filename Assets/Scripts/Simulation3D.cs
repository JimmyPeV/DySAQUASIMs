using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

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
    private Vector3[] positions;
    private Vector3[] velocities;
    private float[] densities;

    ////////////////////////////////////////
    /////////////              /////////////
    /////////////   CELL KEYS  /////////////
    /////////////              /////////////
    ////////////////////////////////////////
    private Entry[] spatialLookup;
    private int[] startIndices;
    private Vector3[] cellOffsets;


    #region Simulation Start-Step

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
        CreateParticlesInCube();
        //CreateParticlesAtRandom(seed);
    }

    private void Update() {
        SimulationStep(Time.deltaTime);
    
        for (int i = 0; i < particleQuantity; i++) {
            particleInstances[i].transform.position = positions[i];
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

    #endregion
    
    #region Spatial 3D grid structure explanation
        /*
        Spatial 3D grid structure

        Spatial key lookup logics to speedup the process finding particles have impact to the sample point
        Consider there are 5 particles in the scene
        [0, 1, 2, 3, 4]
        For point 0
        Point index: 0
        Cell coord is: (2, 1, 5) based on point (x,y,z) and smoothingRadius
        To obtain the cell hash we multiply each coord with a prime number to evade repetitive cell keys, and then we sum them
        2* 1301 +
        1* 5449 +
        5* 14983;
        Cell hash is: 82966
        Cell key is obtained with the "Hash number" % "Number of points in scene"
        82966 % 5
        Cell key is: 2

        Same approach to all points
        So that in the spatialLookup array, it becomes
        [2, 2, 8, 1, 3]
        We can tell that having the same hash key values meaning those points are in the same cell grid
        Then we can short the spatialLookup array to have points same hash key group together
        pointsIndex: [0, 1, 2, 3, 4]
        pointsHashKey: [1, 2, 2, 3, 8] (sorted)

        The based on the pointsHashKey array we have, we can then generate a start index array for each hash key
        Start index: [0, 1, 1, 3, 4]
        Start index array provides a way to look up all points in the same grid
        For example, we calculate the hashkey for current grid is 0
        The startIndices[0] = 2, meaning that all points with hashKey 0 start from lookup array index 1
        So we have points [1, 2] are all in the same grid with hashKey 2

        Then for each sample point, we can first lcate which grid it is inside
        Then we calculate the total 3x3x3 cells around and including the center cell
        For each cell calculate the haskey
        The use startIndeces array to finde all points that inside of the cell
        For each point that reside inside of the 3x3x3 grid
        We then check if it is also inside of the smoothCircle of the sample point
        If inside, we then will update the properties of each point
        */
    #endregion

    void InitializeSpatialStructure(){
        spatialLookup = new Entry[particleQuantity];
        startIndices = new int[particleQuantity];

        cellOffsets = new Vector3[]{
            //Layer 0 (z = 0)
            new Vector3(0, 0, 0), //Center cell
            new Vector3(1, 0, 0), //Right cell
            new Vector3(-1, 0, 0), //Left cell

            new Vector3(0, 1, 0), //Top Center cell
            new Vector3(1, 1, 0), //Top Right cell
            new Vector3(-1, 1, 0), //Top Left cell

            new Vector3(0, -1, 0), //Bottom Center cell
            new Vector3(1, -1, 0), //Bottom Right cell
            new Vector3(-1, -1, 0), //Bottom Left cell

            //Layer 1 (z = 1)
            new Vector3(0, 0, 1), //Center cell
            new Vector3(1, 0, 1), //Right cell
            new Vector3(-1, 0, 1), //Left cell

            new Vector3(0, 1, 1), //Top Center cell
            new Vector3(1, 1, 1), //Top Right cell
            new Vector3(-1, 1, 1), //Top Left cell

            new Vector3(0, -1, 1), //Bottom Center cell
            new Vector3(1, -1, 1), //Bottom Right cell
            new Vector3(-1, -1, 1), //Bottom Left cell

            //Layer -1 (z = -1)
            new Vector3(0, 0, -1), //Center cell
            new Vector3(1, 0, -1), //Right cell
            new Vector3(-1, 0, -1), //Left cell

            new Vector3(0, 1, -1), //Top Center cell
            new Vector3(1, 1, -1), //Top Right cell
            new Vector3(-1, 1, -1), //Top Left cell

            new Vector3(0, -1, -1), //Bottom Center cell
            new Vector3(1, -1, -1), //Bottom Right cell
            new Vector3(-1, -1, -1), //Bottom Left cell
        };
    }
    public void UpdateSpatial(Vector3[] points, float radius){
        this.positions = points;
        this.smoothingRadius = radius;

        //Create spatial lookup (unordened)
        Parallel.For(0, points.Length, i=>
        {
            (int cellX, int cellY, int cellZ) = PositionToCellCoord(points[i], radius);
            uint cellKey = GetKeyFromCellHash(GetCellHash(cellX, cellY, cellZ));
            spatialLookup[i] = new Entry(i, cellKey);
            startIndices[i] = int.MaxValue; //Infinite
        });

        //Sort by cell key
        Array.Sort(spatialLookup);

        //Calculate start indices of each unique cell key in the spatial lookup
        Parallel.For(0, points.Length, i=>{
            uint key = spatialLookup[i].cellKey;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i - 1].cellKey;
            if(key != keyPrev){
                startIndices[key] = i;
            }
        });
    }

    //Convert a cell coordinate into a single number.
    //Hash collisions (diferent cells -> same value) are unavoidable, but we want to try at least
    //to minimize collisions for nearby cells. There must be a better way...

    private uint GetCellHash(int cellX, int cellY, int cellZ)
    {
        uint a = (uint)cellX * 1301;
        uint b = (uint)cellY * 5449;
        uint c = (uint)cellZ * 14983;

        return a + b + c;
    }

    //Warp the hash value around the length of the array (so it can be used as an index)
    private uint GetKeyFromCellHash(uint hash)
    {
        return hash % (uint)spatialLookup.Length;
    }

    //Convert a position to the coordinate of the cell it is within
    public (int x, int y, int z) PositionToCellCoord(Vector3 point, float radius)
    {
        int cellX = (int)(point.x / radius);
        int cellY = (int)(point.y / radius);
        int cellZ = (int)(point.z / radius);

        return (cellX, cellY, cellZ);
    }

    public List<int> ForeachPointWithinRadius(Vector3 samplePoint){
        List<int> validPointsInsideRadius = new List<int>();

        //Find which cell the sample point is in (centre of 3x3x3)
        (int centreX, int centreY, int centreZ) = PositionToCellCoord(samplePoint, smoothingRadius);
        float powRadius = smoothingRadius * smoothingRadius;

        foreach (Vector3 offset in cellOffsets)
        {
            int offsetX = (int)offset.x;
            int offsetY = (int)offset.y;
            int offsetZ = (int)offset.z;

            //Get key of current cell, then loop over all points that share that key
            uint key = GetKeyFromCellHash(GetCellHash(centreX + offsetX, centreY + offsetY, centreZ + offsetZ));
            int cellStartIndex = startIndices[key];

            for (int i = cellStartIndex; i < spatialLookup.Length; i++)
            {
                //Exit loop if we're no longer looking at the correct cell
                if(spatialLookup[i].cellKey != key) break;

                int particleIndex = spatialLookup[i].particleIndex;

                float sqrDst = (particleInstances[particleIndex].transform.position - samplePoint).sqrMagnitude;

                //Test if the point is inside the radius
                if(sqrDst <= powRadius)
                {
                    validPointsInsideRadius.Add(particleIndex);
                }
            }
        }
        return validPointsInsideRadius;
    }

    #region Pressure & Densities
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
        
        //List<int> pointsIdx = ForeachPointWithinRadius(thisParticle);
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

    #endregion

    #region Auxiliar Systems

    //////////////////////////////////////////
    /////////////                /////////////
    /////////////   AUX SYSTEMs  /////////////
    /////////////                /////////////
    //////////////////////////////////////////
    
    struct Entry : IComparable<Entry>
    {
        public int particleIndex;
        public uint cellKey;

        public Entry(int particleIndex, uint cellKey)
        {
            this.particleIndex = particleIndex;
            this.cellKey = cellKey;
        }

        public int CompareTo(Entry other)
        {
            return cellKey.CompareTo(other.cellKey);
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
        Gizmos.color = UnityEngine.Color.yellow;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }

    #endregion
}