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
    public T this[uint x, uint y, uint z]
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
    public T this[uint i]
    {
        get { return m_array[i]; }
        set { m_array[i] = value; }
    }

    /// <summary>
    /// Copy all values from the target to this array. If the arrays are of different size, only the values within range will be used
    /// </summary>
    /// <param name="target">Array to copy values from</param>
    public void CopyValuesFrom(FlatArray3D<T> target)
    {
        for (uint x = 0; x < m_size.x && x < target.Size.x; x++)
        {
            for (uint y = 0; y < m_size.y && y < target.Size.y; y++)
            {
                for (uint z = 0; z < m_size.z && z < target.Size.z; z++)
                {
                    m_array[(x * m_size.y + y) * m_size.z + z] = target[x, y, z];
                }
            }
        }
    }

    [SerializeField, HideInInspector]
    private T[] m_array;
    [SerializeField, HideInInspector]
    private Vector3Int m_size;
    public Vector3Int Size => m_size;
    public int Length => m_array.Length;
}