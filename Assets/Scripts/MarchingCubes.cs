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
        Vector3Int size = chunk.nodes.Size;
        bool isCubeValid;
        Voxel voxel = new Voxel();
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
            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
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
                            if (cache[voxel.Pos(primaryCorner).x, voxel.Pos(primaryCorner).z, edgeType] != -1)
                            {
                                vertIndices[edgeValue] = cache[voxel.Pos(primaryCorner).x, voxel.Pos(primaryCorner).z, edgeType];
                            }
                            else
                            {
                                // Create new vertex
                                vertIndices[edgeValue] = verticies.Count;
                                cache[voxel.Pos(primaryCorner).x, voxel.Pos(primaryCorner).z, edgeType] = vertIndices[edgeValue];
                                Vector3 vertex = Utility.LerpEdge(voxel.GetVertex(LookupTables.edgeConnection[edgeValue, 0]),
                                                                  voxel.GetVertex(LookupTables.edgeConnection[edgeValue, 1]),
                                                                  surfaceLevel, useSmoothing);
                                verticies.Add(vertex);
                                // Get initial material data. CreateEdgeVertex will update the data if the vertex has a material.
                                Node node0 = voxel[LookupTables.edgeConnection[edgeValue, 0]];
                                Node node1 = voxel[LookupTables.edgeConnection[edgeValue, 1]];
                                Vector2 matData = new Vector2(Mathf.Max(node0.materialID, node1.materialID), (surfaceLevel - node0.isoValue) / (node1.isoValue - node0.isoValue));

                                // Calculate the vertex normal using data from the surounding verticies. The algorithm is described here:
                                // https://www.researchgate.net/publication/220944287_Approximating_Normals_for_Marching_Cubes_applied_to_Locally_Supported_Isosurfaces
                                Vector3Int v0 = voxel.Pos(LookupTables.edgeConnection[edgeValue, 0]);
                                Vector3Int v1 = voxel.Pos(LookupTables.edgeConnection[edgeValue, 1]);
                                if (voxel[LookupTables.edgeConnection[edgeValue, 0]].isoValue > surfaceLevel)
                                {
                                    Vector3Int temp = v1;
                                    v1 = v0;
                                    v0 = temp;
                                }

                                // Get adjacent edge verticies
                                Vector3 p0 = Vector3.zero, p1 = Vector3.zero, p2 = Vector3.zero, p3 = Vector3.zero;
                                Vector4 CreateEdgeVertex(int x, int y, int z)
                                {
                                    Node value = Utility.GetValue(chunk, x, y, z);
                                    if (value != null)
                                    {
                                        // If this node has a material, update the data for the vertex
                                        if (value.materialID > matData.x)
                                        {
                                            matData.x = value.materialID;
                                            matData.y = value.isoValue;
                                        }
                                        return new Vector4(x, y, z, value.isoValue);
                                    }
                                    else
                                        return new Vector4(x, y, z, 0.5f);
                                }
                                void GetVerticies(Vector3Int a, ref Vector3 edge0, ref Vector3 edge1)
                                {
                                    // Negitive
                                    if (Utility.GetValue(chunk, v0.x-a.x, v0.y-a.y, v0.z-a.z)?.isoValue > surfaceLevel)
                                    {
                                        edge0 = Utility.LerpEdge(CreateEdgeVertex(v0.x, v0.y, v0.z), CreateEdgeVertex(v0.x-a.x, v0.y-a.y, v0.z-a.z), surfaceLevel, useSmoothing);
                                    }
                                    else
                                    {
                                        if (Utility.GetValue(chunk, v1.x-a.x, v1.y-a.y, v1.z-a.z)?.isoValue > surfaceLevel)
                                            edge0 = Utility.LerpEdge(CreateEdgeVertex(v1.x-a.x, v1.y-a.y, v1.z-a.z), CreateEdgeVertex(v0.x-a.x, v0.y-a.y, v0.z-a.z), surfaceLevel, useSmoothing);
                                        else
                                            edge0 = Utility.LerpEdge(CreateEdgeVertex(v1.x-a.x, v1.y-a.y, v1.z-a.z), CreateEdgeVertex(v1.x, v1.y, v1.z), surfaceLevel, useSmoothing);
                                    }
                                    // Positive
                                    if (Utility.GetValue(chunk, v0.x+a.x, v0.y+a.y, v0.z+a.z)?.isoValue > surfaceLevel)
                                    {
                                        edge1 = Utility.LerpEdge(CreateEdgeVertex(v0.x, v0.y, v0.z), CreateEdgeVertex(v0.x+a.x, v0.y+a.y, v0.z+a.z), surfaceLevel, useSmoothing);
                                    }
                                    else
                                    {
                                        if (Utility.GetValue(chunk, v1.x+a.x, v1.y+a.y, v1.z+a.z)?.isoValue > surfaceLevel)
                                            edge1 = Utility.LerpEdge(CreateEdgeVertex(v1.x+a.x, v1.y+a.y, v1.z+a.z), CreateEdgeVertex(v0.x+a.x, v0.y+a.y, v0.z+a.z), surfaceLevel, useSmoothing);
                                        else
                                            edge1 = Utility.LerpEdge(CreateEdgeVertex(v1.x+a.x, v1.y+a.y, v1.z+a.z), CreateEdgeVertex(v1.x, v1.y, v1.z), surfaceLevel, useSmoothing);
                                    }
                                }
                                if (v0.x == v1.x)
                                {
                                    GetVerticies(Vector3Int.right, ref p0, ref p1);
                                }
                                if (v0.y == v1.y)
                                {
                                    if (v0.x != v1.x)
                                        GetVerticies(Vector3Int.up, ref p0, ref p1);
                                    else
                                        GetVerticies(Vector3Int.up, ref p2, ref p3);
                                }
                                if (v0.z == v1.z)
                                {
                                    GetVerticies(Vector3Int.forward, ref p2, ref p3);
                                }

                                // The vertex normal is the avarage face normal of the adjacent triangles
                                Vector3 normal = Vector3.Cross(p3 - vertex, p1 - vertex).normalized;
                                normal += Vector3.Cross(p0 - vertex, p3 - vertex).normalized;
                                normal += Vector3.Cross(p2 - vertex, p0 - vertex).normalized;
                                normal += Vector3.Cross(p1 - vertex, p2 - vertex).normalized;
                                normal.Normalize();
                                if (v0.x > v1.x || v0.y < v1.y || v0.z > v1.z)
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
            // Setup cache for next layer
            bottomCache.CopyValuesFrom(topCache);
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
