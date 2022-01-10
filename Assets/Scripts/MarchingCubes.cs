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
    /// <param name="normals"></param>
    /// <param name="uvs"></param>
    public static void MarchCubes(Chunk chunk, float surfaceLevel, bool useSmoothing, List<Vector3> verticies, List<int> triangles, List<Vector3> normals, List<Vector2> uvs)
    {
        int sizeX = chunk.nodes.SizeX;
        int sizeY = chunk.nodes.SizeY;
        int sizeZ = chunk.nodes.SizeZ;
        bool isCubeValid;
        Voxel voxel = new Voxel();
        // Vertex caches. They are indexed using a nodes X and Z position, and are reset after each layer (Y axis)
        FlatArray3D<int> topCache = new FlatArray3D<int>(sizeY + 1, sizeZ + 1, 3);
        FlatArray3D<int> bottomCache = new FlatArray3D<int>(sizeY + 1, sizeZ + 1, 3);
        for (int i = 0; i < topCache.Length; i++)
        {
            topCache[i] = -1;
            bottomCache[i] = -1;
        }

        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    isCubeValid = true;
                    voxel.Setup(new Vector3Int(x,y,z));
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
                        int cubeIndex = Utility.GetCubeIndex(voxel, surfaceLevel);
                        if (LookupTables.edgeTable[cubeIndex] == 0)
                            continue;

                        int[] vertIndices = new int[12];
                        // Caches edge vertex index and calculates the vertex normal
                        void CacheEdge(int edgeValue, int primaryCorner, FlatArray3D<int> cache)
                        {
                            // The edge type depends on its orientation, with 0 being along the X axis, 1 along Z, and 2 along Y.
                            int edgeType = (edgeValue < 8) ? edgeValue % 2 : 2;
                            Vector3Int primaryPos = voxel.Pos(primaryCorner);
                            if (cache[primaryPos.x, primaryPos.z, edgeType] != -1)
                            {
                                vertIndices[edgeValue] = cache[primaryPos.x, primaryPos.z, edgeType];
                            }
                            else
                            {
                                // Create new vertex
                                vertIndices[edgeValue] = verticies.Count;
                                cache[primaryPos.x, primaryPos.z, edgeType] = vertIndices[edgeValue];
                                Vector3 vertex = Utility.LerpEdge(voxel.GetVertex(LookupTables.edgeConnection[edgeValue, 0]),
                                                                  voxel.GetVertex(LookupTables.edgeConnection[edgeValue, 1]),
                                                                  surfaceLevel, useSmoothing);
                                verticies.Add(vertex);
                                // Get initial material data. CreateEdgeVertex will update the data if the vertex has a material.
                                Node node0 = voxel[LookupTables.edgeConnection[edgeValue, 0]];
                                Node node1 = voxel[LookupTables.edgeConnection[edgeValue, 1]];
                                Vector2 matData = new Vector2(Mathf.Max(node0.materialID, node1.materialID), (surfaceLevel - node0.isoValue) / (node1.isoValue - node0.isoValue));
                                void MatTest(int x, int y, int z, Node n)
                                {
                                    if (n.materialID > matData.x)
                                    {
                                        matData.x = n.materialID;
                                        matData.y = n.isoValue;
                                    }
                                }

                                // Calculate the vertex normal using data from the surounding verticies. The algorithm is described here:
                                // https://www.researchgate.net/publication/220944287_Approximating_Normals_for_Marching_Cubes_applied_to_Locally_Supported_Isosurfaces
                                int v0X, v0Y, v0Z, v1X, v1Y, v1Z;
                                {
                                    Vector3Int t = voxel.Pos(LookupTables.edgeConnection[edgeValue, 0]);
                                    v0X = t.x;
                                    v0Y = t.y;
                                    v0Z = t.z;
                                    t = voxel.Pos(LookupTables.edgeConnection[edgeValue, 1]);
                                    v1X = t.x;
                                    v1Y = t.y;
                                    v1Z = t.z;
                                }
                                if (voxel[LookupTables.edgeConnection[edgeValue, 0]].isoValue > surfaceLevel)
                                {
                                    int tX, tY, tZ;
                                    tX = v0X;
                                    tY = v0Y;
                                    tZ = v0Z;
                                    v0X = v1X;
                                    v0Y = v1Y;
                                    v0Z = v1Z;
                                    v1X = tX;
                                    v1Y = tY;
                                    v1Z = tZ;
                                }

                                // Get adjacent edge verticies
                                Vector3 p0 = Vector3.zero, p1 = p0, p2 = p0, p3 = p0;
                                Node v0Val = Utility.GetValue(chunk, v0X, v0Y, v0Z);
                                Node v1Val = Utility.GetValue(chunk, v1X, v1Y, v1Z);
                                void GetVerticiesX(out Vector3 edge0, out Vector3 edge1)
                                {
                                    Node v0a, v1a;
                                    // Negitive
                                    v0a = Utility.GetValue(chunk, v0X - 1, v0Y, v0Z);
                                    if (v0a?.isoValue > surfaceLevel)
                                    {
                                        edge0 = Utility.LerpEdge(v0X, v0Y, v0Z, v0Val.isoValue, v0X - 1, v0Y, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                        MatTest(v0X - 1, v0Y, v0Z, v0a);
                                    }
                                    else
                                    {
                                        v1a = Utility.GetValue(chunk, v1X - 1, v1Y, v1Z);
                                        if (v1a != null)
                                        {
                                            if (v1a.isoValue > surfaceLevel)
                                            {
                                                edge0 = Utility.LerpEdge(v1X - 1, v1Y, v1Z, v1a.isoValue, v0X - 1, v0Y, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                                MatTest(v0X - 1, v0Y, v0Z, v0a);
                                            }
                                            else
                                            {
                                                edge0 = Utility.LerpEdge(v1X - 1, v1Y, v1Z, v1a.isoValue, v1X, v1Y, v1Z, v1Val.isoValue, surfaceLevel, useSmoothing);
                                            }
                                            MatTest(v1X - 1, v1Y, v1Z, v1a);
                                        }
                                        else
                                        {
                                            edge0 = Utility.LerpEdge(v1X - 1, v1Y, v1Z, 1, v0X - 1, v0Y, v0Z, 1, surfaceLevel, false);
                                        }
                                    }
                                    // Positive
                                    v0a = Utility.GetValue(chunk, v0X + 1, v0Y, v0Z);
                                    if (v0a?.isoValue > surfaceLevel)
                                    {
                                        edge1 = Utility.LerpEdge(v0X, v0Y, v0Z, v0Val.isoValue, v0X + 1, v0Y, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                        MatTest(v0X + 1, v0Y, v0Z, v0a);
                                    }
                                    else
                                    {
                                        v1a = Utility.GetValue(chunk, v1X + 1, v1Y, v1Z);
                                        if (v1a != null)
                                        {
                                            if (v1a.isoValue > surfaceLevel)
                                            {
                                                edge1 = Utility.LerpEdge(v1X + 1, v1Y, v1Z, v1a.isoValue, v0X + 1, v0Y, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                                MatTest(v0X + 1, v0Y, v0Z, v0a);
                                            }
                                            else
                                            {
                                                edge1 = Utility.LerpEdge(v1X + 1, v1Y, v1Z, v1a.isoValue, v1X, v1Y, v1Z, v1Val.isoValue, surfaceLevel, useSmoothing);
                                            }
                                            MatTest(v1X + 1, v1Y, v1Z, v1a);
                                        }
                                        else
                                        {
                                            edge1 = Utility.LerpEdge(v1X + 1, v1Y, v1Z, 1, v0X + 1, v0Y, v0Z, 1, surfaceLevel, false);
                                        }
                                    }
                                }
                                void GetVerticiesY(out Vector3 edge0, out Vector3 edge1)
                                {
                                    Node v0a, v1a;
                                    // Negitive
                                    v0a = Utility.GetValue(chunk, v0X, v0Y - 1, v0Z);
                                    if (v0a?.isoValue > surfaceLevel)
                                    {
                                        edge0 = Utility.LerpEdge(v0X, v0Y, v0Z, v0Val.isoValue, v0X, v0Y - 1, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                        MatTest(v0X, v0Y - 1, v0Z, v0a);
                                    }
                                    else
                                    {
                                        v1a = Utility.GetValue(chunk, v1X, v1Y - 1, v1Z);
                                        if (v1a != null)
                                        {
                                            if (v1a.isoValue > surfaceLevel)
                                            {
                                                edge0 = Utility.LerpEdge(v1X, v1Y - 1, v1Z, v1a.isoValue, v0X, v0Y - 1, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                                MatTest(v0X, v0Y - 1, v0Z, v0a);
                                            }
                                            else
                                            {
                                                edge0 = Utility.LerpEdge(v1X, v1Y - 1, v1Z, v1a.isoValue, v1X, v1Y, v1Z, v1Val.isoValue, surfaceLevel, useSmoothing);
                                            }
                                            MatTest(v1X, v1Y - 1, v1Z, v1a);
                                        }
                                        else
                                        {
                                            edge0 = Utility.LerpEdge(v1X, v1Y - 1, v1Z, 1, v0X, v0Y - 1, v0Z, 1, surfaceLevel, false);
                                        }
                                    }
                                    // Positive
                                    v0a = Utility.GetValue(chunk, v0X, v0Y + 1, v0Z);
                                    if (v0a?.isoValue > surfaceLevel)
                                    {
                                        edge1 = Utility.LerpEdge(v0X, v0Y, v0Z, v0Val.isoValue, v0X, v0Y + 1, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                        MatTest(v0X, v0Y + 1, v0Z, v0a);
                                    }
                                    else
                                    {
                                        v1a = Utility.GetValue(chunk, v1X, v1Y + 1, v1Z);
                                        if (v1a != null)
                                        {
                                            if (v1a.isoValue > surfaceLevel)
                                            {
                                                edge1 = Utility.LerpEdge(v1X, v1Y + 1, v1Z, v1a.isoValue, v0X, v0Y + 1, v0Z, v0a.isoValue, surfaceLevel, useSmoothing);
                                                MatTest(v0X, v0Y + 1, v0Z, v0a);
                                            }
                                            else
                                            {
                                                edge1 = Utility.LerpEdge(v1X, v1Y + 1, v1Z, v1a.isoValue, v1X, v1Y, v1Z, v1Val.isoValue, surfaceLevel, useSmoothing);
                                            }
                                            MatTest(v1X, v1Y + 1, v1Z, v1a);
                                        }
                                        else
                                        {
                                            edge1 = Utility.LerpEdge(v1X, v1Y + 1, v1Z, 1, v0X, v0Y + 1, v0Z, 1, surfaceLevel, false);
                                        }
                                    }
                                }
                                void GetVerticiesZ(out Vector3 edge0, out Vector3 edge1)
                                {
                                    Node v0a, v1a;
                                    // Negitive
                                    v0a = Utility.GetValue(chunk, v0X, v0Y, v0Z - 1);
                                    if (v0a?.isoValue > surfaceLevel)
                                    {
                                        edge0 = Utility.LerpEdge(v0X, v0Y, v0Z, v0Val.isoValue, v0X, v0Y, v0Z - 1, v0a.isoValue, surfaceLevel, useSmoothing);
                                        MatTest(v0X, v0Y, v0Z - 1, v0a);
                                    }
                                    else
                                    {
                                        v1a = Utility.GetValue(chunk, v1X, v1Y, v1Z - 1);
                                        if (v1a != null)
                                        {
                                            if (v1a.isoValue > surfaceLevel)
                                            {
                                                edge0 = Utility.LerpEdge(v1X, v1Y, v1Z - 1, v1a.isoValue, v0X, v0Y, v0Z - 1, v0a.isoValue, surfaceLevel, useSmoothing);
                                                MatTest(v0X, v0Y, v0Z - 1, v0a);
                                            }
                                            else
                                            {
                                                edge0 = Utility.LerpEdge(v1X, v1Y, v1Z - 1, v1a.isoValue, v1X, v1Y, v1Z, v1Val.isoValue, surfaceLevel, useSmoothing);
                                            }
                                            MatTest(v1X, v1Y, v1Z - 1, v1a);
                                        }
                                        else
                                        {
                                            edge0 = Utility.LerpEdge(v1X, v1Y, v1Z - 1, 1, v0X, v0Y, v0Z - 1, 1, surfaceLevel, false);
                                        }
                                    }
                                    // Positive
                                    v0a = Utility.GetValue(chunk, v0X, v0Y, v0Z + 1);
                                    if (v0a?.isoValue > surfaceLevel)
                                    {
                                        edge1 = Utility.LerpEdge(v0X, v0Y, v0Z, v0Val.isoValue, v0X, v0Y, v0Z + 1, v0a.isoValue, surfaceLevel, useSmoothing);
                                        MatTest(v0X, v0Y, v0Z + 1, v0a);
                                    }
                                    else
                                    {
                                        v1a = Utility.GetValue(chunk, v1X, v1Y, v1Z + 1);
                                        if (v1a != null)
                                        {
                                            if (v1a.isoValue > surfaceLevel)
                                            {
                                                edge1 = Utility.LerpEdge(v1X, v1Y, v1Z + 1, v1a.isoValue, v0X, v0Y, v0Z + 1, v0a.isoValue, surfaceLevel, useSmoothing);
                                                MatTest(v0X, v0Y, v0Z + 1, v0a);
                                            }
                                            else
                                            {
                                                edge1 = Utility.LerpEdge(v1X, v1Y, v1Z + 1, v1a.isoValue, v1X, v1Y, v1Z, v1Val.isoValue, surfaceLevel, useSmoothing);
                                            }
                                            MatTest(v1X, v1Y, v1Z + 1, v1a);
                                        }
                                        else
                                        {
                                            edge1 = Utility.LerpEdge(v1X, v1Y, v1Z + 1, 1, v1X, v1Y, v1Z + 1, 1, surfaceLevel, false);
                                        }
                                    }
                                }
                                if (v0X == v1X)
                                {
                                    GetVerticiesX(out p0, out p1);
                                }
                                if (v0Y == v1Y)
                                {
                                    if (v0X != v1X)
                                        GetVerticiesY(out p0, out p1);
                                    else
                                        GetVerticiesY(out p2, out p3);
                                }
                                if (v0Z == v1Z)
                                {
                                    GetVerticiesZ(out p2, out p3);
                                }
                                
                                // The vertex normal is the avarage face normal of the adjacent triangles
                                p0 -= vertex;
                                p1 -= vertex;
                                p2 -= vertex;
                                p3 -= vertex;
                                Vector3 normal = Vector3.Cross(p3, p1);
                                normal += Vector3.Cross(p0, p3);
                                normal += Vector3.Cross(p2, p0);
                                normal += Vector3.Cross(p1, p2);
                                normal.Normalize();
                                if (v0X > v1X || v0Y < v1Y || v0Z > v1Z)
                                    normal = -normal;
                                normals.Add(normal);
                                // Set material data
                                uvs.Add(matData);
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
                }
            }

            // Swap caches and setup for next layer
            FlatArray3D<int> temp = bottomCache;
            bottomCache = topCache;
            topCache = temp;
            for (int i = 0; i < topCache.Length; i++)
            {
                topCache[i] = -1;
            }
        }
    }

    /// <summary>
    /// Generate mesh data for a single voxel
    /// </summary>
    /// <param name="voxel">Voxel to generate mesh data for</param>
    /// <param name="verticies"></param>
    /// <param name="triangles"></param>
    /// <param name="surfaceLevel"></param>
    /// <param name="useSmoothing">Should interpolation be used?</param>
    public static void ProcessCube(Voxel voxel, List<Vector3> verticies, List<int> triangles, 
                                   float surfaceLevel, bool useSmoothing)
    {
        int cubeIndex = Utility.GetCubeIndex(voxel, surfaceLevel);
        if (LookupTables.edgeTable[cubeIndex] == 0)
            return;

        // Generate verticies for used edges and save their index
        int[] vertIndices = new int[12];
        void CacheEdge(int edgeVal)
        {
            if ((LookupTables.edgeTable[cubeIndex] & 1 << edgeVal) != 0)
            {
                vertIndices[edgeVal] = verticies.Count;
                verticies.Add(Utility.LerpEdge(voxel.GetVertex(LookupTables.edgeConnection[edgeVal, 0]),
                                               voxel.GetVertex(LookupTables.edgeConnection[edgeVal, 1]), 
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
