using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class Simulation3D : MonoBehaviour
{
    #region Variables
    ///////////////////////////////////////////////
    /////////////                     /////////////
    /////////////   Particle drawing  /////////////
    /////////////                     /////////////
    ///////////////////////////////////////////////
    
    private static int segments = 100;
    private Material particleMat;

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
    private List<GameObject> particleList;
    private Vector3[] positions;
    private Vector3[] predictedPosition;
    private Vector3[] velocities;
    private float[] densities;

    /////////////////////////////////////////////////////
    /////////////                           /////////////
    /////////////   Spatial grid structure  /////////////
    /////////////                           /////////////
    /////////////////////////////////////////////////////
    private Entry[] spatialLookup;
    private int[] startIndices;
    private Vector3[] cellOffsets;

    #endregion

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

        GenerateParticles(particleQuantity);
        //CreateParticlesInCube();
        //CreateParticlesAtRandom(seed);
    }

    private void Update() {
        SimulationStep(Time.deltaTime);
    }

    void SimulationStep(float deltaTime)
    {
        // Apply gravity and predict next position
        Parallel.For(0, particleQuantity, i =>
        {
            velocities[i] +=  Vector3.down * gravity * deltaTime;
            predictedPosition[i] = positions[i] + velocities[i] * 1 / 120; //Change 120 for the smoothness
        });

        //Update spatial lookup with predicted position
        UpdateSpatialLookup(predictedPosition, smoothingRadius);

        //Calculate densities
        Parallel.For(0, particleQuantity, i =>
        {
            densities[i] = CalculateDensity(predictedPosition[i]);
        });

        //Aplly pressure forces
        Parallel.For(0, particleQuantity, i =>
        {
            Vector3 pressureForce = CalculatePressureForce(i);
            Vector3 pressureAcceleration = pressureForce / densities[i];
            // F = m * a
            // a = F / m
            velocities[i] += pressureAcceleration * deltaTime;
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

    #region Spatial 3D Structure
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

    public void UpdateSpatialLookup(Vector3[] points, float radius){
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
            uint key = spatialLookup[i].hashKey;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i - 1].hashKey;
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
                if(spatialLookup[i].hashKey != key) break;

                int particleIndex = spatialLookup[i].index;

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

    #endregion

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
    
    public class Entry : IComparable
    {
        public int index;                   // the index in the points array
        public uint hashKey;                // the hashkey of the given points

        public Entry(int _index, uint _hashKey)
        {
            this.index = _index;
            this.hashKey = _hashKey;
        }

        public int CompareTo(object obj)
        {
            Entry entry = obj as Entry;
            return this.hashKey.CompareTo(entry.hashKey);
        }
    }

    float3[] GenerateParticles(int numParticles){
        float3 s = new float3(10, 10, 10);

        int numX = Mathf.CeilToInt(Mathf.Pow(numParticles * (s.x / s.y / s.z), 1.0f / 3.0f));
        int numY = Mathf.CeilToInt(Mathf.Pow(numParticles * (s.y / s.x / s.z), 1.0f / 3.0f));
        int numZ = Mathf.CeilToInt(numParticles / (float)(numX * numY));
        int i = 0;

        float3[] particles = new float3[particleQuantity];

        for (int z = 0; z < numZ; z++)
        {
            for (int y = 0; y < numY; y++)
            {
                for (int x = 0; x < numX; x++)
                {
                    if(i >= particleQuantity) break;

                    float tx = numX <= 1 ? 0.5f : x / (numX - 1f);
                    float ty = numY <= 1 ? 0.5f : y / (numY - 1f);
                    float tz = numZ <= 1 ? 0.5f : z / (numZ - 1f);

                    particles[i] = new float3((tx - 0.5f) * s.x, (ty - 0.5f) * s.y, (tz - 0.5f) * s.z);

                    i++;
                }
            }
        }
        return particles;
    }

    void UniformCreateParticles()
    {
        // Create particle arrays
        positions = new Vector3[particleQuantity];
        predictedPosition = new Vector3[particleQuantity];
        velocities = new Vector3[particleQuantity];

        // Calculate how many particles to place per dimension
        int particlesPerSide = Mathf.CeilToInt(Mathf.Pow(particleQuantity, 1f / 3f)); // Cube root to distribute particles evenly
        float spacing = particleSize * 2;

        for (int i = 0; i < particleQuantity; i++)
        {
            int z = i / (particlesPerSide * particlesPerSide); // Calculate the depth layer index
            int indexXY = i % (particlesPerSide * particlesPerSide); // Calculate index for the x-y plane
            int y = indexXY / particlesPerSide; // Calculate row index within the layer
            int x = indexXY % particlesPerSide; // Calculate column index within the row

            // Compute the positions offset from the center
            float posX = (x - particlesPerSide / 2f + 0.5f) * spacing;
            float posY = (y - particlesPerSide / 2f + 0.5f) * spacing;
            float posZ = (z - particlesPerSide / 2f + 0.5f) * spacing;

            // Assign positions and predicted positions
            positions[i] = new Vector3(posX, posY, posZ);
            predictedPosition[i] = new Vector3(posX, posY, posZ);
        }
    }

    void RandomCreateParticles(int seed)
    {
        System.Random rng = new(seed);
        positions = new Vector3[particleQuantity];
        predictedPosition = new Vector3[particleQuantity];
        velocities = new Vector3[particleQuantity];

        for (int i = 0; i < positions.Length; i++)
        {
            float x = (float)(rng.NextDouble() - 0.5) * boundsSize.x;
            float y = (float)(rng.NextDouble() - 0.5) * boundsSize.y;
            float z = (float)(rng.NextDouble() - 0.5) * boundsSize.z;

            positions[i] = new Vector3(x, y, z);
            predictedPosition[i] = new Vector3(x, y, z);
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

    void DrawParticles(){
        //Delete previous particles
        if(particleList.Count != 0){
            foreach(GameObject particle in particleList){
                Destroy(particle);
            }
        }
        for (int i = 0; i < particleQuantity; i++)
        {
            DrawCircle(positions[i], particleSize, UnityEngine.Color.blue);
        }
        foreach (Transform trnsfrm in transform)
        {
            particleList.Add(trnsfrm.gameObject);
        }
    }

    //Grab this circle gizmo draw for rendering and optimization pruposes, 
    // since the original sphere made by unity its very heavy on graphical terms.
    void DrawCircle(Vector2 position, float radius, UnityEngine.Color color){
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 3];

        vertices[0] = new Vector3(position.x, position.y, 0);  // center vertex
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 1, t = 0; i < vertices.Length; i++, t += 3)
        {
            float angle = (float)(i - 1) / segments * 360 * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * radius + position.x;
            float y = Mathf.Cos(angle) * radius + position.y;

            vertices[i] = new Vector3(x, y, 0);
            uvs[i] = new Vector2((x / radius + 1) * 0.5f, (y / radius + 1) * 0.5f);

            if (i < vertices.Length - 1)
            {
                triangles[t] = 0;
                triangles[t + 1] = i;
                triangles[t + 2] = i + 1;
            }
            else
            {
                triangles[t] = 0;
                triangles[t + 1] = i;
                triangles[t + 2] = 1;
            }
        }

        Mesh mesh = new Mesh();

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Assign material instance to each particle
        Material newMat = new Material(particleMat);
        newMat.SetColor("_Color", color);

        // Create a new particle object
        GameObject newObj = new GameObject("particle");
        newObj.AddComponent<MeshFilter>();
        newObj.AddComponent<MeshRenderer>();
        newObj.GetComponent<MeshFilter>().mesh = mesh;
        newObj.GetComponent<MeshRenderer>().material = newMat;

        /*// Add script and tag for debugging
        newObj.AddComponent<ParticleAtt>();
        newObj.layer = LayerMask.NameToLayer("Particles");
        newObj.AddComponent<MeshCollider>();*/

        // Add it into the list
        newObj.transform.SetParent(transform);
    }

    #endregion
}