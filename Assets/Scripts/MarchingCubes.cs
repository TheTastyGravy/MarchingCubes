using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingCubes
{
    private static float g_surfaceLevel;
    private static NodeMap g_map;

    public static void GenerateFullMap(NodeMap map, float surfaceLevel, bool useSmoothing)
    {
        g_surfaceLevel = surfaceLevel;
        g_map = map;

        for (int x = 0; x < map.nodes.GetLength(0) - 1; x++)
        {
            for (int y = 0; y < map.nodes.GetLength(1) - 1; y++)
            {
                for (int z = 0; z < map.nodes.GetLength(2) - 1; z++)
                {
                    // x, y, z, node value
                    Vector4 CreateCorner(int x, int y, int z)
                    {
                        return new Vector4(x, y, z, map.nodes[x, y, z].isoValue);
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
                    ProcessCube(cube, useSmoothing);
                }
            }
        }
    }

    private static void ProcessCube(in Vector4[] cube, bool useInterpolation = false)
    {
        int cubeIndex = Utility.GetCubeIndex(cube, g_surfaceLevel);

        int edgeVal = LookupTables.edgeTable[cubeIndex];
        if (edgeVal == 0)
            return;

        Vector3[] verts = new Vector3[3];

        int triIndex = cubeIndex * 15;
        for (int i = 0; i < 15 && LookupTables.triangleTable[triIndex + i] != -1; i += 3)
        {
            //create verticies for tri
            for (int j = 0; j < 3; j++)
            {
                int v1 = LookupTables.edgeConnection[LookupTables.triangleTable[triIndex + i + j], 0];
                int v2 = LookupTables.edgeConnection[LookupTables.triangleTable[triIndex + i + j], 1];
                float t = useInterpolation ? (g_surfaceLevel - cube[v1].w) / (cube[v2].w - cube[v1].w) : 0.5f;
                verts[j] = cube[v1] + (cube[v2] - cube[v1]) * t;
            }

            //the order verticies are used to make a tri
            int count = g_map.verticies.Count;
            g_map.indicies.Add(count);
            g_map.indicies.Add(count + 1);
            g_map.indicies.Add(count + 2);

            g_map.verticies.Add(verts[0]);
            g_map.verticies.Add(verts[1]);
            g_map.verticies.Add(verts[2]);
        }
    }
}
