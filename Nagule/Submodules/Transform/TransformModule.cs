namespace Nagule;

using Sia;

public class Transform3DDirtyHandleSystem()
    : SystemBase(
        matcher: Matchers.Of<Transform3D, NodeHierarchy>(),
        trigger: EventUnion.Of<
            Transform3D.SetLocal,
            Transform3D.SetWorld,
            Transform3D.SetPosition,
            Transform3D.SetRotation,
            Transform3D.SetScale,
            Transform3D.SetWorldPosition,
            Transform3D.SetWorldRotation>())
{
    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        query.ForEach(world, static (world, entity) => {
            TransformUtils.NotifyDirty(world, entity);
        });
    }
}

public class Transform3DParentChangeHandleSystem()
    : SystemBase(
        matcher: Matchers.Of<Transform3D, NodeHierarchy>(),
        trigger: EventUnion.Of<NodeHierarchy.SetParent>())
{
    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        query.ForEach(world, static (world, entity) => {
            ref var trans = ref entity.Get<Transform3D>();

            trans.Parent = entity.Get<NodeHierarchy>().Parent;
            trans.DirtyTags |= TransformDirtyTags.Globals;

            TransformUtils.NotifyDirty(world, entity);
        });
    }
}

public class TransformModule()
    : SystemBase(
        children: SystemChain.Empty
            .Add<Transform3DDirtyHandleSystem>()
            .Add<Transform3DParentChangeHandleSystem>());