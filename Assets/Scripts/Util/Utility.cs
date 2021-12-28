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
    /// Wrapper for Debug.LogWarning with additional formatting
    /// </summary>
    internal static void PrintWarning(string message)
    {
        Debug.LogWarning("MARCHING_CUBES: " + message);
    }
}
