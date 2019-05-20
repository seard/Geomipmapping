using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class ShaderMeshGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ShaderThreads
    {
        public int xy = 8;
        public int x
        {
            get { return xy; }
        }
        public int y
        {
            get { return xy; }
        }
    }

    [Header("Kernel Settings")]
    public string kernelHandle = "CSMain"; // Name of the shader kernel
    public ComputeShader shader;
    public ShaderThreads numthreads;

    [Header("Kernel Input Parameters")]
    string arrayWidthParameter = "indexingWidth";
    string resolutionMultiplier = "resolutionMultiplier";
    string heightMultiplier_name = "heightMultiplier";
    string heightMapSize_name = "heightMapSize";
    string heightMap = "heightMap";
    string topLeftParameter = "topLeft";
    string topRightParameter = "topRight";
    string bottomLeftParameter = "bottomLeft";
    string bottomRightParameter = "bottomRight";

    [Header("Kernel Output Parameters")]
    string vertexArray = "vertices";
    string normalArray = "normals";
    string uvArray = "uvs";
    string triangleArray = "triangles";

    // The integer name connected to the kernel
    private int handle;

    /// <summary>
    /// This method will return a mesh of resolution 7x7x[resolutionMultiplier]
    /// </summary>
    public Mesh BuildPatch(Vector3 position, float size, int resolutionMultiplier, Texture2D map, float heightMultiplier)
    {
        handle = shader.FindKernel(kernelHandle);

        // Call the make function and set to render a mesh
        Vector3 positionCorrection = position + (new Vector3(1.0f, 0, 1.0f) * map.width * 0.5f);

        MeshData meshData = Make(
        positionCorrection + new Vector3(-size, 0, size) * 0.5f,
        positionCorrection + new Vector3(size, 0, size) * 0.5f,
        positionCorrection + new Vector3(-size, 0, -size) * 0.5f,
        positionCorrection + new Vector3(size, 0, -size) * 0.5f,
        resolutionMultiplier,
        map,
        heightMultiplier);

        return meshData.mesh;
    }

    /// <summary>
    /// This method prepares for calling the compute shader,
    /// which will task the kernels of the GPU to run vertex-calculations simultaneously.
    /// This drastically increases the performance of vertex-generation, rather than running it on the CPU
    /// </summary>
    private MeshData Make(Vector3 topLeft, Vector3 topRight, Vector3 bottomLeft, Vector3 bottomRight, int resMult, Texture2D map, float heightMultiplier)
    {
        //Replace resolution with one matching out shader's threads
        //Resolution is number of vertices across any axis
        int resolution = (resMult > 1 ? resMult : 1) * numthreads.xy; //Ensure multiple of xy

        //Initialize values
        int width = resolution;
        int size = width * width;

        int num_vertices = size;
        int num_normals = size;
        int num_uvs = size;
        int num_triangles = ((width - 1) * (width - 1)) * 6;

        //Create Buffers
        Vector3[] vertices = new Vector3[num_vertices];
        Vector3[] normals = new Vector3[num_normals];
        Vector2[] uvs = new Vector2[num_uvs];
        int[] triangles = new int[num_triangles];

        ComputeBuffer vb = new ComputeBuffer(vertices.Length, 3 * sizeof(float)); //3 floats * 4 bytes / float
        ComputeBuffer nb = new ComputeBuffer(normals.Length, 3 * sizeof(float));
        ComputeBuffer ub = new ComputeBuffer(uvs.Length, 2 * sizeof(float));
        ComputeBuffer tb = new ComputeBuffer(triangles.Length, sizeof(int));

        //Transfer data to GPU
        shader.SetInt(arrayWidthParameter, width);
        shader.SetInt(resolutionMultiplier, resMult);
        shader.SetFloat(heightMapSize_name, map.width);
        shader.SetFloat(heightMultiplier_name, heightMultiplier);
        shader.SetTexture(handle, heightMap, map);
        shader.SetVector(topLeftParameter, topLeft);
        shader.SetVector(topRightParameter, topRight);
        shader.SetVector(bottomLeftParameter, bottomLeft);
        shader.SetVector(bottomRightParameter, bottomRight);

        shader.SetBuffer(handle, vertexArray, vb);
        shader.SetBuffer(handle, normalArray, nb);
        shader.SetBuffer(handle, uvArray, ub);
        shader.SetBuffer(handle, triangleArray, tb);

        //Dispatch the shader
        shader.Dispatch(handle, (width / numthreads.x), (width / numthreads.y), 1);

        //Retrieve data from GPU
        vb.GetData(vertices);
        nb.GetData(normals);
        ub.GetData(uvs);
        tb.GetData(triangles);

        //Dispose buffers to be cleaned up by GC
        vb.Dispose();
        nb.Dispose();
        ub.Dispose();
        tb.Dispose();

        //Create mesh
        MeshData m = new MeshData()
        {
            name = "Surface_r" + resolution,
            vertices = vertices,
            uvs = uvs,
            triangles = triangles,
            normals = normals
        };

        return m;
    }
}
