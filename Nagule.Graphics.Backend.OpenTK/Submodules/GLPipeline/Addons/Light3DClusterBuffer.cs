namespace Nagule.Graphics.Backend.OpenTK;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance.Buffers;
using Sia;

[StructLayout(LayoutKind.Sequential)]
public struct Light3DClustersParameters
{
    public const int MaximumGlobalLightCount = 8;
    public const int ClusterCountX = 16;
    public const int ClusterCountY = 9;
    public const int ClusterCountZ = 24;
    public const int ClusterCount = ClusterCountX * ClusterCountY * ClusterCountZ;
    public const int MaximumClusterLightCount = 1024;
    public const int MaximumActiveLightCount = ClusterCount * MaximumClusterLightCount;

    public float ClusterDepthSliceMultiplier;
    public float ClusterDepthSliceSubstractor;

    public int GlobalLightCount;
}

public class Light3DClustersBuffer : IAddon
{
    public long CameraParametersVersion { get; private set; }

    public BufferHandle Handle { get; private set; }
    public IntPtr Pointer { get; private set; }

    public BufferHandle ClustersHandle { get; private set; }
    public TextureHandle ClustersTexHandle { get; private set; }

    public BufferHandle ClusterLightCountsHandle { get; private set; }
    public TextureHandle ClusterLightCountsTexHandle { get; private set; }

    private IntPtr _clustersPointer;
    private readonly int[] _clusterLightCounts = new int[Light3DClustersParameters.ClusterCount];
    private readonly ExtendedRectangle[] _clusterBoundingBoxes = new ExtendedRectangle[Light3DClustersParameters.ClusterCount];

    private readonly int[] _globalLightIndices = new int[4 * Light3DClustersParameters.MaximumGlobalLightCount];
    private Light3DClustersParameters _params;
    private int _localLightCount;

    [AllowNull] private Light3DLibrary _lib;
    [AllowNull] private IEntityQuery _lightStatesQuery;

    public void Load(World world, in Camera3DState cameraState)
    {
        _lib = world.GetAddon<Light3DLibrary>();
        _lightStatesQuery = world.Query<TypeUnion<Light3DState>>();

        Handle = new(GL.GenBuffer());
        GL.BindBuffer(BufferTargetARB.UniformBuffer, Handle.Handle);
        GL.BindBufferBase(BufferTargetARB.UniformBuffer, (int)UniformBlockBinding.LightClusters, Handle.Handle);

        Pointer = GLUtils.InitializeBuffer(
            BufferTargetARB.UniformBuffer, 16 + 4 * Light3DClustersParameters.MaximumGlobalLightCount);

        Update(cameraState);
        
        // initialize texture buffer of clusters

        ClustersHandle = new(GL.GenBuffer());
        GL.BindBuffer(BufferTargetARB.TextureBuffer, ClustersHandle.Handle);
        _clustersPointer = GLUtils.InitializeBuffer(
            BufferTargetARB.TextureBuffer, 4 * Light3DClustersParameters.MaximumActiveLightCount);

        ClustersTexHandle = new(GL.GenTexture());
        GL.BindTexture(TextureTarget.TextureBuffer, ClustersTexHandle.Handle);
        GL.TexBuffer(TextureTarget.TextureBuffer, SizedInternalFormat.R32ui, ClustersHandle.Handle);

        // initialize texture buffer of cluster light counts

        ClusterLightCountsHandle = new(GL.GenBuffer());
        GL.BindBuffer(BufferTargetARB.TextureBuffer, ClusterLightCountsHandle.Handle);

        ClusterLightCountsTexHandle = new(GL.GenTexture());
        GL.BindTexture(TextureTarget.TextureBuffer, ClusterLightCountsTexHandle.Handle);
        GL.TexBuffer(TextureTarget.TextureBuffer, SizedInternalFormat.R32ui, ClusterLightCountsHandle.Handle);

        GL.BindBuffer(BufferTargetARB.UniformBuffer, 0);
        GL.BindTexture(TextureTarget.TextureBuffer, 0);
    }

    public void Update(in Camera3DState cameraState)
    {
        CameraParametersVersion = cameraState.ParametersVersion;
        UpdateClusterBoundingBoxes(cameraState);
        UpdateClusterParameters(cameraState);
    }

