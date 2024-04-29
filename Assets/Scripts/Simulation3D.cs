using UnityEngine;
using Unity.Mathematics;
using UnityEditor;

public class Simulation3D : MonoBehaviour
{
    #region Variables

    //public event System.Action SimulationStepCompleted;
    public Transform collisionObject;
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsByFrame;

    [Header ("References")]
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Spawner spawner;
    [SerializeField] private Display display;
    public Transform floorDisplay;

    [Header ("Particle Simulation Settings")]
    public float gravity = 0.0f;
    [Range(0.0f, 1.0f)] public float collisionDamping = 0.8f;
    public float smoothingRadius = 1.0f;
    public float targetDensity;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityStrength;

    //buffers
    public ComputeBuffer positionBuffer {get; private set; }
    public ComputeBuffer velocityBuffer {get; private set; }
    public ComputeBuffer densityBuffer {get; private set;}
    public ComputeBuffer predictedPositionsBuffer;
    ComputeBuffer spatialIndexes;
    ComputeBuffer spatialOffsets;
    
    //kernels
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionsKernel = 5;

    GPUSort gpuSort;

    Spawner.SpawnData spawnData;

    #endregion

    #region Simulation Start-Step

    private void Start() {
        InitializeComputeBuffers();
    }

    void FixedUpdate() {
        if(fixedTimeStep){
            RunSimulationFrameByFrame(Time.fixedDeltaTime);
        }
    }
    void Update() {
        if(!fixedTimeStep){
            RunSimulationFrameByFrame(Time.deltaTime);
        }
        floorDisplay.transform.localScale = new Vector3(1, 1/ transform.localScale.y * 0.1f, 1);
        //SimulationStep();
    }

