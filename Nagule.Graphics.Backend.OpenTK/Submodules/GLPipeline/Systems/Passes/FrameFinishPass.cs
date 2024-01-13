namespace Nagule.Graphics.Backend.OpenTK;

using Sia;

public class FrameFinishPass : RenderPassSystemBase
{
    public unsafe override void Initialize(World world, Scheduler scheduler)
    {
        base.Initialize(world, scheduler);

        RenderFrame.Start(() => {
            GL.Finish();
            return NextFrame;
        });
    }
}