    private void UpdateClusterBoundingBoxes(in Camera3DState cameraState)
    {
        const int countX = Light3DClustersParameters.ClusterCountX;
        const int countY = Light3DClustersParameters.ClusterCountY;
        const int countZ = Light3DClustersParameters.ClusterCountZ;

        const float floatCountX = countX;
        const float floatCountY = countY;
        const float floatCountZ = countZ;
    
        static Vector3 ScreenToView(Vector3 texCoord, in Matrix4x4 invProj)
        {
            var clip = new Vector4(texCoord.X * 2 - 1, texCoord.Y * 2 - 1, texCoord.Z, 1);
            var view = Vector4.Transform(clip, invProj);
            return new Vector3(view.X, view.Y, view.Z) / view.W;
        }

        ref readonly var cameraParams = ref cameraState.Parameters;
        var projInv = cameraParams.ProjInv;
        var nearPlaneDis = cameraParams.NearPlaneDistance;
        var farPlaneDis = cameraParams.FarPlaneDistance;
        var planeRatio = farPlaneDis / nearPlaneDis;

        Partitioner.Create(0, countX * countY).AsParallel().ForAll(range => {
            for (int i = range.Item1; i != range.Item2; ++i) {
                int x = i % countX;
                int y = i / countX;

                var minPoint = ScreenToView(new Vector3(x / floatCountX, y / floatCountY, -1), projInv);
                var maxPoint = ScreenToView(new Vector3((x + 1) / floatCountX, (y + 1) / floatCountY, -1), projInv);

                minPoint /= minPoint.Z;
                maxPoint /= maxPoint.Z;

                for (int z = 0; z < countZ; ++z) {
                    float tileNear = -nearPlaneDis * MathF.Pow(planeRatio, z / floatCountZ);
                    float tileFar = -farPlaneDis * MathF.Pow(planeRatio, (z + 1) / floatCountZ);

                    var minPointNear = minPoint * tileNear;
                    var minPointFar = minPoint * tileFar;
                    var maxPointNear = maxPoint * tileNear;
                    var maxPointFar = maxPoint * tileFar;

                    ref var rect = ref _clusterBoundingBoxes[x + countX * y + countX * countY * z];
                    rect.Min = Vector3.Min(Vector3.Min(minPointNear, minPointFar), Vector3.Min(maxPointNear, maxPointFar));
                    rect.Max = Vector3.Max(Vector3.Max(minPointNear, minPointFar), Vector3.Max(maxPointNear, maxPointFar));
                    rect.UpdateExtents();
                }
            }
        });
    }

    private unsafe void UpdateClusterParameters(in Camera3DState cameraState)
    {
        ref readonly var cameraParams = ref cameraState.Parameters;

        float factor = Light3DClustersParameters.ClusterCountZ /
            MathF.Log2(cameraParams.FarPlaneDistance / cameraParams.NearPlaneDistance);
        float subtractor = MathF.Log2(cameraParams.NearPlaneDistance) * factor;

        float* envPtr = (float*)Pointer;
        *envPtr = factor;
        *(envPtr + 1) = subtractor;

        _params.ClusterDepthSliceMultiplier = factor;
        _params.ClusterDepthSliceSubstractor = subtractor;
    }

    public unsafe void CullLights(Camera3DState camera3DState)
    {
        var lightCount = _lightStatesQuery.Count;
        if (lightCount == 0) {
            return;
        }

        var mem = MemoryOwner<Light3DState>.Allocate(lightCount);
        var span = mem.Span;

        int i = 0;
        _lightStatesQuery.ForEach(mem, (mem, entity) => {
            mem.Span[i] = entity.Get<Light3DState>();
            i++;
        });

        Partitioner.Create(0, lightCount).AsParallel().ForAll(range => {
            for (int i = range.Item1; i != range.Item2; ++i) {
                CullLight(camera3DState, mem.Span[i]);
            }
        });

        mem.Dispose();

        ref var globalLightCount = ref _params.GlobalLightCount;
        *(int*)(Pointer + 8) = globalLightCount;
        Marshal.Copy(_globalLightIndices, 0, Pointer + 16, 4 * globalLightCount);
        globalLightCount = 0;

        if (_localLightCount != 0) {
            GL.BindBuffer(BufferTargetARB.TextureBuffer, ClusterLightCountsHandle.Handle);
            GL.BufferData(BufferTargetARB.TextureBuffer, _clusterLightCounts, BufferUsageARB.StreamDraw);
            
            Array.Clear(_clusterLightCounts);
            _localLightCount = 0;
        }
    }

