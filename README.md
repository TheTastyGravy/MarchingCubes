# Marching Cubes
A marching cubes terrain system on the GPU. The system has gone through many itterations and changes, and is incomplete (although functional) in its current state. Unity version 2021.2.16f1.

## Features
- Marching cubes on the GPU using compute shaders
- Custom optimised raycast using the [fast voxel traversal algorithm](https://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.42.3443&rep=rep1&type=pdf)
- Realtime terrain editing
- Saving and loading chunks with .dat files
- Custom materials using barycenter coordinates
- Triplanar mapping

Mesh generation is done by passing the mesh's vertex and index buffers into the marching cubes compute shader. This means the resulting mesh data stays on the GPU instead of being passed back to the CPU. The consequence of this is that mesh data is lost when the scene is reloaded (eg entering and exiting play mode), and must be rebuilt.

## Usage
The system consists of three important scripts:
- `WorldManager` is used to control VoxelMap, and has options for map size, building meshes, setting chunk states, saving and loading, etc.
- `ChunkGenerator` simply provides the ability to generate data for new chunks.
- `VoxelMap` is the main script, and contains all the functionality for managing chunks, building meshes, and raycasting.

Also of note are:
- `GenerateMesh.compute` is the compute shader used for mesh generation.
- `FileHandler` is used to read and write chunk data to files.

### Custom Raycast
The custom raycast function will only check for intersections with tris inside the voxels the ray passes through. The fast voxel traversal algorithm is used to itterate over each voxel, and a simple marching cubes function is used to generate its tris. A ray-triangle intersection is tested for each tri until a collision is found, or the end of the map is reached.

### Custom Materials
Each node stores a material index coresponding to the `customMaterialTextures` array (0 is no material, 1 is the first element, etc). Durring mesh generation, each vertex of a tri has UV0 set to its barycenter coordinate, while UV1 contains the material index for each vertex (ie x = v0, y = v1, z = v2). In the fragment shader, this results in UV0 being the interpolated barycenter coordinate, and UV1 remaining constant as all three verticies have the same value, and can be used to index a texture array.
