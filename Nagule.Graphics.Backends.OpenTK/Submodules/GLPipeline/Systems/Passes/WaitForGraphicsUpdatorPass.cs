namespace Nagule.Graphics.Backends.OpenTK;

using Sia;

public class WaitForGraphicsUpdatorPass<TUpdator> : RenderPassBase
    where TUpdator : class, IGLGraphicsUpdator
{
    private TUpdator _updator = null!;

    public override void Initialize(World world, Scheduler scheduler)
    {
        base.Initialize(world, scheduler);
        _updator = MainWorld.GetAddon<TUpdator>();
    }

    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        _updator.WaitSync();
    }
}
