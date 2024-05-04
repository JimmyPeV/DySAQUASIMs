using UnityEngine;

public class Display : MonoBehaviour
{

    public Shader displayShader;
    public float meshScale;
    public Color displayColor;
    public Gradient colorGradient;
    public int gradientResolution;
    public float maxVelocityDisplay;
    public int meshResolution;

    private Mesh sphereMesh;
    private Material displayMaterial;
    private ComputeBuffer drawArgsBuffer;
    private Bounds drawBounds;
    private Texture2D gradientTexture;
    private bool gradientNeedsUpdate;
    private int debugMeshTriangleCount;

    #region Initialization
    public void Init(Simulation3D simulation)
    {
        CreateMaterial(simulation);
        GenerateMesh();
        SetupDrawArgumentsBuffer(simulation);
        SetupDrawBounds();
    }

    private void CreateMaterial(Simulation3D simulation)
    {
        displayMaterial = new Material(displayShader);
        displayMaterial.SetBuffer("Positions", simulation.positionBuffer);
        displayMaterial.SetBuffer("Velocities", simulation.velocityBuffer);
    }
    private void GenerateMesh()
    {
        sphereMesh = SphereStuff.SphereGenerator.GenerateSphereMesh(meshResolution);
        debugMeshTriangleCount = sphereMesh.triangles.Length / 3;
    }

    private void SetupDrawArgumentsBuffer(Simulation3D simulation)
    {
        drawArgsBuffer = ComputeHelper.CreateArgsBuffer(sphereMesh, simulation.positionBuffer.count);
    }

    private void SetupDrawBounds()
    {
        drawBounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }
    #endregion

    #region Update Settings and Rendering
    void LateUpdate()
    {
        UpdateSettings();
        Graphics.DrawMeshInstancedIndirect(sphereMesh, 0, displayMaterial, drawBounds, drawArgsBuffer);
    }

    private void UpdateSettings()
    {
        CheckAndUpdateGradientTexture();
        UpdateMaterialProperties();
        UpdateTransformMatrix();
    }

    private void CheckAndUpdateGradientTexture()
    {
        if (gradientNeedsUpdate)
        {
            gradientNeedsUpdate = false;
            CreateTextureFromGradient(ref gradientTexture, gradientResolution, colorGradient);
            displayMaterial.SetTexture("ColourMap", gradientTexture);
        }
    }

    private void UpdateMaterialProperties()
    {
        displayMaterial.SetFloat("scale", meshScale);
        displayMaterial.SetColor("colour", displayColor);
        displayMaterial.SetFloat("velocityMax", maxVelocityDisplay);
    }
    private void UpdateTransformMatrix()
    {
        Vector3 originalScale = transform.localScale;
        transform.localScale = Vector3.one;
        displayMaterial.SetMatrix("localToWorld", transform.localToWorldMatrix);
        transform.localScale = originalScale;
    }
    #endregion

    #region Utility Methods

    public static void CreateTextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (texture == null || texture.width != width)
        {
            texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = filterMode;

        Color[] colors = new Color[width];
        for (int i = 0; i < width; i++)
        {
            float t = i / (float)(width - 1);
            colors[i] = gradient.Evaluate(t);
        }
        texture.SetPixels(colors);
        texture.Apply();
    }

    private void OnValidate()
    {
        gradientNeedsUpdate = true;
    }

    void OnDestroy()
    {
        ComputeHelper.Release(drawArgsBuffer);
    }

    #endregion
}