    void InitializeComputeBuffers(){
        float deltaTime = 1/60f;
        Time.fixedDeltaTime = deltaTime;

        spawnData = spawner.GetSpawnData();

        int particleQuantity = spawnData.points.Length;

        //Start particle buffers
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleQuantity);
        predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleQuantity);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleQuantity);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleQuantity);
        spatialIndexes = ComputeHelper.CreateStructuredBuffer<uint3>(particleQuantity);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(particleQuantity);

        SetInitialBufferData(spawnData);

        // Init compute
        ComputeHelper.SetBuffer(computeShader, positionBuffer, "Positions", externalForcesKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(computeShader, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(computeShader, spatialIndexes, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(computeShader, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(computeShader, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(computeShader, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);

        computeShader.SetInt("numParticles", positionBuffer.count);

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndexes, spatialOffsets);

        // Init display
        display.Init(this);
    }

    void RunSimulationFrameByFrame(float frameTime){
        float timeStep = frameTime / iterationsByFrame * timeScale;

        UpdateSettings(timeStep);

        for (int i = 0; i < iterationsByFrame; i++)
        {
            SimulationStep();
        }
    }

    void SimulationStep()
    {
        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: updatePositionsKernel);
    }

    void SetInitialBufferData(Spawner.SpawnData spawnData)
    {
        float3[] allPoints = new float3[spawnData.points.Length];
        System.Array.Copy(spawnData.points, allPoints, spawnData.points.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnData.velocities);
    }

    void UpdateSettings(float deltaTime){
        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("gravity", gravity);
        computeShader.SetFloat("collisionDamping", collisionDamping);
        computeShader.SetFloat("smoothingRadius", smoothingRadius);
        computeShader.SetFloat("targetDensity", targetDensity);
        computeShader.SetFloat("pressureMultiplier", pressureMultiplier);
        computeShader.SetFloat("nearPressureMultiplier", pressureMultiplier);
        computeShader.SetFloat("viscosityStrength", viscosityStrength);
        computeShader.SetVector("boundsSize", simBoundsSize);
        computeShader.SetVector("centre", simBoundsCentre);
        computeShader.SetVector("objectPosition", collisionObject.transform.position);
        computeShader.SetVector("objectSize", collisionObject.transform.localScale);

        computeShader.SetMatrix("localToWorld", transform.localToWorldMatrix);
        computeShader.SetMatrix("worldToLocal", transform.worldToLocalMatrix);
    }

    void OnDrawGizmos(){
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = m;
    }

    

    /*void UpdateParticleMovement(float deltaTime){

        // Apply gravity and predict next position
        Parallel.For(0, particleQuantity, i =>
        {
            velocities[i] +=  Vector3.down * gravity * deltaTime;
            predictedPosition[i] = positions[i] + velocities[i] * 1 / 120; //Change 120 for the smoothness
        });

        //Update spatial lookup with predicted position
        //UpdateSpatialLookup(predictedPosition, smoothingRadius);

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
    }*/

    #endregion

}
 
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
    
    ///////////////////////////////////////////////////
    /////////////                         /////////////
    /////////////   PRESSURE & DENSITIES  /////////////
    /////////////                         /////////////
    ///////////////////////////////////////////////////
    
    
/*
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

    void UpdateDensities()
    {
        Parallel.For(0, particleQuantity, i =>
        {
            densities[i] = CalculateDensity(positions[i]);
        });
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

    public enum kernels{
        CalculateVelocity = 0,
        CalculateDensities = 1,
        CalculatePressureForce = 2,
        UpdatePositions = 3,
        HashParticles = 4,
        CalculateCellOffsets = 5,
        ClearCellOffsets = 6

    }

    public static void ComputeHelper.Dispatch(ComputeShader computeShader, int numParticles, int kernelIndex){
        uint x, y, z;
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);

        int numGroupsX = Mathf.CeilToInt(numParticles / (float)x);
        int numGroupsY = Mathf.CeilToInt(1 / (float)y);
        int numGroupsZ = Mathf.CeilToInt(1 / (float)z);

        computeShader.ComputeHelper.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
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


    void DrawBoundary()
    {
        if (boundsSize == Vector3.zero || boundsSize == null)
        {
            return;
        }

        if (boundingBoxRenderer == null)
        {
            return;
        }

        boundingBoxRenderer.positionCount = 13;

        float halfX = boundsSize.x / 2;
        float halfY = boundsSize.y / 2;
        float halfZ = boundsSize.z / 2;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(-halfX, -halfY, -halfZ),
            new Vector3(halfX, -halfY, -halfZ),
            new Vector3(halfX, -halfY, halfZ),
            new Vector3(-halfX, -halfY, halfZ),
            new Vector3(-halfX, -halfY, -halfZ),

            new Vector3(-halfX, halfY, -halfZ),  
            new Vector3(halfX, halfY, -halfZ),  
            new Vector3(halfX, halfY, halfZ),   
            new Vector3(-halfX, halfY, halfZ),  
            new Vector3(-halfX, halfY, -halfZ),  

            
            new Vector3(-halfX, -halfY, -halfZ), 
            new Vector3(halfX, -halfY, -halfZ), 
            new Vector3(halfX, halfY, -halfZ)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            boundingBoxRenderer.SetPosition(i, corners[i]);
        }
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
        foreach (Transform trans in transform)
        {
            particleList.Add(trans.gameObject);
        }
    }

    //Grab this circle gizmo draw for rendering and optimization pruposes, 
    // since the original sphere made by unity its very heavy on graphical terms.
    void DrawCircle(Vector3 position, float radius, UnityEngine.Color color){
        int vertexCount = (segments + 1) * (layers + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[segments * layers * 6];

        float phiStep = Mathf.PI / layers;
        float thetaStep = 2 * Mathf.PI / segments;

        // Generar los vértices de la esfera
        int vertexIndex = 0;
        for (int layer = 0; layer <= layers; layer++) {
            float phi = phiStep * layer;

            for (int segment = 0; segment <= segments; segment++) {
                float theta = thetaStep * segment;

                Vector3 vertex = new Vector3(
                    position.x + radius * Mathf.Sin(phi) * Mathf.Cos(theta),
                    position.y + radius * Mathf.Sin(phi) * Mathf.Sin(theta),
                    position.z + radius * Mathf.Cos(phi)
                );

                vertices[vertexIndex] = vertex;
                uvs[vertexIndex] = new Vector2((float)segment / segments, (float)layer / layers);
                vertexIndex++;
            }
        }

        // Generar los triángulos de la esfera
        int triangleIndex = 0;
        for (int layer = 0; layer < layers; layer++) {
            for (int segment = 0; segment < segments; segment++) {
                int current = layer * (segments + 1) + segment;
                int next = current + segments + 1;

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;

                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;
            }
        }

        // Crear la malla y asignarla al objeto
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        Material newMat = new Material(particleMat);
        newMat.SetColor("_Color", color);

        GameObject newObj = new GameObject("sphereParticle");
        newObj.AddComponent<MeshFilter>();
        newObj.AddComponent<MeshRenderer>();
        newObj.GetComponent<MeshFilter>().mesh = mesh;
        newObj.GetComponent<MeshRenderer>().material = newMat;

        newObj.transform.SetParent(transform);
    }
*/
    #endregion

