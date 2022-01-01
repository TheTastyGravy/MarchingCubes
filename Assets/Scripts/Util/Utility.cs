using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Node
{
    public float isoValue;
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

public class Utility
{
    public static int GetCubeIndex(in Vector4[] cube, float surfaceLevel)
    {
        int cubeIndex = 0;
        if (cube[0].w <= surfaceLevel) cubeIndex |= 1;
        if (cube[1].w <= surfaceLevel) cubeIndex |= 2;
        if (cube[2].w <= surfaceLevel) cubeIndex |= 4;
        if (cube[3].w <= surfaceLevel) cubeIndex |= 8;
        if (cube[4].w <= surfaceLevel) cubeIndex |= 16;
        if (cube[5].w <= surfaceLevel) cubeIndex |= 32;
        if (cube[6].w <= surfaceLevel) cubeIndex |= 64;
        if (cube[7].w <= surfaceLevel) cubeIndex |= 128;
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
