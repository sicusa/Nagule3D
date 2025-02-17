namespace Nagule.Graphics.Backends.OpenTK;

using System.Numerics;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Sia;

public partial class Mesh3DManager
{
    public IReadOnlyDictionary<Mesh3DData, Mesh3DDataBuffer> DataBuffers => _dataBuffers;

    private readonly Dictionary<Mesh3DData, Mesh3DDataBuffer> _dataBuffers = [];
    private readonly Dictionary<object, Mesh3DSubBuffer> _subBuffers = [];

    public override void OnInitialize(World world)
    {
        base.OnInitialize(world);

        Listen((in EntityRef entity, ref Mesh3D snapshot, in Mesh3D.SetData cmd) => {
            var data = cmd.Value;
            var stateEntity = entity.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<Mesh3DState>();
                UnreferDataBuffer(state.DataBuffer!);
                state.DataBuffer = AcquireDataState(data);
            });
        });

        Listen((in EntityRef entity, ref Mesh3D snapshot, in Mesh3D.SetMaterial cmd) => {
            var prevMaterial = snapshot.Material;
            entity.Unrefer(world.GetAsset(prevMaterial));

            var material = cmd.Value;
            var matEntity = World.AcquireAsset(material);
            var stateEntity = entity.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<Mesh3DState>();
                state.MaterialEntity = matEntity;
            });
        });
    }

    public override void LoadAsset(in EntityRef entity, ref Mesh3D asset, EntityRef stateEntity)
    {
        var data = asset.Data;
        var matEntity = World.AcquireAsset(asset.Material, entity);

        RenderFramer.Enqueue(entity, () => {
            stateEntity.Get<Mesh3DState>() = new Mesh3DState {
                MaterialEntity = matEntity,
                DataBuffer = AcquireDataState(data),
            };
        });
    }

    public override void UnloadAsset(in EntityRef entity, in Mesh3D asset, EntityRef stateEntity)
    {
        RenderFramer.Enqueue(entity, () => {
            ref var state = ref stateEntity.Get<Mesh3DState>();
            UnreferDataBuffer(state.DataBuffer!);
        });
    }

    private Mesh3DDataBuffer AcquireDataState(Mesh3DData data)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(_dataBuffers, data, out bool exists);
        if (exists) {
            Interlocked.Increment(ref state!.RefCount);
            return state;
        }

        var handle = GL.GenBuffer();
        state = new Mesh3DDataBuffer(data, new(handle));

        var indices = data.Indices;
        var vertices = data.Vertices;
        var texCoords = data.TexCoords;
        var normals = data.Normals;
        var tangents = data.Tangents;

        GL.BindBuffer(BufferTargetARB.UniformBuffer, handle);
        GL.BufferData(BufferTargetARB.UniformBuffer, 2 * 16, IntPtr.Zero, BufferUsageARB.DynamicDraw);
        GL.BindBufferBase(BufferTargetARB.UniformBuffer, (int)UniformBlockBinding.Mesh, handle);

        GL.BufferSubData(BufferTargetARB.UniformBuffer, IntPtr.Zero, 12, state.BoundingBox.Min);
        GL.BufferSubData(BufferTargetARB.UniformBuffer, IntPtr.Zero + 16, 12, state.BoundingBox.Max);
        GL.BindBuffer(BufferTargetARB.UniformBuffer, 0);

        var buffers = state.SubBuffers;
        if (vertices.Length != 0) { buffers[Mesh3DBufferType.Vertices] = AcquireSubBuffer(vertices); }
        if (texCoords.Length != 0) { buffers[Mesh3DBufferType.TexCoords] = AcquireSubBuffer(texCoords); }
        if (normals.Length != 0) { buffers[Mesh3DBufferType.Normals] = AcquireSubBuffer(normals); }
        if (tangents.Length != 0) { buffers[Mesh3DBufferType.Tangents] = AcquireSubBuffer(tangents); }
        if (indices.Length != 0) { buffers[Mesh3DBufferType.Indices] = AcquireSubBuffer(indices); }

        return state;
    }

    private void UnreferDataBuffer(Mesh3DDataBuffer buffer)
    {
        if (Interlocked.Decrement(ref buffer.RefCount) != 0) {
            return;
        }
        GL.DeleteBuffer(buffer.Handle.Handle);
        foreach (var subBuffer in buffer.SubBuffers.Values) {
            if (Interlocked.Decrement(ref buffer.RefCount) == 0) {
                GL.DeleteBuffer(subBuffer.Handle.Handle);
                _subBuffers.Remove(subBuffer.Key);
            }
        }
        _dataBuffers.Remove(buffer.Key);
    }

    private Mesh3DSubBuffer AcquireSubBuffer(ImmutableArray<Vector3> array)
    {
        ref var subBuffer = ref CollectionsMarshal.GetValueRefOrAddDefault(_subBuffers, array, out bool exists);
        if (exists) {
            Interlocked.Increment(ref subBuffer!.RefCount);
            return subBuffer;
        }

        var handle = GL.GenBuffer();
        GL.BindBuffer(BufferTargetARB.ArrayBuffer, handle);
        GL.BufferData(BufferTargetARB.ArrayBuffer, array.AsSpan(), BufferUsageARB.StaticDraw);
        GL.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        subBuffer = new(array, new(handle));
        return subBuffer;
    }

    private Mesh3DSubBuffer AcquireSubBuffer(ImmutableArray<uint> array)
    {
        ref var subBuffer = ref CollectionsMarshal.GetValueRefOrAddDefault(_subBuffers, array, out bool exists);
        if (exists) {
            Interlocked.Increment(ref subBuffer!.RefCount);
            return subBuffer;
        }

        var handle = GL.GenBuffer();
        GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, handle);
        GL.BufferData(BufferTargetARB.ElementArrayBuffer, array.AsSpan(), BufferUsageARB.StaticDraw);
        GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

        subBuffer = new(array, new(handle));
        return subBuffer;
    }
}