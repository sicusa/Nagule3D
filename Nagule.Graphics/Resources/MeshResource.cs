namespace Nagule.Graphics;

using System.Numerics;

public record MeshResource : ResourceBase
{
    public static readonly MeshResource Empty = new();

    public Vector3[]? Vertices;
    public Vector3[]? TexCoords;
    public Vector3[]? Normals;
    public Vector3[]? Tangents;
    public int[]? Indeces;
    public Rectangle BoudingBox;
    public MaterialResource? Material;

    public bool IsOccluder;
}