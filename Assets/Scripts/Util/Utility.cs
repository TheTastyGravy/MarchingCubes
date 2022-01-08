using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Node
{
    public float isoValue;
    public int materialID;
}

[System.Serializable]
public class Chunk
{
    public FlatArray3D<Node> nodes;
    public VoxelMap map;
    // Used as an index in 3D space
    public Vector3Int position;

    public GameObject meshObject;
    public MeshFilter meshFilter;
    public Mesh mesh;
}

public class Voxel
{
    public Voxel()
    {
        m_verticies = new Node[8];
    }

    /// <summary>
    /// Setup/Reset the voxel for new values
    /// </summary>
    /// <param name="baseIndex">The origin index for the voxel</param>
    public void Setup(Vector3Int baseIndex)
    {
        m_baseIndex = baseIndex;
        for (int i = 0; i < 8; i++)
            m_verticies[i] = null;
    }

    public Node this[int i]
    {
        get { return m_verticies[i]; }
        set { m_verticies[i] = value; }
    }

    /// <summary>
    /// Get the index for vertex i
    /// </summary>
    public Vector3Int Pos(int i)
    {
        if (i < 0 || i > 7)
            return Vector3Int.zero;
        Vector3Int res = m_baseIndex;
        if (i >= 4)
        {
            res.y += 1;
            i %= 4;
        }
        if (i < 2)
            res.z += 1;
        if (i == 1 || i == 2)
            res.x += 1;
        return res;
    }

    /// <summary>
    /// Get vertex i as a Vector4. [x, y, z, iso]
    /// </summary>
    public Vector4 GetVertex(int i)
    {
        Vector3Int pos = Pos(i);
        return new Vector4(pos.x, pos.y, pos.z, this[i].isoValue);
    }

    private Vector3Int m_baseIndex;
    private Node[] m_verticies;
}

public class Utility
{
    public static int GetCubeIndex(Voxel cube, float surfaceLevel)
    {
        int cubeIndex = 0;
        if (cube[0].isoValue <= surfaceLevel) cubeIndex |= 1;
        if (cube[1].isoValue <= surfaceLevel) cubeIndex |= 2;
        if (cube[2].isoValue <= surfaceLevel) cubeIndex |= 4;
        if (cube[3].isoValue <= surfaceLevel) cubeIndex |= 8;
        if (cube[4].isoValue <= surfaceLevel) cubeIndex |= 16;
        if (cube[5].isoValue <= surfaceLevel) cubeIndex |= 32;
        if (cube[6].isoValue <= surfaceLevel) cubeIndex |= 64;
        if (cube[7].isoValue <= surfaceLevel) cubeIndex |= 128;
        return cubeIndex;
    }

    /// <summary>
    /// Interpolate to find the edge vertex 
    /// </summary>
    /// <param name="v1">The first corner</param>
    /// <param name="v2">The secon corner</param>
    /// <returns>The position of the edge vertex</returns>
    public static Vector3 LerpEdge(Vector4 v1, Vector4 v2, float surfaceLevel, bool useSmoothing)
    {
        float t = useSmoothing ? (surfaceLevel - v1.w) / (v2.w - v1.w) : 0.5f;
        return v1 + (v2 - v1) * t;
    }

    /// <summary>
    /// Get the node at [x, y, z] relitive to chunk. If the index is outside the chunk, its neighbours will be used
    /// </summary>
    /// <returns>The node at the index, or null if it doesnt exist</returns>
    public static Node GetValue(Chunk chunk, int x, int y, int z)
    {
        Vector3Int size = chunk.nodes.Size;
        if (x >= 0 && y >= 0 && z >= 0 && x < size.x && y < size.y && z < size.z)
            return chunk.nodes[x, y, z];

        Vector3Int chunkIndex = Vector3Int.zero;
        if (x >= size.x)
        {
            chunkIndex.x = 1;
            x -= size.x;
        }
        if (y >= size.y)
        {
            chunkIndex.y = 1;
            y -= size.y;
        }
        if (z >= size.z)
        {
            chunkIndex.z = 1;
            z -= size.z;
        }
        if (x < 0)
        {
            chunkIndex.x = -1;
            x += size.x;
        }
        if (y < 0)
        {
            chunkIndex.y = -1;
            y += size.y;
        }
        if (z < 0)
        {
            chunkIndex.z = -1;
            z += size.z;
        }
        
        // Use the neighbour chunk
        Chunk current = chunk.map.GetChunk(chunk.position + chunkIndex);
        if (current == null)
            return null;
        else
            return current.nodes[x, y, z];
    }

    /// <summary>
    /// Ray-triangle intersection test using the Möller–Trumbore intersection algorithm
    /// </summary>
    /// <param name="triangle">Array of 3 verticies</param>
    /// <param name="intersect">The point that the ray hits the triangle. Zero if it misses</param>
    /// <returns>Has the ray hit the triangle?</returns>
    public static bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDirection, Vector3[] triangle, out Vector3 intersect)
    {
        const float EPSILON = 0.0000001f;
        Vector3 vertex0 = triangle[0];
        Vector3 vertex1 = triangle[1];
        Vector3 vertex2 = triangle[2];

        Vector3 edge1 = vertex1 - vertex0;
        Vector3 edge2 = vertex2 - vertex0;
        Vector3 h = Vector3.Cross(rayDirection, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -EPSILON && a < EPSILON)
        {
            intersect = Vector3.zero;
            return false;    // This ray is parallel to this triangle.
        }

        float f = 1f / a;
        Vector3 s = rayOrigin - vertex0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0 || u > 1.0)
        {
            intersect = Vector3.zero;
            return false;
        }
        
        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDirection, q);

        if (v < 0f || u + v > 1f)
        {
            intersect = Vector3.zero;
            return false;
        }
        
        // At this stage we can compute t to find out where the intersection point is on the line.
        float t = f * Vector3.Dot(edge2, q);
        
        if (t > EPSILON) // ray intersection
        {
            intersect = rayOrigin + rayDirection * t;
            return true;
        }
        else // This means that there is a line intersection but not a ray intersection.
        {
            intersect = Vector3.zero;
            return false;
        }
    }

    /// <summary>
    /// Wrapper for Debug.LogWarning with additional formatting
    /// </summary>
    internal static void PrintWarning(string message)
    {
        Debug.LogWarning("MARCHING_CUBES: " + message);
    }
}
