using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FileHandler
{
    private const string SAVE_DIR = "Data/";
    private const string SAVE_FILE_NAME = "Chunk";

    private static FileHandler instance;
    public static FileHandler Instance
    {
        get
        {
            if (instance == null)
                instance = new FileHandler();
            return instance;
        }
    }



    public void SaveChunk(in Chunk chunk)
    {
        if (chunk == null)
            return;

        string path = GetFilePath(chunk.position);
        using (FileStream fs = File.Open(path, FileMode.OpenOrCreate,FileAccess.Write))
        {
            // If the file has data, this should clear it
            if (fs.Length > 0)
            {
                fs.SetLength(0);
            }

            using(BinaryWriter bs = new BinaryWriter(fs))
            {
                for (uint i = 0; i < chunk.nodes.Length; i++)
                {
                    bs.Write(chunk.nodes[i].isoValue);
                    bs.Write((byte)chunk.nodes[i].materialID);
                }
                bs.Close();
            }
            fs.Close();
        }
    }

    public bool LoadChunk(ref Chunk chunk)
    {
        if (chunk == null)
            return false;
        if (chunk.nodes == null)
            chunk.nodes = new FlatArray3D<Node>(VoxelMap.ChunkSize, VoxelMap.ChunkSize, VoxelMap.ChunkSize);

        string path = GetFilePath(chunk.position);
        if (!File.Exists(path))
        {
            return false;
        }

        using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
        {
            using (BinaryReader bs = new BinaryReader(fs))
            {
                for (uint i = 0; i < chunk.nodes.Length; i++)
                {
                    chunk.nodes[i].isoValue = bs.ReadSingle();
                    chunk.nodes[i].materialID = (int)bs.ReadByte();
                }
                bs.Close();
            }
            fs.Close();
        }
        return true;
    }

    private string GetFilePath(Vector3Int index)
    {
        // [SAVE_FILE_NAME]_X_Y_Z.dat
        return Application.dataPath + "/" + SAVE_DIR + SAVE_FILE_NAME + "_" + index.x + "_" + index.y + "_" + index.z + ".dat";
    }
}
