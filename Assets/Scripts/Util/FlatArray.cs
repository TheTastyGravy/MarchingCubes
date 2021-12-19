using UnityEngine;

[System.Serializable]
public class FlatArray2D<T>
{
    public FlatArray2D(Vector2Int a_size)
    {
        m_size = a_size;
        m_array = new T[a_size.x * a_size.y];
    }
    public FlatArray2D(int x, int y)
    {
        m_size = new Vector2Int(x, y);
        m_array = new T[x * y];
    }

    public T this[int x, int y]
    {
        get { return m_array[x * m_size.y + y]; }
        set { m_array[x * m_size.y + y] = value; }
    }
    public T this[Vector2Int i]
    {
        get { return m_array[i.x * m_size.y + i.y]; }
        set { m_array[i.x * m_size.y + i.y] = value; }
    }
    public T this[int i]
    {
        get { return m_array[i]; }
        set { m_array[i] = value; }
    }

    [SerializeField, HideInInspector]
    private T[] m_array;
    [SerializeField, HideInInspector]
    private Vector2Int m_size;
    public Vector2Int Size => m_size;
    public int Length => m_array.Length;
}

[System.Serializable]
public class FlatArray3D<T>
{
    public FlatArray3D(Vector3Int a_size)
    {
        m_size = a_size;
        m_array = new T[a_size.x * a_size.y * a_size.z];
    }
    public FlatArray3D(int x, int y, int z)
    {
        m_size = new Vector3Int(x, y, z);
        m_array = new T[x * y * z];
    }

    public T this[int x, int y, int z]
    {
        get { return m_array[(x * m_size.y + y) * m_size.z + z]; }
        set { m_array[(x * m_size.y + y) * m_size.z + z] = value; }
    }
    public T this[Vector3Int i]
    {
        get { return m_array[(i.x * m_size.y + i.y) * m_size.z + i.z]; }
        set { m_array[(i.x * m_size.y + i.y) * m_size.z + i.z] = value; }
    }
    public T this[int i]
    {
        get { return m_array[i]; }
        set { m_array[i] = value; }
    }

    [SerializeField, HideInInspector]
    private T[] m_array;
    [SerializeField, HideInInspector]
    private Vector3Int m_size;
    public Vector3Int Size => m_size;
    public int Length => m_array.Length;
}