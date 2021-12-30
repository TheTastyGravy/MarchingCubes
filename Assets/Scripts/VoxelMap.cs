using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class VoxelMap : MonoBehaviour
{
    // Size of chunk's node array
    public const int ChunkSize = 16;

    [SerializeField, HideInInspector]
    private List<Chunk> chunks = new List<Chunk>();
    public Material meshMaterial;

    //temp
    [Space]
    public Vector2Int chunkCount = Vector2Int.zero;
    public bool generate = false;
    public bool setValues = false;
    public float surface = 0.5f;
    public float noiseSize = 1;
    public bool smooth;

    [SerializeField, HideInInspector]
    private bool currentSmooth;

    public Transform rayCastObj;



    public Chunk GetChunk(Vector3Int position)
    {
        return chunks.Find((Chunk chunk) => chunk.position == position);
    }

    public bool ChunkExists(Vector3Int position)
    {
        return chunks.Exists((Chunk chunk) => chunk.position == position);
    }

    /// <summary>
    /// Create a new chunk at a position
    /// </summary>
    /// <param name="position">The position to create the chunk in</param>
    /// <returns>The new chunk. If a chunk alrady exists at the position, returns null</returns>
    public Chunk CreateChunk(Vector3Int position)
    {
        if (ChunkExists(position))
        {
            Utility.PrintWarning("Attempted to create a chunk in a used position");
            return null;
        }

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
        newRenderer.sharedMaterial = meshMaterial;

        chunks.Add(newChunk);
        return newChunk;
    }

    public void DestroyChunk(Vector3Int position)
    {
        Chunk chunk = GetChunk(position);
        DestroyChunk(chunk);
    }

    public void DestroyChunk(Chunk chunk)
    {
        // Check we own this chunk
        if (chunk == null || !chunks.Contains(chunk))
        {
            Utility.PrintWarning("Attempted to destroy chunk that does not exist or belong to this map");
            return;
        }

        chunks.Remove(chunk);

        chunk.mesh.Clear();
#if UNITY_EDITOR
        // Work around to allow objects to be destroyed in editor
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

    /// <summary>
    /// Update a chunks mesh
    /// </summary>
    public void UpdateChunk(Vector3Int position)
    {
        Chunk chunk = GetChunk(position);
        UpdateChunk(chunk);
    }

    /// <summary>
    /// Update a chunks mesh
    /// </summary>
    public void UpdateChunk(Chunk chunk)
    {
        // Check we own this chunk
        if (chunk == null || !chunks.Contains(chunk))
        {
            Utility.PrintWarning("Attempted to update chunk that does not exist or belong to this map");
            return;
        }

        List<Vector3> verticies = new List<Vector3>();
        List<int> triangles = new List<int>();
        MarchingCubes.MarchCubes(chunk, surface, smooth, verticies, triangles);

        chunk.mesh.Clear();
        chunk.mesh.SetVertices(verticies);
        chunk.mesh.SetTriangles(triangles, 0);
        chunk.mesh.RecalculateNormals();
    }


    private void UpdateAllChunks()
    {
        foreach (Chunk chunk in chunks)
        {
            UpdateChunk(chunk);
        }
    }


    //temp func. node data would be set by user
    private void SetValuesForAllChunks()
    {
        Vector3 offset = new Vector3(Random.Range(0f, 100f), Random.Range(0f, 100f), Random.Range(0f, 100f));
        for (int x = 0; x < chunkCount.x; x++)
        {
            for (int y = 0; y < chunkCount.y; y++)
            {
                CreateChunk(new Vector3Int(x, y, 0));
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


    public void Raycast(Vector3 origin, Vector3 direction)
    {
        // Instead of testing against every triangle in every chunk, we determine what voxels the 
        // ray passes through and only check if the ray hits the triangles in those. The method 
        // used to find what voxels the ray passes through is described here:
        // https://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.42.3443&rep=rep1&type=pdf

        // Get ray in local space
        origin = transform.worldToLocalMatrix * origin;
        direction = transform.worldToLocalMatrix * direction;
        // Used to find tMax
        float Frac(float f, int s)
        {
            if (s > 0)
                return 1 - f + Mathf.Floor(f);
            else
                return f - Mathf.Floor(f);
        }

        // Voxel index
        Vector3Int index = new Vector3Int(Mathf.FloorToInt(origin.x) % ChunkSize,
                                          Mathf.FloorToInt(origin.y) % ChunkSize,
                                          Mathf.FloorToInt(origin.z) % ChunkSize);
        // Used to increment index
        Vector3Int step = new Vector3Int(System.Math.Sign(direction.x),
                                         System.Math.Sign(direction.y),
                                         System.Math.Sign(direction.z));
        // Units of t between edges of cell, ie how much time it takes to travel in each direction
        Vector3 tDelta = new Vector3(1f / Mathf.Abs(direction.x), 1f / Mathf.Abs(direction.y), 1f / Mathf.Abs(direction.z));
        // Position to start checking voxels with
        Vector3 startPos = origin;


        // Get the chunk. If its not at the ray origin, find the first one the ray intersects
        Vector3Int chunkPos = new Vector3Int(Mathf.FloorToInt(origin.x / ChunkSize), 
                                             Mathf.FloorToInt(origin.y / ChunkSize), 
                                             Mathf.FloorToInt(origin.z / ChunkSize));
        Chunk chunk = GetChunk(chunkPos);
        if (chunk == null)
        {
            // Use the same voxel traversal algorithm, tracking the last axis used
            int axis = 0;
            Vector3 tMaxChunk = new Vector3(Frac(origin.x / ChunkSize, step.x), Frac(origin.y / ChunkSize, step.y), Frac(origin.z / ChunkSize, step.z));
            tMaxChunk.Scale(tDelta);
            for (int i = 0; i < 10 && chunk == null; i++)
            {
                if (tMaxChunk.x < tMaxChunk.y)
                {
                    if (tMaxChunk.x < tMaxChunk.z)
                    {
                        chunkPos.x += step.x;
                        tMaxChunk.x += tDelta.x;
                        axis = 0;
                    }
                    else
                    {
                        chunkPos.z += step.z;
                        tMaxChunk.z += tDelta.z;
                        axis = 2;
                    }
                }
                else
                {
                    if (tMaxChunk.y < tMaxChunk.z)
                    {
                        chunkPos.y += step.y;
                        tMaxChunk.y += tDelta.y;
                        axis = 1;
                    }
                    else
                    {
                        chunkPos.z += step.z;
                        tMaxChunk.z += tDelta.z;
                        axis = 2;
                    }
                }
                chunk = GetChunk(chunkPos);
            }
            if (chunk == null)
            {
                //Utility.PrintWarning("Raycast does not intersect with any chunks");
                return;
            }

            // Find intersection point with axis aligned plane
            float dist = chunk.position[axis] * ChunkSize + (step[axis] > 0 ? 0 : ChunkSize);
            dist = (dist - origin[axis]) / direction[axis];
            startPos = origin + direction * dist;
            index = new Vector3Int(Mathf.FloorToInt(startPos.x) % ChunkSize,
                                   Mathf.FloorToInt(startPos.y) % ChunkSize,
                                   Mathf.FloorToInt(startPos.z) % ChunkSize);
            // If on the negitive edge, move inside its bounds
            if (step[axis] < 0)
            {
                index[axis] = ChunkSize - 1;
                startPos[axis] -= 0.001f;
            }
        }


        // Search each voxel the ray passes through until we find a collision or there is no neighbouring chunk
        Vector3 tMax = new Vector3(Frac(startPos.x, step.x), Frac(startPos.y, step.y), Frac(startPos.z, step.z));
        tMax.Scale(tDelta);
        while (chunk != null)
        {
            debugList.Add(chunk.position * ChunkSize + index);

            //check for collision in voxel
            //note: we assume that the chunk has its edges filled, ie it has neighbours
            //if there is no neighbour, treat the cell as empty

            // Increment index
            if (tMax.x < tMax.y)
            {
                if (tMax.x < tMax.z)
                {
                    index.x += step.x;
                    tMax.x += tDelta.x;
                }
                else
                {
                    index.z += step.z;
                    tMax.z += tDelta.z;
                }
            }
            else
            {
                if (tMax.y < tMax.z)
                {
                    index.y += step.y;
                    tMax.y += tDelta.y;
                }
                else
                {
                    index.z += step.z;
                    tMax.z += tDelta.z;
                }
            }

            // If we have reached the end of the chunk, get the next chunk
            if (index.x >= ChunkSize || index.x < 0 ||
                index.y >= ChunkSize || index.y < 0 ||
                index.z >= ChunkSize || index.z < 0)
            {
                if (index.x < 0)
                {
                    index.x = ChunkSize - 1;
                    chunkPos.x += step.x;
                }
                if (index.x >= ChunkSize)
                {
                    index.x = 0;
                    chunkPos.x += step.x;
                }
                if (index.y < 0)
                {
                    index.y = ChunkSize - 1;
                    chunkPos.y += step.y;
                }
                if (index.y >= ChunkSize)
                {
                    index.y = 0;
                    chunkPos.y += step.y;
                }
                if (index.z < 0)
                {
                    index.z = ChunkSize - 1;
                    chunkPos.z += step.z;
                }
                if (index.z >= ChunkSize)
                {
                    index.z = 0;
                    chunkPos.z += step.z;
                }
                chunk = GetChunk(chunkPos);
            }
        }


        Debug.DrawRay(origin, direction * 35, Color.red, 0.001f);
        // Draw cross at origin point
        float mult = 0.3f;
        Debug.DrawLine(startPos + mult * Vector3.up, startPos - mult * Vector3.up, Color.blue, 0.001f);
        Debug.DrawLine(startPos + mult * Vector3.right, startPos - mult * Vector3.right, Color.blue, 0.001f);
        Debug.DrawLine(startPos + mult * Vector3.forward, startPos - mult * Vector3.forward, Color.blue, 0.001f);
    }

    public List<Vector3> debugList = new List<Vector3>();

    private void OnDrawGizmos()
    {
        Color oldColor = Gizmos.color;
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        foreach (var obj in debugList)
        {
            Gizmos.DrawCube(obj + new Vector3(0.5f,0.5f,0.5f), Vector3.one);
        }
        Gizmos.color = oldColor;
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

    private void Update()
    {
        if (rayCastObj != null)
        {
            debugList.Clear();
            Raycast(rayCastObj.position, rayCastObj.forward);
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
