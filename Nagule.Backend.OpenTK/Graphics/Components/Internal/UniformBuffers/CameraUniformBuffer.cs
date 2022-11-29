namespace Nagule.Backend.OpenTK.Graphics;

using System.Numerics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CameraParameters
{
    public static readonly int MemorySize = Marshal.SizeOf<CameraParameters>();

    public Matrix4x4 View;
    public Matrix4x4 Proj;
    public Matrix4x4 ViewProj;
    public Vector3 Position;
    public float NearPlaneDistance;
    public float FarPlaneDistance;
}

public struct CameraUniformBuffer : IPooledComponent
{
    public int Handle;
    public IntPtr Pointer;
    public CameraParameters Parameters;
}