using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class VoxelMap : MonoBehaviour
{
    // Size of chunk's node array
    public const int ChunkSize = 16;
    // The max number of triangles a chunk's mesh can have
    public const int MaxTriCount = 2500;

    [SerializeField, HideInInspector]
    internal List<Chunk> chunks = new List<Chunk>();

    public ChunkGenerator chunkGenerator;
    [Space]
    public Material meshMaterial;
    [Tooltip("Textures used for MaterialID, and indexed accordingly")]
    public Texture2D[] customMaterialTextures;
    [Space]
    public float surface = 0.5f;
    [Space]
    public bool drawDebug = false;
    [SerializeField, HideInInspector]
    private List<Vector3> debugList = new List<Vector3>();
    [Space]
    [SerializeField]
    private ComputeShader computeShader;
    private ComputeBuffer pointBuffer;
    private ComputeBuffer counterBuffer;
    // Used to structure the point buffer passed to the compute shader
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct PointData
    {
        public float iso;
        public float matID;
    };



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
    /// <param name="setupChunkImmediate">Should the chunks state be applied immediatly, or wait for UpdateChunkStates()?</param>
    /// <param name="state">The state to initilize the chunk with</param>
    /// <returns>The new chunk, or null if a chunk already exists in the position</returns>
    internal Chunk CreateChunk(Vector3Int position, bool setupChunkImmediate, Chunk.ChunkState state = Chunk.ChunkState.Active)
    {
        if (ChunkExists(position))
        {
            if (drawDebug)
                Utility.PrintWarning("Attempted to create a chunk in a used position");
            return null;
        }
        if (state == Chunk.ChunkState.Disabled)
            return null;

        Chunk newChunk = new Chunk();
        newChunk.nodes = new FlatArray3D<Node>(ChunkSize, ChunkSize, ChunkSize);
        newChunk.position = position;
        newChunk.map = this;
        // Create object as child in correct position
        newChunk.meshObject = new GameObject("Chunk (" + position.x + " " + position.y + " " + position.z + ")");
        newChunk.meshObject.transform.SetParent(transform);
        newChunk.meshObject.transform.SetPositionAndRotation(transform.position + position * ChunkSize, transform.rotation);
        newChunk.meshObject.SetActive(state == Chunk.ChunkState.Active);
        // Setup mesh
        newChunk.mesh = new Mesh();
        newChunk.mesh.name = "Mesh (" + position.x + " " + position.y + " " + position.z + ")";
        newChunk.mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        newChunk.mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        var layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3)
        };
        newChunk.mesh.SetVertexBufferParams(MaxTriCount * 3, layout);
        newChunk.mesh.SetIndexBufferParams(MaxTriCount * 3, IndexFormat.UInt32);
        newChunk.mesh.SetSubMesh(0, new SubMeshDescriptor(0, MaxTriCount * 3), MeshUpdateFlags.DontRecalculateBounds);
        newChunk.mesh.bounds = new Bounds(Vector3.zero, new Vector3(ChunkSize, ChunkSize, ChunkSize) * 2);
#if !UNITY_EDITOR
        newChunk.vertexBuffer = newChunk.mesh.GetVertexBuffer(0);
        newChunk.indexBuffer = newChunk.mesh.GetIndexBuffer();
