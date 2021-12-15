using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NodeMap : MonoBehaviour
{
    public Node[,,] nodes;

    public List<Vector3> verticies = new List<Vector3>();
    public List<int> indicies = new List<int>();

    public int mapSize = 16;

    public MeshFilter meshFilter;
    private Mesh mesh;


    public bool generate = false;
    public float surface = 0.5f;
    public float noiseSize = 1;
    public bool smooth;



    void Start()
    {
        mesh = new Mesh();
        mesh.name = "mesh";
        meshFilter.mesh = mesh;
    }


    private void OnValidate()
    {
        if (generate)
        {
            generate = false;
            Func();
        }
    }

    void Func()
    {
        verticies.Clear();
        indicies.Clear();

        GenerateNodes();

        MarchingCubes.GenerateFullMap(this, surface, smooth);

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "mesh";
            meshFilter.mesh = mesh;
        }

        mesh.Clear();
        mesh.SetVertices(verticies);
        mesh.SetTriangles(indicies, 0);
        mesh.RecalculateNormals();
    }

    //TODO: replace this with layered noise
    public static float PerlinNoise3D(float x, float y, float z)
    {
        float xy = Mathf.PerlinNoise(x, y);
        float xz = Mathf.PerlinNoise(x, z);
        float yz = Mathf.PerlinNoise(y, z);
        float yx = Mathf.PerlinNoise(y, x);
        float zx = Mathf.PerlinNoise(z, x);
        float zy = Mathf.PerlinNoise(z, y);

        return (xy + xz + yz + yx + zx + zy) / 6;
    }

    private void GenerateNodes()
    {
        nodes = new Node[mapSize, mapSize, mapSize];

        float xOff = Random.Range(0f, 100f);
        float yOff = Random.Range(0f, 100f);
        float zOff = Random.Range(0f, 100f);

        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                for (int z = 0; z < nodes.GetLength(2); z++)
                {
                    nodes[x, y, z] = new Node();
                    nodes[x, y, z].isoValue = PerlinNoise3D(x * noiseSize + xOff, y * noiseSize + yOff, z * noiseSize + zOff);
                }
            }
        }
    }
}

#if UNITY_EDITOR
//[CustomEditor(typeof(NodeMap))]
//public class NodeMapEditor : Editor
//{
//    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
//    public static void DrawPoints(NodeMap target, GizmoType gizmoType)
//    {
//        if (target.nodes == null)
//            return;
//
//        for (int x = 0; x < target.nodes.GetLength(0); x++)
//        {
//            for (int y = 0; y < target.nodes.GetLength(1); y++)
//            {
//                for (int z = 0; z < target.nodes.GetLength(2); z++)
//                {
//                    Gizmos.DrawIcon(new Vector3(x, y, z), "DotFill.tif", true, new Color(target.nodes[x, y, z].isoValue, target.nodes[x, y, z].isoValue, target.nodes[x, y, z].isoValue));
//                }
//            }
//        }
//    }
//}
#endif //UNITY_EDITOR
