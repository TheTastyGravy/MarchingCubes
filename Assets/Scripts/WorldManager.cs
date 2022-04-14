using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public VoxelMap voxelMap;
    public ChunkGenerator chunkGenerator;

    [Header("Map controls")]
    public Vector3Int chunkCount = Vector3Int.zero;
    [Space]
    public bool buildMesh = false;
    public bool randomise = false;
    public bool rebuildChunks = false;
    public bool save = false;
    public bool load = false;
    [Header("State controls")]
    public Vector3Int chunkIndex;
    public Chunk.ChunkState state;
    public bool setState = false;
    [Header("Terrain editing")]
    public float addRate = 1;
    public float removeRate = 1;
    public float radius = 1.25f;
    
    private Camera cam;



    void Awake()
    {
        DestroyAllChunks();
        CreateChunks(chunkCount);
        LoadAllChunksData();
        MeshAllChunks();
    }

    void Update()
    {
        if (cam == null)
            cam = Camera.main;
    
        // Left button used to add to surface, right button to remove. Simple implementation for testing
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            Ray mouseRay = cam.ScreenPointToRay(Input.mousePosition);
            if (voxelMap.Raycast(mouseRay.origin, mouseRay.direction, out VoxelRaycastHit hit))
            {
                voxelMap.ModifyTerrain(hit, (Input.GetMouseButton(0) ? addRate : -removeRate) * Time.deltaTime, radius);
                MeshAllChunks();
            }
        }
    }

    void OnValidate()
    {
        if (buildMesh)
        {
            buildMesh = false;
            MeshAllChunks();
        }
        if (randomise)
        {
            randomise = false;
            GenerateAllChunksData();
        }
        if (rebuildChunks)
        {
            rebuildChunks = false;
            DestroyAllChunks();
            CreateChunks(chunkCount);
            LoadAllChunksData();
            MeshAllChunks();
        }

        if (save)
        {
            save = false;
            FileHandler fh = FileHandler.Instance;
            foreach (Chunk chunk in voxelMap.chunks)
            {
                fh.SaveChunk(chunk);
            }
        }
        if (load)
        {
            load = false;
            LoadAllChunksData();
        }

        if (setState)
        {
            setState = false;
            voxelMap.SetChunkStateImmediate(chunkIndex, state);
        }
    }


    private void CreateChunks(Vector3Int count)
    {
        for (int x = 0; x < count.x; x++)
        {
            for (int y = 0; y < count.y; y++)
            {
                for (int z = 0; z < count.z; z++)
                {
                    voxelMap.CreateChunk(new Vector3Int(x, y, z), false);
                }
            }
        }
    }

    private void DestroyAllChunks()
    {
        int count = voxelMap.chunks.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            voxelMap.DestroyChunk(voxelMap.chunks[i]);
        }
    }

    public void LoadAllChunksData()
    {
        FileHandler fh = FileHandler.Instance;
        for (int i = 0; i < voxelMap.chunks.Count; i++)
        {
            Chunk chunk = voxelMap.chunks[i];
            fh.LoadChunk(ref chunk);
        }
    }

    public void GenerateAllChunksData()
    {
        for (int i = 0; i < voxelMap.chunks.Count; i++)
        {
            Chunk chunk = voxelMap.chunks[i];
            chunkGenerator.GenerateChunkData(ref chunk);
        }
    }

    public void MeshAllChunks()
    {
        foreach (Chunk chunk in voxelMap.chunks)
        {
            voxelMap.GenerateChunkMesh(chunk);
        }
    }
}
