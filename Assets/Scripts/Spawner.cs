using Unity.Mathematics;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public int particleQuantityPerAxis;
    public Vector3 centre;
    public float size;
    public float3 initialVel;
    public float jitterStrength;
    public bool showSpawnBounds;

    [Header("Info")]
    public int debug_numParticles;

    public SpawnData GetSpawnData()
    {
        int numPoints = particleQuantityPerAxis * particleQuantityPerAxis * particleQuantityPerAxis;
        float3[] points = new float3[numPoints];
        float3[] velocities = new float3[numPoints];

        int i = 0;

        for (int x = 0; x < particleQuantityPerAxis; x++)
        {
            for (int y = 0; y < particleQuantityPerAxis; y++)
            {
                for (int z = 0; z < particleQuantityPerAxis; z++)
                {
                    float tx = x / (particleQuantityPerAxis - 1f);
                    float ty = y / (particleQuantityPerAxis - 1f);
                    float tz = z / (particleQuantityPerAxis - 1f);

                    float px = (tx - 0.5f) * size + centre.x;
                    float py = (ty - 0.5f) * size + centre.y;
                    float pz = (tz - 0.5f) * size + centre.z;
                    float3 jitter = UnityEngine.Random.insideUnitSphere * jitterStrength;
                    points[i] = new float3(px, py, pz) + jitter;
                    velocities[i] = initialVel;
                    
                    i++;
                }
            }
        }
        //Debug.Log(velocities[0]);
        return new SpawnData() { points = points, velocities = velocities };
    }

    public struct SpawnData
    {
        public float3[] points;
        public float3[] velocities;
    }

    void OnValidate()
    {
        debug_numParticles = particleQuantityPerAxis * particleQuantityPerAxis * particleQuantityPerAxis;
    }

    void OnDrawGizmos()
    {
        if (showSpawnBounds && !Application.isPlaying)
        {
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawWireCube(centre, Vector3.one * size);
        }
    }
}
