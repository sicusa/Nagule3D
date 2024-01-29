namespace Nagule.Graphics.Backends.OpenTK;

using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance.Buffers;
using Sia;

public class Mesh3DInstanceTransformUpdateSystem()
    : RenderSystemBase(
        matcher: Matchers.Of<Mesh3D>(),
        trigger: EventUnion.Of<Feature.OnTransformChanged>())
{
    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
        => world.GetAddon<GLMesh3DInstanceUpdator>().Record(query);
}

public class Mesh3DInstanceGroupSystem()
    : RenderSystemBase(
        matcher: Matchers.Of<Mesh3D>(),
        trigger: EventUnion.Of<
            WorldEvents.Add,
            Mesh3D.SetData,
            Mesh3D.SetMaterial>())
{
    private record struct EntryData(EntityRef Entity, Mesh3DInstanceGroupKey Key, Matrix4x4 WorldMatrix);

    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        int count = query.Count;
        if (count == 0) { return; }

        var lib = world.GetAddon<GLMesh3DInstanceLibrary>();
        var meshManager = world.GetAddon<Mesh3DManager>();

        var mem = MemoryOwner<EntryData>.Allocate(count);
        query.Record(world, mem, static (in World world, in EntityRef entity, ref EntryData value) => {
            ref var mesh = ref entity.Get<Mesh3D>();
            var matEntity = world.GetAssetEntity(mesh.Material);
            var worldMatrix = entity.GetFeatureNode<Transform3D>().World;
            value = new(entity, new(matEntity.GetStateEntity(), mesh.Data), worldMatrix);
        });

        RenderFramer.Start(() => {
            var groups = lib.Groups;
            var instanceEntries = lib.InstanceEntries;

            foreach (var (entity, key, mat) in mem.Span) {
                ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(
                    instanceEntries, entity, out bool exists);
                var group = entry.Group;

                if (exists) {
                    if (entry!.Group.Key == key) {
                        continue;
                    }
                    if (group.Count == 1) {
                        group.Dispose();
                        groups.Remove(group.Key);
                    }
                    else {
                        group.Remove(entry.Index);
                        instanceEntries[group.Entities[entry.Index]] = (group, entry.Index);
                    }
                }

                ref var sharedGroup = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, key, out exists);
                if (!exists) {
                    sharedGroup = new(key, meshManager.DataBuffers[key.MeshData]);
                }

                int index = sharedGroup!.Add(entity, mat);
                entry = (sharedGroup, index);
            }

            mem.Dispose();
            return true;
        });
    }
}

public class GLInstancedModule()
    : AddonSystemBase(
        children: SystemChain.Empty
            .Add<Mesh3DInstanceTransformUpdateSystem>()
            .Add<Mesh3DInstanceGroupSystem>())
{
    public override void Initialize(World world, Scheduler scheduler)
    {
        base.Initialize(world, scheduler);
        AddAddon<GLMesh3DInstanceLibrary>(world);
        AddAddon<GLMesh3DInstanceCleaner>(world);
        AddAddon<GLMesh3DInstanceUpdator>(world);
    }
}