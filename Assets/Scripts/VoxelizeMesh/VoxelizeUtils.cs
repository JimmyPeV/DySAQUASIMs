using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VoxelizeUtils
{
    [MenuItem("Tools/Voxelize Selection")]
    public static void VoxelizeSelectedObject(MenuCommand command)
    {
        GameObject meshFilterGameObject =
            Selection.gameObjects.First(o => o.TryGetComponent(out MeshFilter meshFilter));
        VoxelizeMesh(meshFilterGameObject.GetComponent<MeshFilter>());
    }

    public static void VoxelizeMesh(MeshFilter meshFilter)
    {
        if (!meshFilter.TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
        }

        if (!meshFilter.TryGetComponent(out VoxelizedMesh voxelizedMesh))
        {
            voxelizedMesh = meshFilter.gameObject.AddComponent<VoxelizedMesh>();
        }

        Bounds bounds = meshCollider.bounds;
        Vector3 minExtents = bounds.center - bounds.extents;
        float halfSize = voxelizedMesh.HalfSize;
        Vector3 count = bounds.extents / halfSize;

        int xGridSize = Mathf.CeilToInt(count.x);
        int yGridSize = Mathf.CeilToInt(count.y);
        int zGridSize = Mathf.CeilToInt(count.z);

        voxelizedMesh.GridPoints.Clear();
        voxelizedMesh.LocalOrigin = voxelizedMesh.transform.InverseTransformPoint(minExtents);

        for (int x = 0; x < xGridSize; ++x)
        {
            for (int z = 0; z < zGridSize; ++z)
            {
                for (int y = 0; y < yGridSize; ++y)
                {
                    Vector3 pos = voxelizedMesh.PointToPosition(new Vector3Int(x, y, z));
                    if (Physics.CheckBox(pos, new Vector3(halfSize, halfSize, halfSize)))
                    {
                        voxelizedMesh.GridPoints.Add(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        Mesh newVoxelizedMesh = GenerateNewVoxelizedMesh(meshFilter, voxelizedMesh.GridPoints, voxelizedMesh);
        SaveVoxelizedMesh(newVoxelizedMesh, voxelizedMesh.gameObject);
    }

    private static Mesh GenerateNewVoxelizedMesh(MeshFilter meshFilt, List<Vector3Int> gridPoints, VoxelizedMesh voxelMesh)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float size = voxelMesh.HalfSize * 2f;

        // Iterar sobre los puntos de la malla voxelizada y generar los vértices
        foreach (Vector3Int gridPoint in gridPoints)
        {
            Vector3 basePos = new Vector3(gridPoint.x * size, gridPoint.y * size, gridPoint.z * size);
            Vector3 worldPos = voxelMesh.LocalOrigin + voxelMesh.transform.TransformPoint(basePos);

            // Agregar los 8 vértices de un cubo a la lista de vértices
            vertices.Add(worldPos);
            vertices.Add(worldPos + new Vector3(size, 0, 0));
            vertices.Add(worldPos + new Vector3(size, size, 0));
            vertices.Add(worldPos + new Vector3(0, size, 0));
            vertices.Add(worldPos + new Vector3(0, 0, size));
            vertices.Add(worldPos + new Vector3(size, 0, size));
            vertices.Add(worldPos + new Vector3(size, size, size));
            vertices.Add(worldPos + new Vector3(0, size, size));
        }

        // Generar los triángulos para cada cubo
        int numVertices = 8;
        for (int i = 0; i < gridPoints.Count; i++)
        {
            int baseIndex = i * numVertices;
            // Cara frontal
            AddQuad(triangles, baseIndex + 0, baseIndex + 1, baseIndex + 2, baseIndex + 3);
            // Cara trasera
            AddQuad(triangles, baseIndex + 4, baseIndex + 7, baseIndex + 6, baseIndex + 5);
            // Cara superior
            AddQuad(triangles, baseIndex + 3, baseIndex + 2, baseIndex + 6, baseIndex + 7);
            // Cara inferior
            AddQuad(triangles, baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 1);
            // Cara derecha
            AddQuad(triangles, baseIndex + 1, baseIndex + 5, baseIndex + 6, baseIndex + 2);
            // Cara izquierda
            AddQuad(triangles, baseIndex + 0, baseIndex + 3, baseIndex + 7, baseIndex + 4);
        }

        // Crear la malla y asignarla al MeshFilter
        Mesh voxelizedMesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            // Opcional: recalcula normales y límites si es necesario
        };
        meshFilt.mesh = voxelizedMesh;

        return voxelizedMesh;
    }

    // Método auxiliar para añadir los índices de los cuadriláteros (quads)
    private static void AddQuad(List<int> triangles, int v0, int v1, int v2, int v3)
    {
        triangles.Add(v0);
        triangles.Add(v1);
        triangles.Add(v2);
        triangles.Add(v2);
        triangles.Add(v3);
        triangles.Add(v0);
    }



    public static void SaveVoxelizedMesh(Mesh voxelizedMesh, GameObject gameObject)
    {
        VoxelizedMesh voxelizedMeshComponent = gameObject.GetComponent<VoxelizedMesh>();
        if (voxelizedMeshComponent == null)
        {
            Debug.LogError("No VoxelizedMesh component attached to the GameObject.");
            return;
        }

        // Obtener el nombre del objeto voxelizado
        string objectName = gameObject.name;

        // Asegúrate de que el nombre sea único
        string uniqueName = GetUniqueMeshName(objectName);

        // Guardar la malla voxelizada con el nombre único
        string path = "Assets/VoxelizedMeshes/" + uniqueName + "_Voxelized.asset";
        AssetDatabase.CreateAsset(voxelizedMesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Voxelized mesh saved at: " + path);
    }


    private static string GetUniqueMeshName(string objectName)
    {
        string uniqueName = objectName;
        int index = 1;

        // Verifica si el nombre ya existe, y si es así, agrega un número incremental al final
        while (AssetDatabase.LoadAssetAtPath<Mesh>("Assets/VoxelizedMeshes/" + uniqueName + "_Voxelized.asset") != null)
        {
            uniqueName = objectName + "_" + index;
            index++;
        }

        return uniqueName;
    }
}