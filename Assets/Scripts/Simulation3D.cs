using UnityEngine;
using Unity.Mathematics;
using UnityEditor;
using System;

public struct Particle {
    public float pressure;
    public float density;
    public Vector3 currentForce;
    public Vector3 velocity;
    public Vector3 position;
}

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
    public ComputeShader sphCompute;
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

    //Raymarching settings
    public ComputeBuffer _particlesBuffer;
    //public ComputeBuffer _argsBuffer;
    public Particle[] particles;
    private int synchronizeKernel;

    // Kernels
    private readonly int externalForcesKernel = 0;
    private readonly int spatialHashKernel = 1;
    private readonly int densityKernel = 2;
    private readonly int pressureKernel = 3;
    private readonly int viscosityKernel = 4;
    private readonly int updatePositionsKernel = 5;

    private GPUSort gpuSort;
    public Spawner.SpawnData spawnData;

    // Status
    private bool isPaused;
    private bool pauseNextFrame;

    #endregion

    #region Simulation Initialization

    private void Start() {
        Debug.Log("Controls: Space = Play/Pause, R = Reset");
        //particles = spawner.GenerateParticles();
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
        particles = spawnData.particles;
        InitializeBuffers(spawnData.points.Length);

        _particlesBuffer = new ComputeBuffer(spawnData.points.Length, 44);
        _particlesBuffer.SetData(particles);

        SetupRaymarchingShaderBuffers();
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
        /*
            Vale Ojito a como te has quedado con respecto al Raymarching del pavo ese
            ahora tenemos que encontrar la manera de meter el particles, de alguna forma
            por lo que estoy viendo tengo que modificar o crear una nueva funcion
            para hacer aparecer las particulas en la caja y asi poder meter
            el particles en el _particlesBuffer. Después de eso tengo que seguir investigando
            el como meter _particlesBuffer. :D Venga Jaime que sé que tu puedes campeón
            Esto cuesta, pero repetir cuesta el doble y yo confío en ti <3
        */
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

        ComputeHelper.Dispatch(computeShader, positionBuffer.count, kernelIndex: synchronizeKernel);
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

        sphCompute.SetVector("boxSize", transform.localScale);
        sphCompute.SetFloat("timestep", deltaTime);
        sphCompute.SetVector("spherePos", collisionObject.transform.position);
        sphCompute.SetFloat("sphereRadius", collisionObject.transform.localScale.x/2);
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

    private void SetupRaymarchingShaderBuffers()
    {
        synchronizeKernel = computeShader.FindKernel("SynchronizeParticles");

        sphCompute.SetInt("particleLength", spawnData.points.Length);
        sphCompute.SetFloat("particleMass", 1.0f);
        sphCompute.SetFloat("viscosity", viscosityStrength);
        sphCompute.SetFloat("gasConstant", 2.0f);
        sphCompute.SetFloat("restDensity", 1.0f);
        sphCompute.SetFloat("boundDamping", collisionDamping);
        sphCompute.SetFloat("pi", Mathf.PI);
        sphCompute.SetVector("boxSize", transform.localScale);

        sphCompute.SetFloat("radius", smoothingRadius);
        sphCompute.SetFloat("radius2", smoothingRadius * smoothingRadius);
        sphCompute.SetFloat("radius3", smoothingRadius * smoothingRadius * smoothingRadius);
        sphCompute.SetFloat("radius4", smoothingRadius * smoothingRadius * smoothingRadius * smoothingRadius);
        sphCompute.SetFloat("radius5", smoothingRadius * smoothingRadius * smoothingRadius * smoothingRadius * smoothingRadius);
        
        sphCompute.SetBuffer(synchronizeKernel, "_particles", _particlesBuffer);
        /*sphCompute.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        sphCompute.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);*/
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