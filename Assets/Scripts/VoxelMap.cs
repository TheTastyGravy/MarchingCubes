using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class VoxelMap : MonoBehaviour
{
    public List<Chunk> chunks = new List<Chunk>();

    //temp
    public Vector2Int chunkCount = Vector2Int.zero;

    public Material material;

    // Size of chunk node array
    public const int ChunkSize = 16;

    [Space]
    public bool generate = false;
    public bool setValues = false;
    public float surface = 0.5f;
    public float noiseSize = 1;
    public bool smooth;

    [SerializeField, HideInInspector]
    private bool currentSmooth;



    public Chunk GetChunk(Vector3Int index)
    {
        return chunks.Find((Chunk chunk) => chunk.position == index);
    }

    public bool ChunkExists(Vector3Int index)
    {
        return chunks.Exists((Chunk chunk) => chunk.position == index);
    }


    public void SetupChunk(Vector3Int position)
    {
        if (ChunkExists(position))
            return;

        Chunk newChunk = new Chunk();
        newChunk.nodes = new FlatArray3D<Node>(ChunkSize, ChunkSize, ChunkSize);
        newChunk.position = position;
        newChunk.map = this;
        // Create object as child in correct position
        newChunk.meshObject = new GameObject("Chunk (" + position.x + " " + position.y + " " + position.z + ")");
        newChunk.meshObject.transform.SetParent(transform);
        newChunk.meshObject.transform.SetPositionAndRotation(transform.position + position * ChunkSize, transform.rotation);
        // Setup mesh renderer
        newChunk.mesh = new Mesh();
        newChunk.mesh.name = "Mesh (" + position.x + " " + position.y + " " + position.z + ")";
        newChunk.meshFilter = newChunk.meshObject.AddComponent<MeshFilter>();
        newChunk.meshFilter.mesh = newChunk.mesh;
        MeshRenderer newRenderer = newChunk.meshObject.AddComponent<MeshRenderer>();
        newRenderer.sharedMaterial = material;

        chunks.Add(newChunk);
    }

    public void DestroyChunk(Vector3Int position)
    {
        if (!ChunkExists(position))
            return;

        Chunk chunk = GetChunk(position);
        chunks.Remove(chunk);

        chunk.mesh.Clear();
#if UNITY_EDITOR
        IEnumerator DelayedDestroy(GameObject obj)
        {
            yield return null;
            if (!EditorApplication.isPlaying)
                DestroyImmediate(obj);
            else
                Destroy(obj);
        }
        StartCoroutine(DelayedDestroy(chunk.meshObject));
#else
        Destroy(chunk.meshObject);
#endif
    }


    private void UpdateAllChunks()
    {
        foreach (Chunk chunk in chunks)
        {
            UpdateChunk(chunk);
        }
    }

    private void UpdateChunk(Chunk chunk)
    {
        List<Vector3> verticies = new List<Vector3>();
        List<int> triangles = new List<int>();
        MarchingCubes.MarchCubes(chunk, surface, smooth, ref verticies, ref triangles);

        chunk.mesh.Clear();
        chunk.mesh.SetVertices(verticies);
        chunk.mesh.SetTriangles(triangles, 0);
        chunk.mesh.RecalculateNormals();
    }


    //temp func. node data would be set by user
    private void SetValuesForAllChunks()
    {
        Vector3 offset = new Vector3(Random.Range(0f, 100f), Random.Range(0f, 100f), Random.Range(0f, 100f));
        for (int x = 0; x < chunkCount.x; x++)
        {
            for (int y = 0; y < chunkCount.y; y++)
            {
                SetupChunk(new Vector3Int(x, y, 0));
                SetChunkNodeValues(new Vector3Int(x, y, 0), offset);
            }
        }
    }

    private void DestroyAllChunks()
    {
        int count = chunks.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            DestroyChunk(chunks[i].position);
        }

        chunks.Clear();
    }



    private void OnValidate()
    {
        if (chunkCount.x * chunkCount.y != chunks.Count)
        {
            DestroyAllChunks();
            SetValuesForAllChunks();
        }

        if (generate)
        {
            generate = false;
            UpdateAllChunks();
        }

        if (setValues)
        {
            setValues = false;
            SetValuesForAllChunks();
        }

        if (smooth != currentSmooth)
        {
            currentSmooth = smooth;
            UpdateAllChunks();
        }
    }


    //TODO: replace this with layered noise
    public static float PerlinNoise3D(Vector3 xyz)
    {
        float xy = Mathf.PerlinNoise(xyz.x, xyz.y);
        float xz = Mathf.PerlinNoise(xyz.x, xyz.z);
        float yz = Mathf.PerlinNoise(xyz.y, xyz.z);
        float yx = Mathf.PerlinNoise(xyz.y, xyz.x);
        float zx = Mathf.PerlinNoise(xyz.z, xyz.x);
        float zy = Mathf.PerlinNoise(xyz.z, xyz.y);

        return (xy + xz + yz + yx + zx + zy) / 6f;
    }

    private void SetChunkNodeValues(Vector3Int index, Vector3 offset)
    {
        Chunk chunk = GetChunk(index);
        Vector3 basePos = (Vector3)index * ChunkSize * noiseSize + offset;

        for (int x = 0; x < chunk.nodes.Size.x; x++)
        {
            for (int y = 0; y < chunk.nodes.Size.y; y++)
            {
                for (int z = 0; z < chunk.nodes.Size.z; z++)
                {
                    if (chunk.nodes[x, y, z] == null)
                        chunk.nodes[x, y, z] = new Node();
                    Vector3 pos = basePos + new Vector3(x, y, z) * noiseSize;
                    chunk.nodes[x, y, z].isoValue = PerlinNoise3D(pos);
                }
            }
        }
    }
}

#if UNITY_EDITOR
//[CustomEditor(typeof(NodeMap))]
//public class NodeMapEditor : Editor
//{
//    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
//    public static void DrawPoints(NodeMap target, GizmoType gizmoType)
//    {
//        if (target.nodes == null)
//            return;
//
//        for (int x = 0; x < target.nodes.GetLength(0); x++)
//        {
//            for (int y = 0; y < target.nodes.GetLength(1); y++)
//            {
//                for (int z = 0; z < target.nodes.GetLength(2); z++)
//                {
//                    Gizmos.DrawIcon(new Vector3(x, y, z), "DotFill.tif", true, new Color(target.nodes[x, y, z].isoValue, target.nodes[x, y, z].isoValue, target.nodes[x, y, z].isoValue));
//                }
//            }
//        }
//    }
//}
#endif //UNITY_EDITOR
