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
        m_sizeX = a_size.x;
        m_sizeY = a_size.y;
        m_sizeZ = a_size.z;
        m_array = new T[m_sizeX * m_sizeY * m_sizeZ];
    }
    public FlatArray3D(int x, int y, int z)
    {
        m_sizeX = x;
        m_sizeY = y;
        m_sizeZ = z;
        m_array = new T[m_sizeX * m_sizeY * m_sizeZ];
    }

    public T this[int x, int y, int z]
    {
        get => m_array[(x * m_sizeY + y) * m_sizeZ + z];
        set => m_array[(x * m_sizeY + y) * m_sizeZ + z] = value;
    }
    public T this[uint x, uint y, uint z]
    {
        get => m_array[(x * m_sizeY + y) * m_sizeZ + z];
        set => m_array[(x * m_sizeY + y) * m_sizeZ + z] = value;
    }
    public T this[Vector3Int i]
    {
        get => m_array[(i.x * m_sizeY + i.y) * m_sizeZ + i.z];
        set => m_array[(i.x * m_sizeY + i.y) * m_sizeZ + i.z] = value;
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
        for (uint x = 0; x < m_sizeX && x < target.SizeX; x++)
        {
            for (uint y = 0; y < m_sizeY && y < target.SizeY; y++)
            {
                for (uint z = 0; z < m_sizeZ && z < target.SizeZ; z++)
                {
                    m_array[(x * m_sizeY + y) * m_sizeZ + z] = target[x, y, z];
                }
            }
        }
    }

    [SerializeField, HideInInspector]
    private T[] m_array;
    [SerializeField, HideInInspector]
    private int m_sizeX;
    [SerializeField, HideInInspector]
    private int m_sizeY;
    [SerializeField, HideInInspector]
    private int m_sizeZ;
    public int SizeX => m_sizeX;
    public int SizeY => m_sizeY;
    public int SizeZ => m_sizeZ;
    public Vector3Int Size => new Vector3Int(m_sizeX, m_sizeY, m_sizeZ);
    public int Length => m_array.Length;
}