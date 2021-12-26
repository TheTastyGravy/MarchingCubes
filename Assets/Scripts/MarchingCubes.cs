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
    public static void MarchCubes(Chunk chunk, float surfaceLevel, bool useSmoothing, List<Vector3> verticies, List<int> triangles)
    {
        Vector3Int size = chunk.nodes.Size;
        bool isCubeValid;
        bool xEdge, yEdge, zEdge;
        // Vertex caches. They are indexed using a nodes X and Z position, and are reset after each layer (Y axis)
        FlatArray3D<int> topCache = new FlatArray3D<int>(size.y + 1, size.z + 1, 3);
        FlatArray3D<int> bottomCache = new FlatArray3D<int>(size.y + 1, size.z + 1, 3);
        for (int i = 0; i < topCache.Length; i++)
        {
            topCache[i] = -1;
            bottomCache[i] = -1;
        }

        for (int y = 0; y < size.y; y++)
        {
            yEdge = y == size.y - 1;
            for (int x = 0; x < size.x; x++)
            {
                xEdge = x == size.x - 1;
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
                        ProcessCubeWithCache(cube, verticies, triangles, topCache, bottomCache, surfaceLevel, useSmoothing);
                    }
                }
            }
            // Setup cache for next layer
            bottomCache.CopyValuesFrom(topCache);
            for (int i = 0; i < topCache.Length; i++)
            {
                topCache[i] = -1;
            }
        }
    }

    // Used internaly only, by MarchCubes
    private static void ProcessCubeWithCache(Vector4[] cube, List<Vector3> verticies, List<int> triangles, FlatArray3D<int> topCache, 
                                             FlatArray3D<int> bottomCache, float surfaceLevel, bool useSmoothing)
    {
        int cubeIndex = Utility.GetCubeIndex(cube, surfaceLevel);
        if (LookupTables.edgeTable[cubeIndex] == 0)
            return;

        int[] vertIndices = new int[12];
        void CacheEdge(int edgeValue, int primaryCorner, FlatArray3D<int> cache)
        {
            // The edge type depends on its orientation, with 0 being along the X axis, 1 along Z, and 2 along Y.
            int edgeType = (edgeValue < 8) ? edgeValue % 2 : 2;
            if (cache[(int)cube[primaryCorner].x, (int)cube[primaryCorner].z, edgeType] != -1)
            {
                vertIndices[edgeValue] = cache[(int)cube[primaryCorner].x, (int)cube[primaryCorner].z, edgeType];
            }
            else
            {
                // Create new vertex
                vertIndices[edgeValue] = verticies.Count;
                cache[(int)cube[primaryCorner].x, (int)cube[primaryCorner].z, edgeType] = vertIndices[edgeValue];
                verticies.Add(Utility.LerpEdge(cube[LookupTables.edgeConnection[edgeValue, 0]], 
                                               cube[LookupTables.edgeConnection[edgeValue, 1]], 
                                               surfaceLevel, useSmoothing));
            }
        }
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 0) != 0)
            CacheEdge(0, 0, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 1) != 0)
            CacheEdge(1, 2, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 2) != 0)
            CacheEdge(2, 3, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 3) != 0)
            CacheEdge(3, 3, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 4) != 0)
            CacheEdge(4, 4, topCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 5) != 0)
            CacheEdge(5, 6, topCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 6) != 0)
            CacheEdge(6, 7, topCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 7) != 0)
            CacheEdge(7, 7, topCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 8) != 0)
            CacheEdge(8, 0, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 9) != 0)
            CacheEdge(9, 1, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 10) != 0)
            CacheEdge(10, 2, bottomCache);
        if ((LookupTables.edgeTable[cubeIndex] & 1 << 11) != 0)
            CacheEdge(11, 3, bottomCache);

        for (int i = 0; i < LookupTables.triangleTable[cubeIndex].Length; i++)
        {
            triangles.Add(vertIndices[LookupTables.triangleTable[cubeIndex][i]]);
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
    public static void ProcessCube(Vector4[] cube, List<Vector3> verticies, List<int> triangles, 
                                   float surfaceLevel, bool useSmoothing)
    {
        int cubeIndex = Utility.GetCubeIndex(cube, surfaceLevel);
        if (LookupTables.edgeTable[cubeIndex] == 0)
            return;

        // Generate verticies for used edges and save their index
        int[] vertIndices = new int[12];
        void CacheEdge(int edgeVal)
        {
            if ((LookupTables.edgeTable[cubeIndex] & 1 << edgeVal) != 0)
            {
                vertIndices[edgeVal] = verticies.Count;
                verticies.Add(Utility.LerpEdge(cube[LookupTables.edgeConnection[edgeVal, 0]], 
                                               cube[LookupTables.edgeConnection[edgeVal, 1]], 
                                               surfaceLevel, useSmoothing));
            }
        }
        for (int i = 0; i < 12; i++)
        {
            CacheEdge(i);
        }

        for (int i = 0; i < LookupTables.triangleTable[cubeIndex].Length; i++)
        {
            triangles.Add(vertIndices[LookupTables.triangleTable[cubeIndex][i]]);
        }
    }
}