#endif
        // Setup mesh renderer
        newChunk.meshFilter = newChunk.meshObject.AddComponent<MeshFilter>();
        newChunk.meshFilter.mesh = newChunk.mesh;
        MeshRenderer newRenderer = newChunk.meshObject.AddComponent<MeshRenderer>();
        newRenderer.sharedMaterial = meshMaterial;
        // Set texture array in property block
        Texture2DArray texArray = new Texture2DArray(2048, 2048, customMaterialTextures.Length, TextureFormat.RGBA32, false);
        for (int i = 0; i < customMaterialTextures.Length; i++)
        {
            Graphics.CopyTexture(customMaterialTextures[i], 0, 0, texArray, i, 0);
        }
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetTexture("_MatTex", texArray);
        newRenderer.SetPropertyBlock(propertyBlock);
        // Setup the chunks state
        newChunk.currentState = setupChunkImmediate ? state : Chunk.ChunkState.Disabled;
        newChunk.savedState = state;
        if (setupChunkImmediate)
        {
            FileHandler fh = FileHandler.Instance;
            if (!fh.LoadChunk(ref newChunk))
            {
                // Generate new chunk data
                chunkGenerator.GenerateChunkData(ref newChunk);
            }

            if (state == Chunk.ChunkState.Active)
            {
                GenerateChunkMesh(newChunk);
            }
        }

        chunks.Add(newChunk);
        return newChunk;
    }

    internal void DestroyChunk(Chunk chunk)
    {
        // Check we own this chunk
        if (chunk == null || !chunks.Contains(chunk))
        {
            if (drawDebug)
                Utility.PrintWarning("Attempted to destroy chunk that does not exist or belong to this map");
            return;
        }

        chunks.Remove(chunk);

        chunk.mesh?.Clear();
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
        if (chunk.vertexBuffer != null)
            chunk.vertexBuffer.Release();
        if (chunk.indexBuffer != null)
            chunk.indexBuffer.Release();
        Destroy(chunk.meshObject);
#endif
    }


    /// <summary>
    /// Set a chunks state and immediatly apply it
    /// </summary>
    public void SetChunkStateImmediate(Vector3Int chunkPos, Chunk.ChunkState state)
    {
        Chunk chunk = GetChunk(chunkPos);
        // If the chunk does not exist, create it with the state
        if (chunk == null)
        {
            CreateChunk(chunkPos, true, state);
            return;
        }

        chunk.savedState = state;
        if (chunk.currentState == state)
            return;

        // A disabled chunk is unloaded and destroied
        if (state == Chunk.ChunkState.Disabled)
        {
            FileHandler fh = FileHandler.Instance;
            fh.SaveChunk(chunk);
            DestroyChunk(chunk);
            return;
        }

        // If the chunk was disabled, load chunk data
        if (chunk.currentState == Chunk.ChunkState.Disabled)
        {
            FileHandler fh = FileHandler.Instance;
            if (!fh.LoadChunk(ref chunk))
            {
                // Generate new chunk data
                chunkGenerator.GenerateChunkData(ref chunk);
            }
        }

        if (state == Chunk.ChunkState.Active)
        {
            chunk.meshObject.SetActive(true);
            GenerateChunkMesh(chunk);
        }
        else if (state == Chunk.ChunkState.Inactive)
        {
            chunk.meshObject.SetActive(false);
            // We cant just clear the mesh because that will break the vert and index bufffers, 
            // so instead we use the compute shaders second kernel to set all tris to 0.
            if (pointBuffer == null)
            {
                CreateBuffers();
            }
#if UNITY_EDITOR
            GraphicsBuffer vertBuff = chunk.mesh.GetVertexBuffer(0);
            GraphicsBuffer indexBuff = chunk.mesh.GetIndexBuffer();
            computeShader.SetBuffer(1, "vertBuffer", vertBuff);
            computeShader.SetBuffer(1, "indexBuffer", indexBuff);
#else
            computeShader.SetBuffer(1, "vertBuffer", chunk.vertexBuffer);
            computeShader.SetBuffer(1, "indexBuffer", chunk.indexBuffer);
#endif
            computeShader.SetInt("maxTriangles", MaxTriCount);
            counterBuffer.SetCounterValue(0);
            computeShader.SetBuffer(1, "triCounter", counterBuffer);
            computeShader.Dispatch(1, 1, 1, 1);
#if UNITY_EDITOR
            vertBuff.Dispose();
            indexBuff.Dispose();
            if (!EditorApplication.isPlaying)
            {
                ReleaseBuffers();
            }
#endif
        }

        chunk.currentState = state;
    }

    /// <summary>
    /// Set a chunks state. Note the change will not take effect until UpdateChunkStates() is called
    /// </summary>
    public void SetChunkState(Vector3Int chunkPos, Chunk.ChunkState state)
    {
        Chunk chunk = GetChunk(chunkPos);
        if (chunk == null)
            return;

        chunk.savedState = state;
    }

    /// <summary>
    /// Apply any state changes made to chunks via SetChunkState()
    /// </summary>
    public void UpdateChunkStates()
    {
        FileHandler fh = FileHandler.Instance;
        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk chunk = chunks[i];
            if (chunk.currentState == chunk.savedState)
                continue;

            // A disabled chunk is unloaded and destroied
            if (chunk.savedState == Chunk.ChunkState.Disabled)
            {
                fh.SaveChunk(chunk);
                DestroyChunk(chunk);
                continue;
            }

            // If the chunk was disabled, load chunk data
            if (chunk.currentState == Chunk.ChunkState.Disabled)
            {
                if (!fh.LoadChunk(ref chunk))
                {
                    // Generate new chunk data
                    chunkGenerator.GenerateChunkData(ref chunk);
                }
            }

            if (chunk.savedState == Chunk.ChunkState.Active)
            {
                chunk.meshObject.SetActive(true);
                GenerateChunkMesh(chunk);
            }
            else if (chunk.savedState == Chunk.ChunkState.Inactive)
            {
                chunk.meshObject.SetActive(false);
                // We cant just clear the mesh because that will break the vert and index bufffers, 
                // so instead we use the compute shaders second kernel to set all tris to 0.
                if (pointBuffer == null)
                {
                    CreateBuffers();
                }
#if UNITY_EDITOR
                GraphicsBuffer vertBuff = chunk.mesh.GetVertexBuffer(0);
                GraphicsBuffer indexBuff = chunk.mesh.GetIndexBuffer();
                computeShader.SetBuffer(1, "vertBuffer", vertBuff);
                computeShader.SetBuffer(1, "indexBuffer", indexBuff);
#else
                computeShader.SetBuffer(1, "vertBuffer", chunk.vertexBuffer);
                computeShader.SetBuffer(1, "indexBuffer", chunk.indexBuffer);
#endif
                computeShader.SetInt("maxTriangles", MaxTriCount);
                counterBuffer.SetCounterValue(0);
                computeShader.SetBuffer(1, "triCounter", counterBuffer);
                computeShader.Dispatch(1, 1, 1, 1);
#if UNITY_EDITOR
                vertBuff.Dispose();
                indexBuff.Dispose();
                if (!EditorApplication.isPlaying)
                {
                    ReleaseBuffers();
                }
#endif
            }

            chunk.currentState = chunk.savedState;
        }
    }


    public void GenerateChunkMesh(Vector3Int position)
    {
        Chunk chunk = GetChunk(position);
        GenerateChunkMesh(chunk);
    }

    public void GenerateChunkMesh(Chunk chunk)
    {
        if (chunk == null)
        {
            if (drawDebug)
                Utility.PrintWarning("Attempted to update chunk that does not exist");
            return;
        }

        if (pointBuffer == null)
        {
            CreateBuffers();
        }

        int pointDataSize = ChunkSize + 1;
        int numThreadsPerAxis = Mathf.CeilToInt(pointDataSize / 4);

        counterBuffer.SetCounterValue(0);
        // Setup point cloud to include neighbours
        PointData[] pointData = new PointData[pointDataSize * pointDataSize * pointDataSize];
        PointData tempData = new PointData();
        for (int x = 0; x < pointDataSize; x++)
        {
            for (int y = 0; y < pointDataSize; y++)
            {
                for (int z = 0; z < pointDataSize; z++)
                {
                    Node n = Utility.GetValue(chunk, x, y, z);
                    if (n != null)
                    {
                        tempData.iso = n.isoValue;
                        tempData.matID = n.materialID;
                    }
                    else
                    {
                        tempData.iso = 0;
                        tempData.matID = -1;
                    }
                    pointData[(x * pointDataSize + y) * pointDataSize + z] = tempData;
                }
            }
        }
        pointBuffer.SetData(pointData, 0, 0, pointDataSize * pointDataSize * pointDataSize);

#if UNITY_EDITOR
        GraphicsBuffer vertBuff = chunk.mesh.GetVertexBuffer(0);
        GraphicsBuffer indexBuff = chunk.mesh.GetIndexBuffer();
#endif

        // Generate mesh
        computeShader.SetBuffer(0, "points", pointBuffer);
        computeShader.SetInt("numPointsPerAxis", pointDataSize);
        computeShader.SetFloat("surfaceLevel", surface);
        computeShader.SetInt("maxTriangles", MaxTriCount);
#if UNITY_EDITOR
        computeShader.SetBuffer(0, "vertBuffer", vertBuff);
        computeShader.SetBuffer(0, "indexBuffer", indexBuff);
#else
        computeShader.SetBuffer(0, "vertBuffer", chunk.vertexBuffer);
        computeShader.SetBuffer(0, "indexBuffer", chunk.indexBuffer);
#endif
        computeShader.SetBuffer(0, "triCounter", counterBuffer);
        computeShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
        // Clear unused vertexes in the mesh
#if UNITY_EDITOR
        computeShader.SetBuffer(1, "vertBuffer", vertBuff);
        computeShader.SetBuffer(1, "indexBuffer", indexBuff);
#else
        computeShader.SetBuffer(1, "vertBuffer", chunk.vertexBuffer);
        computeShader.SetBuffer(1, "indexBuffer", chunk.indexBuffer);
#endif
        computeShader.SetBuffer(1, "triCounter", counterBuffer);
        computeShader.Dispatch(1, 1, 1, 1);

#if UNITY_EDITOR
        vertBuff.Dispose();
        indexBuff.Dispose();
        if (!EditorApplication.isPlaying)
        {
            ReleaseBuffers();
        }
#endif
    }

    private void CreateBuffers()
    {
        int pointDataSize = ChunkSize + 1;
        int numVoxels = pointDataSize * pointDataSize * pointDataSize;

        // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
        // Otherwise, only create if null
        if (!Application.isPlaying || (pointBuffer == null))
        {
            pointBuffer = new ComputeBuffer(numVoxels, sizeof(float) * 2);
            counterBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
        }
    }

    private void ReleaseBuffers()
    {
        if (pointBuffer != null)
        {
            pointBuffer.Dispose();
            pointBuffer = null;
            counterBuffer.Dispose();
            counterBuffer = null;
        }
    }


    /// <summary>
    /// Optimised raycast for generated mesh
    /// </summary>
    /// <param name="origin">Ray origin</param>
    /// <param name="direction">Ray direction. Should be normilized</param>
    /// <returns>Does the ray hit anything?</returns>
    public bool Raycast(Vector3 origin, Vector3 direction)
    {
        return Raycast(origin, direction, out VoxelRaycastHit _);
    }

    /// <summary>
    /// Optimised raycast for generated mesh
    /// </summary>
    /// <param name="origin">Ray origin</param>
    /// <param name="direction">Ray direction. Should be normilized</param>
    /// <param name="hit"></param>
    /// <returns>Does the ray hit anything?</returns>
    public bool Raycast(Vector3 origin, Vector3 direction, out VoxelRaycastHit hit)
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
                hit.point = origin;
                hit.surfaceNormal = Vector3.zero;
                hit.chunk = null;
                hit.voxelIndex = index;
                return false;
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

        if (drawDebug)
        {
            // Draw cross at origin point
            float mult = 0.3f;
            Debug.DrawLine(startPos + mult * Vector3.up, startPos - mult * Vector3.up, Color.blue, 0.001f);
            Debug.DrawLine(startPos + mult * Vector3.right, startPos - mult * Vector3.right, Color.blue, 0.001f);
            Debug.DrawLine(startPos + mult * Vector3.forward, startPos - mult * Vector3.forward, Color.blue, 0.001f);
        }


        // For marching cubes
        bool isCubeValid;
        Voxel voxel = new Voxel();
        List<Vector3> verticies = new List<Vector3>();
        List<int> triangles = new List<int>();
        // For ray-tri intersection
        Vector3[] tri = new Vector3[3];
        // Search each voxel the ray passes through until we find a collision or there is no neighbouring chunk
        Vector3 tMax = new Vector3(Frac(startPos.x, step.x), Frac(startPos.y, step.y), Frac(startPos.z, step.z));
        tMax.Scale(tDelta);
        while (chunk != null)
        {
            if (drawDebug)
                debugList.Add(chunk.position * ChunkSize + index);

            // Construct voxel to generate mesh data
            isCubeValid = true;
            voxel.Setup(index);
            for (int i = 0; i < 8; i++)
            {
                Vector3Int p = voxel.Pos(i);
                voxel[i] = Utility.GetValue(chunk, p.x, p.y, p.z);
                if (voxel[i] == null)
                {
                    isCubeValid = false;
                    break;
                }
            }

            if (isCubeValid)
            {
                verticies.Clear();
                triangles.Clear();
                MarchingCubes.ProcessCube(voxel, verticies, triangles, surface, true);
                if (verticies.Count > 0)
                {
                    // Check for intersection with each triangle
                    for (int i = 0; i < triangles.Count; i += 3)
                    {
                        tri[0] = verticies[triangles[i + 0]] + chunkPos * ChunkSize;
                        tri[1] = verticies[triangles[i + 1]] + chunkPos * ChunkSize;
                        tri[2] = verticies[triangles[i + 2]] + chunkPos * ChunkSize;
                        if (Utility.RayTriangleIntersect(origin, direction, tri, out Vector3 intersect))
                        {
                            hit.point = transform.worldToLocalMatrix * intersect;
                            hit.surfaceNormal = Vector3.Cross(tri[1] - tri[0], tri[2] - tri[0]).normalized;
                            hit.chunk = chunk;
                            hit.voxelIndex = index;
                            if (drawDebug)
                                Debug.DrawLine(origin, hit.point, Color.green, 0.001f);
                            return true;
                        }
                    }
                }
            }

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

        // The ray has hit nothing
        if (drawDebug)
            Debug.DrawLine(origin, origin + direction * 50, Color.red, 0.001f);
        hit.point = tMax;
        hit.surfaceNormal = Vector3.zero;
        hit.chunk = chunk;
        hit.voxelIndex = index;
        return false;
    }


    /// <summary>
    /// Add or subtract from the iso value surrounding a raycast hit
    /// </summary>
    /// <param name="ammount">How much to change the iso value</param>
    /// <param name="radius">The radius arround the point to change</param>
    public void ModifyTerrain(in VoxelRaycastHit hit, float ammount, float radius)
    {
        ModifyTerrain(hit.chunk, hit.point, ammount, radius);
    }

    /// <summary>
    /// Add or subtract from the iso value surrounding a point
    /// </summary>
    /// <param name="ammount">How much to change the iso value</param>
    /// <param name="radius">The radius arround the point to change</param>
    public void ModifyTerrain(Vector3 point, float ammount, float radius)
    {
        Chunk chunk = GetChunk(new Vector3Int(Mathf.FloorToInt(point.x / ChunkSize),
                                              Mathf.FloorToInt(point.y / ChunkSize),
                                              Mathf.FloorToInt(point.z / ChunkSize)));
        ModifyTerrain(chunk, point, ammount, radius);
    }

    /// <summary>
    /// Add or subtract from the iso value surrounding a point
    /// </summary>
    /// <param name="chunk">The chunk that the point is relitive to</param>
    /// <param name="ammount">How much to change the iso value</param>
    /// <param name="radius">The radius arround the point to change</param>
    public void ModifyTerrain(Chunk chunk, Vector3 point, float ammount, float radius)
    {
        if (chunk == null)
            return;
        int hitX = Mathf.RoundToInt(point.x) - chunk.position.x * ChunkSize;
        int hitY = Mathf.RoundToInt(point.y) - chunk.position.y * ChunkSize;
        int hitZ = Mathf.RoundToInt(point.z) - chunk.position.z * ChunkSize;
        float sqrRadius = radius * radius;
        int extent = Mathf.CeilToInt(radius);
        float sqrDist;
        // Squared distance between the hit point and the voxel position
        sqrDist = point.x - Mathf.Floor(point.x);
        float unitOffset = sqrDist * sqrDist;
        sqrDist = point.y - Mathf.Floor(point.y);
        unitOffset += sqrDist * sqrDist;
        sqrDist = point.z - Mathf.Floor(point.z);
        unitOffset += sqrDist * sqrDist;

        for (int x = -extent; x <= extent; x++)
        {
            for (int y = -extent; y <= extent; y++)
            {
                for (int z = -extent; z <= extent; z++)
                {
                    sqrDist = x * x + y * y + z * z;
                    if (sqrDist - unitOffset < sqrRadius)
                    {
                        Node node = Utility.GetValue(chunk, hitX + x, hitY + y, hitZ + z);
                        if (node != null)
                            node.isoValue = Mathf.Clamp(node.isoValue + ammount, 0, 1);
                    }
                }
            }
        }
    }

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
}