    private unsafe void CullLight(in Camera3DState cameraState, in Light3DState lightState)
    {
        const int countX = Light3DClustersParameters.ClusterCountX;
        const int countY = Light3DClustersParameters.ClusterCountY;
        const int maxGlobalLightCount = Light3DClustersParameters.MaximumGlobalLightCount;
        const int maxClusterLightCount = Light3DClustersParameters.MaximumClusterLightCount;

        var lightIndex = lightState.Index;
        ref readonly var lightPars = ref _lib.Parameters[lightIndex];

        float range = lightPars.Range;
        if (range == float.PositiveInfinity) {
            var count = Interlocked.Increment(ref _params.GlobalLightCount) - 1;
            if (count < maxGlobalLightCount) {
                _globalLightIndices[count * 4] = lightIndex;
            }
            else {
                _params.GlobalLightCount = maxGlobalLightCount;
            }
            return;
        }

        ref readonly var view = ref cameraState.Parameters.View;
        ref readonly var proj = ref cameraState.Parameters.Proj;
        var viewPos = Vector4.Transform(lightPars.Position, view);

        // culled by depth
        if (viewPos.Z < -cameraState.Parameters.FarPlaneDistance - range
                || viewPos.Z > -cameraState.Parameters.NearPlaneDistance + range) {
            return;
        }

        var rangeVec = new Vector4(range, range, 0, 0);
        var bottomLeft = TransformToScreen(viewPos - rangeVec, proj);
        var topRight = TransformToScreen(viewPos + rangeVec, proj);

        var screenMin = Vector2.Min(bottomLeft, topRight);
        var screenMax = Vector2.Max(bottomLeft, topRight);

        // out of screen
        if (screenMin.X > 1 || screenMax.X < -1 || screenMin.Y > 1 || screenMax.Y < -1) {
            return;
        }

        Interlocked.Increment(ref _localLightCount);

        int minX = (int)(Math.Clamp((screenMin.X + 1) / 2, 0, 1) * countX);
        int maxX = (int)MathF.Ceiling(Math.Clamp((screenMax.X + 1) / 2, 0, 1) * countX);
        int minY = (int)(Math.Clamp((screenMin.Y + 1) / 2, 0, 1) * countY);
        int maxY = (int)MathF.Ceiling(Math.Clamp((screenMax.Y + 1) / 2, 0, 1) * countY);
        int minZ = CalculateClusterDepthSlice(Math.Max(0, -viewPos.Z - range));
        int maxZ = CalculateClusterDepthSlice(-viewPos.Z + range);

        var centerPoint = new Vector3(viewPos.X, viewPos.Y, viewPos.Z);
        float rangeSq = range * range;
        int* clusters = (int*)_clustersPointer;

        var type = lightState.Type;
        if (type == LightType.Spot) {
            var spotViewPos = Vector3.Transform(lightPars.Position, view);
            var spotViewDir = Vector3.Normalize(Vector3.TransformNormal(lightPars.Direction, view));

            for (int z = minZ; z <= maxZ; ++z) {
                for (int y = minY; y < maxY; ++y) {
                    for (int x = minX; x < maxX; ++x) {
                        int index = x + countX * y + countX * countY * z;
                        if (!IntersectConeWithSphere(
                                spotViewPos, spotViewDir, range, lightPars.OuterConeAngle,
                                _clusterBoundingBoxes[index].Middle, _clusterBoundingBoxes[index].Radius)) {
                            continue;
                        }
                        var lightCount = Interlocked.Increment(ref _clusterLightCounts[index]) - 1;
                        if (lightCount >= maxClusterLightCount) {
                            _clusterLightCounts[index] = maxClusterLightCount;
                            continue;
                        }
                        clusters[index * maxClusterLightCount + lightCount] = lightIndex;
                    }
                }
            }
        }
        else {
            for (int z = minZ; z <= maxZ; ++z) {
                for (int y = minY; y < maxY; ++y) {
                    for (int x = minX; x < maxX; ++x) {
                        int index = x + countX * y + countX * countY * z;
                        if (rangeSq < _clusterBoundingBoxes[index].DistanceToPointSquared(centerPoint)) {
                            continue;
                        }
                        var lightCount = Interlocked.Increment(ref _clusterLightCounts[index]) - 1;
                        if (lightCount >= maxClusterLightCount) {
                            _clusterLightCounts[index] = maxClusterLightCount;
                            continue;
                        }
                        clusters[index * maxClusterLightCount + lightCount] = lightIndex;
                    }
                }
            }
        }
    }

    private int CalculateClusterDepthSlice(float z)
        => Math.Clamp((int)(MathF.Log2(z) * _params.ClusterDepthSliceMultiplier - _params.ClusterDepthSliceSubstractor), 0,
            Light3DClustersParameters.ClusterCountZ - 1);

    private static Vector2 TransformToScreen(Vector4 vec, in Matrix4x4 proj)
    {
        var res = Vector4.Transform(vec, proj);
        return new Vector2(res.X, res.Y) / res.W;
    }

    public static bool IntersectConeWithPlane(Vector3 origin, Vector3 forward, float size, float angle, Vector3 plane, float planeOffset)
    {
        var v1 = Vector3.Cross(plane, forward);
        var v2 = Vector3.Cross(v1, forward);
        var capRimPoint = origin + size * MathF.Cos(angle) * forward + size * MathF.Sin(angle) * v2;
        return Vector3.Dot(capRimPoint, plane) + planeOffset >= 0.0f || Vector3.Dot(origin, plane) + planeOffset >= 0.0f;
    }

    public static bool IntersectConeWithSphere(Vector3 origin, Vector3 forward, float size, float angle, Vector3 sphereOrigin, float sphereRadius)
    {
        Vector3 v = sphereOrigin - origin;
        float vlenSq = Vector3.Dot(v, v);
        float v1len = Vector3.Dot(v, forward);
        float distanceClosestPoint = MathF.Cos(angle) * MathF.Sqrt(vlenSq - v1len * v1len) - v1len * MathF.Sin(angle);
    
        bool angleCull = distanceClosestPoint > sphereRadius;
        bool frontCull = v1len > sphereRadius + size;
        bool backCull = v1len < -sphereRadius;
        return !(angleCull || frontCull || backCull);
    }
}