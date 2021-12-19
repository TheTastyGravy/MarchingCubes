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
}
