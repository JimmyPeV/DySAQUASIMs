using UnityEngine;
using Unity.Mathematics;
using UnityEditor;
using System.Collections.Generic;

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
    public List<Vector3Int> voxelModel;
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
    ComputeBuffer voxelBuffer;
    
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
        Vector3[] voxelPositions = LoadVoxelData();
        voxelBuffer = new ComputeBuffer(voxelPositions.Length, sizeof(float) *3);
        voxelBuffer.SetData(voxelPositions);

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
    
    
    #endregion

