using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class FluidRayTracking : MonoBehaviour
{
    public ComputeShader computeShader;
    public ComputeBuffer _particleBuffer;
    [SerializeField] private Camera camera;

    List<ComputeBuffer> buffers = new List<ComputeBuffer>();

    public Simulation3D simulation;

    RenderTexture target;

    [Header("Parameters")]
    public float viewRadius;
    public float blendStrength;
    public Color liquidColor;

    public Color ambientLight;
    public Light lightSource;

    private bool render = false;
    
    void InitializeRenderTexture(){
        camera.depthTextureMode = DepthTextureMode.Depth;

        target = new RenderTexture (camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        target.enableRandomWrite = true;
        target.Create();
    }

    private void SpawnParticlesInBox(){
        _particleBuffer = new ComputeBuffer(1, 44);
        //_particleBuffer.SetData(new Particle[] { new Particle {Position = new Vector3(0,0,0)}});
    }

    public void Begin(){
        InitializeRenderTexture();
        computeShader.SetBuffer(0, "particles", simulation._particlesBuffer);
    }
}
