using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingCubes
{
    private static float g_surfaceLevel;



    public static void MarchCubes(Chunk chunk, float surfaceLevel, bool useSmoothing, ref List<Vector3> verticies, ref List<int> triangles)
    {
        g_surfaceLevel = surfaceLevel;

        Vector3Int size = chunk.nodes.Size;

        for (int x = 0; x < size.x - 1; x++)
        {
            for (int y = 0; y < size.y - 1; y++)
            {
                for (int z = 0; z < size.z - 1; z++)
                {
                    // x, y, z, node value
                    Vector4 CreateCorner(int x, int y, int z)
                    {
                        return new Vector4(x, y, z, chunk.nodes[x, y, z].isoValue);
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
                    ProcessCube(cube, ref verticies, ref triangles, useSmoothing);
                }
            }
        }
    }

    public static void ProcessCube(in Vector4[] cube, ref List<Vector3> verticies, ref List<int> triangles, bool useInterpolation = false)
    {
        int cubeIndex = Utility.GetCubeIndex(cube, g_surfaceLevel);

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
                float t = useInterpolation ? (g_surfaceLevel - cube[v1].w) / (cube[v2].w - cube[v1].w) : 0.5f;
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
