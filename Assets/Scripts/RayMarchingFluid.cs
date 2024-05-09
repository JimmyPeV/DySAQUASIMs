using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayMarchingFluid : MonoBehaviour
{
    [SerializeField] private ComputeShader rayMarching;
    public Camera camera;

    List<ComputeBuffer> buffersToDispose = new List<ComputeBuffer>();

    public Simulation3D simulation;

    RenderTexture target;

    [Header("Parameters")]
    private int particlesLength;
    public float viewRadius;
    public float blendStrength;
    public Color liquidColor;
    public Color ambientLight;
    public Light lightSource;

    void InitRenderTexture () {
        if (target == null || target.width != camera.pixelWidth || target.height != camera.pixelHeight) {
            if (target != null) {
                target.Release ();
            }
            
            camera.depthTextureMode = DepthTextureMode.Depth;

            target = new RenderTexture (camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
    }

    private bool render = false;

    public ComputeBuffer positionBuffer { get; private set; }

    public void Begin()
    {
        InitRenderTexture();
        //rayMarching.SetBuffer(0,"particles",simulation._particlesBuffer);
        rayMarching.SetInt("numParticles", simulation.spawnData.points.Length);
        rayMarching.SetFloat("particleRadius", viewRadius);
        rayMarching.SetFloat("blendStrength", blendStrength);
        rayMarching.SetVector("waterColor", liquidColor);
        rayMarching.SetVector("_AmbientLight", ambientLight);
        rayMarching.SetTextureFromGlobal(0, "_DepthTexture", "_CameraDepthTexture");
        render = true;
    }

    void OnRenderImage (RenderTexture source, RenderTexture destination) {
        
        // InitRenderTexture();

        if (!render) {
            Begin();
        }

        if (render) {

            rayMarching.SetVector ("_Light", lightSource.transform.forward);

            rayMarching.SetTexture (0, "Source", source);
            rayMarching.SetTexture (0, "Destination", target);
            rayMarching.SetVector("_CameraPos", camera.transform.position);
            rayMarching.SetMatrix ("_CameraToWorld", camera.cameraToWorldMatrix);
            rayMarching.SetMatrix ("_CameraInverseProjection", camera.projectionMatrix.inverse);

            int threadGroupsX = Mathf.CeilToInt (camera.pixelWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt (camera.pixelHeight / 8.0f);
            rayMarching.Dispatch (0, threadGroupsX, threadGroupsY, 1);

            Graphics.Blit (target, destination);
        }
    }
}
