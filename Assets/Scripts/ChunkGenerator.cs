using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkGenerator : MonoBehaviour
{
    public int seed;
    [Space]
    public float noiseScale = 1;



    public void GenerateChunkData(ref Chunk chunk)
    {
        Random.InitState(seed);
        Vector3 basePos = (Vector3)chunk.Position * VoxelMap.ChunkSize * noiseScale + 50 * Random.value * Random.insideUnitSphere;

        for (int x = 0; x < chunk.nodes.Size.x; x++)
        {
            for (int y = 0; y < chunk.nodes.Size.y; y++)
            {
                for (int z = 0; z < chunk.nodes.Size.z; z++)
                {
                    if (chunk.nodes[x, y, z] == null)
                        chunk.nodes[x, y, z] = new Node();
                    Vector3 pos = basePos + new Vector3(x, y, z) * noiseScale;
                    chunk.nodes[x, y, z].isoValue = PerlinNoise3D(pos);
                    chunk.nodes[x, y, z].materialID = Mathf.CeilToInt(PerlinNoise3D((basePos + Vector3.one * 5) + new Vector3(x, y, z) * noiseScale * 3.5f) - 0.55f);
                }
            }
        }
    }

    private float PerlinNoise3D(Vector3 xyz)
    {
        float xy = Mathf.PerlinNoise(xyz.x, xyz.y);
        float xz = Mathf.PerlinNoise(xyz.x, xyz.z);
        float yz = Mathf.PerlinNoise(xyz.y, xyz.z);
        float yx = Mathf.PerlinNoise(xyz.y, xyz.x);
        float zx = Mathf.PerlinNoise(xyz.z, xyz.x);
        float zy = Mathf.PerlinNoise(xyz.z, xyz.y);

        return (xy + xz + yz + yx + zx + zy) / 6f;
    }
}
