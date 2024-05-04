using UnityEngine;
using Unity.Mathematics;
using UnityEditor;
using System;

public class Simulation3D : MonoBehaviour
{
    #region Variables

    public event System.Action simulationStepCompleted;
    public Transform collisionObject;
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsByFrame;


    [Header("References")]
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Spawner spawner;
    [SerializeField] private Display display;
    public Transform floorDisplay;

    [Header("Particle Simulation Settings")]
    public float gravity = 0.0f;
    [Range(0.0f, 1.0f)] public float collisionDamping = 0.8f;
    public float smoothingRadius = 1.0f;
    public float targetDensity;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityStrength;

    // Buffers
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
    public ComputeBuffer densityBuffer { get; private set; }
    public ComputeBuffer predictedPositionsBuffer;
    private ComputeBuffer spatialIndexes;
    private ComputeBuffer spatialOffsets;

    // Kernels
    private readonly int externalForcesKernel = 0;
    private readonly int spatialHashKernel = 1;
    private readonly int densityKernel = 2;
    private readonly int pressureKernel = 3;
    private readonly int viscosityKernel = 4;
    private readonly int updatePositionsKernel = 5;

    private GPUSort gpuSort;
    private Spawner.SpawnData spawnData;

    // Status
    private bool isPaused;
    private bool pauseNextFrame;

    #endregion

    #region Simulation Initialization

    private void Start() {
        Debug.Log("Controls: Space = Play/Pause, R = Reset");
        InitializeComputeBuffers();
    }

    #endregion

    #region Simulation Update

    void FixedUpdate() {
        if (fixedTimeStep) {
            RunSimulationFrameByFrame(Time.fixedDeltaTime);
        }
    }

    void Update() {
        RunVariableTimestepSimulation();
        ManagePauseState();
        AdjustFloorDisplayScale();
        HandleInput();
    }

    private void RunVariableTimestepSimulation() {
        if (!fixedTimeStep && Time.frameCount > 10) {
            RunSimulationFrameByFrame(Time.deltaTime);
        }
    }

    private void ManagePauseState() {
        if (pauseNextFrame) {
            isPaused = true;
            pauseNextFrame = false;
        }
    }

    private void AdjustFloorDisplayScale() {
        float scaleAdjustment = 1 / transform.localScale.y * 0.1f;
        floorDisplay.transform.localScale = new Vector3(1, scaleAdjustment, 1);
    }

    #endregion

    #region Buffer Initialization

    void InitializeComputeBuffers() {
        SetFixedTimeStep();
        RetrieveSpawnData();
        InitializeBuffers(spawnData.points.Length);
        SetInitialBufferData(spawnData);
        ConfigureComputeShader();
        InitializeGPUSort();
        display.Init(this);
    }

    private void SetFixedTimeStep() 
    {
        Time.fixedDeltaTime = 1 / 60f;
    }

    private void RetrieveSpawnData() 
    {
        spawnData = spawner.GetSpawnData();
    }

    private void InitializeBuffers(int particleQuantity) 
    {
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleQuantity);
        predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleQuantity);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleQuantity);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleQuantity);
        spatialIndexes = ComputeHelper.CreateStructuredBuffer<uint3>(particleQuantity);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(particleQuantity);
    }

    private void ConfigureComputeShader() {
        SetBuffersInShader();
        computeShader.SetInt("numParticles", positionBuffer.count);
    }

    private void SetBuffersInShader() {
        ComputeHelper.SetBuffer(computeShader, positionBuffer, "Positions", externalForcesKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(computeShader, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(computeShader, spatialIndexes, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(computeShader, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(computeShader, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(computeShader, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);

        computeShader.SetInt("numParticles", positionBuffer.count);
    }

    private void InitializeGPUSort() {
        gpuSort = new GPUSort();
        gpuSort.SetBuffers(spatialIndexes, spatialOffsets);
    }

    #endregion


    #region Simulation Execution

    void RunSimulationFrameByFrame(float frameTime) {
        if (!isPaused) {
            ExecuteSimulationStepsAndUpdate(frameTime);
        }
    }

    private void ExecuteSimulationStepsAndUpdate(float frameTime) {
        float timeStep = frameTime / iterationsByFrame * timeScale;
        UpdateSettings(timeStep);

        for (int i = 0; i < iterationsByFrame; i++) {
            SimulationStep();
            simulationStepCompleted?.Invoke();
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

    #endregion

    #region Utility Methods
    void SetInitialBufferData(Spawner.SpawnData spawnData) {
        float3[] allPoints = new float3[spawnData.points.Length];
        System.Array.Copy(spawnData.points, allPoints, spawnData.points.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnData.velocities);
    }

    void UpdateSettings(float deltaTime) {
        // Update settings in compute shader
        SetShaderParameters(deltaTime);
        computeShader.SetMatrix("localToWorld", transform.localToWorldMatrix);
        computeShader.SetMatrix("worldToLocal", transform.worldToLocalMatrix);
    }

    private void SetShaderParameters(float deltaTime)
    {
        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("gravity", gravity);
        computeShader.SetFloat("collisionDamping", collisionDamping);
        computeShader.SetFloat("smoothingRadius", smoothingRadius);
        computeShader.SetFloat("targetDensity", targetDensity);
        computeShader.SetFloat("pressureMultiplier", pressureMultiplier);
        computeShader.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
        computeShader.SetFloat("viscosityStrength", viscosityStrength);
        computeShader.SetVector("boundsSize", simBoundsSize);
        computeShader.SetVector("centre", simBoundsCentre);
        computeShader.SetVector("objectPosition", collisionObject.transform.position);
        computeShader.SetVector("objectSize", collisionObject.transform.localScale);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            isPaused = false;
            pauseNextFrame = true;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            isPaused = true;
            SetInitialBufferData(spawnData);
        }
    }

    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionsBuffer, velocityBuffer, densityBuffer, spatialIndexes, spatialOffsets);
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