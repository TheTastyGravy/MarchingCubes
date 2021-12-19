using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingCubes
{
    /// <summary>
    /// Generate mesh data for chunk, including gaps with neighbours
    /// </summary>
    /// <param name="chunk"></param>
    /// <param name="surfaceLevel"></param>
    /// <param name="useSmoothing">Should interpolation be used?</param>
    /// <param name="verticies"></param>
    /// <param name="triangles"></param>
    public static void MarchCubes(Chunk chunk, float surfaceLevel, bool useSmoothing, ref List<Vector3> verticies, ref List<int> triangles)
    {
        Vector3Int size = chunk.nodes.Size;
        bool isCubeValid;
        bool xEdge, yEdge, zEdge;

        for (int x = 0; x < size.x; x++)
        {
            xEdge = x == size.x - 1;
            for (int y = 0; y < size.y; y++)
            {
                yEdge = y == size.y - 1;
                for (int z = 0; z < size.z; z++)
                {
                    zEdge = z == size.z - 1;
                    isCubeValid = true;
                    // x, y, z, node value
                    Vector4 CreateCorner(int x, int y, int z)
                    {
                        // Not an edge so we can use the current chunk
                        if (!xEdge && !yEdge && !zEdge)
                            return new Vector4(x, y, z, chunk.nodes[x, y, z].isoValue);
                        // A nessesary neighbour doesnt exist, so dont try
                        if (!isCubeValid)
                            return Vector4.zero;

                        Vector4 result = new Vector4(x, y, z, 0);
                        // Check if we need to get a neighbouring chunk
                        Vector3Int chunkIndex = Vector3Int.zero;
                        if (x == size.x)
                        {
                            chunkIndex.x = 1;
                            x = 0;
                        }
                        if (y == size.y)
                        {
                            chunkIndex.y = 1;
                            y = 0;
                        }
                        if (z == size.z)
                        {
                            chunkIndex.z = 1;
                            z = 0;
                        }

                        if (chunkIndex == Vector3Int.zero)
                        {
                            result.w = chunk.nodes[x, y, z].isoValue;
                        }
                        else
                        {
                            // Use the neighbour chunk
                            Chunk current = chunk.map.GetChunk(chunk.position + chunkIndex);
                            if (current == null)
                                isCubeValid = false;
                            else
                                result.w = current.nodes[x, y, z].isoValue;
                        }

                        return result;
                    }

                    Vector4[] cube = new Vector4[8]
                    {
                        CreateCorner(x, y, z + 1),
                        CreateCorner(x + 1, y, z + 1),
                        CreateCorner(x + 1, y, z),
                        CreateCorner(x, y, z),
                        CreateCorner(x, y + 1, z + 1),
                        CreateCorner(x + 1, y + 1, z + 1),
                        CreateCorner(x + 1, y + 1, z),
                        CreateCorner(x, y + 1, z),
                    };

                    if (isCubeValid)
                    {
                        ProcessCube(cube, ref verticies, ref triangles, surfaceLevel, useSmoothing);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generate mesh data for a single voxel
    /// </summary>
    /// <param name="cube">Corners forming the cube. [X, Y, Z, Value]</param>
    /// <param name="verticies"></param>
    /// <param name="triangles"></param>
    /// <param name="surfaceLevel"></param>
    /// <param name="useSmoothing">Should interpolation be used?</param>
    public static void ProcessCube(in Vector4[] cube, ref List<Vector3> verticies, ref List<int> triangles, 
                                   float surfaceLevel, bool useSmoothing)
    {
        int cubeIndex = Utility.GetCubeIndex(cube, surfaceLevel);

        int edgeVal = LookupTables.edgeTable[cubeIndex];
        if (edgeVal == 0)
            return;

        Vector3[] verts = new Vector3[3];

        int[] triList = LookupTables.triangleTable[cubeIndex];
        for (int i = 0; i < triList.Length; i += 3)
        {
            //create verticies for tri
            for (int j = 0; j < 3; j++)
            {
                int v1 = LookupTables.edgeConnection[triList[i + j], 0];
                int v2 = LookupTables.edgeConnection[triList[i + j], 1];
                float t = useSmoothing ? (surfaceLevel - cube[v1].w) / (cube[v2].w - cube[v1].w) : 0.5f;
                verts[j] = cube[v1] + (cube[v2] - cube[v1]) * t;
            }

            //the order verticies are used to make a tri
            int count = verticies.Count;
            triangles.Add(count);
            triangles.Add(count + 1);
            triangles.Add(count + 2);

            verticies.Add(verts[0]);
            verticies.Add(verts[1]);
            verticies.Add(verts[2]);
        }
    }
}
