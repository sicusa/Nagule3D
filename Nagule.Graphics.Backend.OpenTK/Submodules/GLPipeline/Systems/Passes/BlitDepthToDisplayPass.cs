namespace Nagule.Graphics.Backend.OpenTK;

using Sia;

public class BlitDepthToDisplayPass : RenderPassSystemBase
{
    private static readonly RGLSLProgram s_blitProgramAsset = 
        new RGLSLProgram {
            Name = "nagule.pipeline.blit_depth_to_display"
        }
        .WithShaders(
            new(ShaderType.Fragment,
                ShaderUtils.LoadCore("nagule.common.blit_depth.frag.glsl")),
            new(ShaderType.Vertex,
                ShaderUtils.LoadCore("nagule.common.quad.vert.glsl")))
        .WithParameter("ColorBuffer", ShaderParameterType.Texture2D);

    public override void Initialize(World world, Scheduler scheduler)
    {
        base.Initialize(world, scheduler);

        var primaryWindow = world.GetAddon<PrimaryWindow>();

        var blitProgramEntity = GLSLProgram.CreateEntity(
            world, s_blitProgramAsset, AssetLife.Persistent);

        RenderFramer.Start(() => {
            ref var blitProgramState = ref blitProgramEntity.GetState<GLSLProgramState>();
            if (!blitProgramState.Loaded) { return NextFrame; }

            var framebuffer = Pipeline.GetAddon<Framebuffer>();
            var hizBuffer = Pipeline.GetAddon<HierarchicalZBuffer>();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.UseProgram(blitProgramState.Handle.Handle);
            GL.BindVertexArray(framebuffer.EmptyVertexArray.Handle);

            var window = primaryWindow.Entity.Get<Window>();
            var (width, height) = window.PhysicalSize;
            GL.Viewport(0, 0, width, height);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2d, hizBuffer.TextureHandle.Handle);
            GL.Uniform1i(0, 0);

            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            GL.DrawArrays(GLPrimitiveType.TriangleStrip, 0, 4);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
            
            return NextFrame;
        });
    }
}