#if UNITY_EDITOR
[CustomEditor(typeof(VoxelMap))]
class VoxelMapEditor : Editor
{
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    private static void DrawNodes(VoxelMap aTarget, GizmoType aGizmoType)
    {
        if (aTarget.drawDebug)
        {
            Camera cam = Camera.current;
            foreach (Chunk chunk in aTarget.chunks)
            {
                int sizeX = chunk.nodes.SizeX, sizeY = chunk.nodes.SizeY, sizeZ = chunk.nodes.SizeZ;
                Vector3 basePos = chunk.position * VoxelMap.ChunkSize;
                Vector3 pos;
                Vector3 viewPortPos;

                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            pos = basePos + new Vector3(x, y, z);
                            viewPortPos = cam.WorldToViewportPoint(pos);
                            if (viewPortPos.x < 0 || viewPortPos.y < 0 || viewPortPos.z < 0 || viewPortPos.x > 1 || viewPortPos.y > 1 || viewPortPos.z > 10)
                            {
                                continue;
                            }

                            if (chunk.nodes[x, y, z].materialID == 0)
                            {
                                Gizmos.color = new Color(1, 1, 1, 1);
                            }
                            else
                            {
                                Gizmos.color = new Color(1, 0, 0, 1);
                            }

                            float factor = Mathf.Pow(Mathf.Cos(Mathf.PI * viewPortPos.z * 0.1f * 0.5f), 0.5f);
                            Gizmos.DrawSphere(pos, 0.1f * factor);
                            if (viewPortPos.z < 3)
                            {
                                Gizmos.DrawLine(pos - Vector3.right, pos + Vector3.right);
                                Gizmos.DrawLine(pos - Vector3.up, pos + Vector3.up);
                                Gizmos.DrawLine(pos - Vector3.forward, pos + Vector3.forward);
                            }
                        }
                    }
                }
            }
        }
    }
}
#endif //UNITY_EDITOR
