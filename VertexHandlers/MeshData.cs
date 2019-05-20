using UnityEngine;

/// <summary>
/// Container for the vertex information needed to render a full mesh
/// </summary>
public class MeshData
{
    public string name;
    public Vector3[] vertices;
    public Vector3[] normals;
    public Vector2[] uvs;
    public int[] triangles;

    public Mesh mesh
    {
        get
        {
            return new Mesh()
            {
                name = this.name,
                vertices = this.vertices,
                uv = this.uvs,
                triangles = this.triangles,
                normals = this.normals
            };
        }
    }
}