namespace Nagule.Graphics.Backend.OpenTK;

using System.Diagnostics.CodeAnalysis;

using Nagule.Graphics;

using ShaderType = Nagule.Graphics.ShaderType;

public class MaterialManager : ResourceManagerBase<Material>
{
    private class InitializeCommand : Command<InitializeCommand, RenderTarget>, IDeferrableCommand
    {
        public Guid MaterialId;
        public Material? Resource;
        public Guid ShaderProgramId;
        public Guid DepthShaderProgramId;
        [AllowNull] public Dictionary<string, Guid> Textures;

        public override Guid? Id => MaterialId;

        public bool ShouldExecute(ICommandHost host)
            => host.Contains<GLSLProgramData>(ShaderProgramId);

        public unsafe override void Execute(ICommandHost host)
        {
            ref var data = ref host.Acquire<MaterialData>(MaterialId, out bool exists);
            ref readonly var programData = ref host.Inspect<GLSLProgramData>(ShaderProgramId);

            if (!exists) {
                data.Handle = GL.GenBuffer();
                GL.BindBuffer(BufferTargetARB.UniformBuffer, data.Handle);
                data.Pointer = GLHelper.InitializeBuffer(
                    BufferTargetARB.UniformBuffer, programData.MaterialBlockSize);
            }

            data.IsTwoSided = Resource!.IsTwoSided;
            data.ShaderProgramId = ShaderProgramId;
            data.DepthShaderProgramId = DepthShaderProgramId;
            data.Textures = Textures;

            if (programData.Parameters != null) {
                var ptr = data.Pointer;
                var pars = programData.Parameters;
                
                foreach (var (name, value) in Resource.Properties) {
                    if (pars.TryGetValue(name, out var entry)) {
                        GraphicsHelper.SetShaderParameter(
                            name, entry.Type, value, ptr + entry.Offset);
                    }
                }
            }
        }
    }

    private class UninitializeCommand : Command<UninitializeCommand, RenderTarget>
    {
        public Guid MaterialId;

        public override void Execute(ICommandHost host)
        {
            if (host.Remove<MaterialData>(MaterialId, out var data)) {
                GL.DeleteBuffer(data.Handle);
            }
        }
    }

    protected override void Initialize(
        IContext context, Guid id, Material resource, Material? prevResource)
    {
        if (prevResource != null) {
            ResourceLibrary.UnreferenceAll(context, id);
        }

        var cmd = InitializeCommand.Create();
        cmd.MaterialId = id;
        cmd.Resource = resource;

        var shaderProgram = GraphicsHelper.TransformMaterialShaderProgram(
            context, resource, (context, name, value) => {
                switch (value) {
                case TextureDyn textureDyn:
                    if (textureDyn.Value != null) {
                        cmd.Textures ??= new();
                        cmd.Textures[name] = ResourceLibrary.Reference(context, id, textureDyn.Value);
                    }
                    break;
                case CubemapDyn cubemapDyn:
                    if (cubemapDyn.Value != null) {
                        cmd.Textures ??= new();
                        cmd.Textures[name] = ResourceLibrary.Reference(context, id, cubemapDyn.Value);
                    }
                    break;
                case RenderTextureDyn renderTexDyn:
                    if (renderTexDyn.Value != null) {
                        cmd.Textures ??= new();
                        cmd.Textures[name] = ResourceLibrary.Reference(context, id, renderTexDyn.Value);
                    }
                    break;
                }
            });

        cmd.ShaderProgramId =
            ResourceLibrary.Reference(context, id, shaderProgram);

        cmd.DepthShaderProgramId =
            ResourceLibrary.Reference(
                context, id, shaderProgram.WithShader(
                    ShaderType.Fragment, GraphicsHelper.EmptyFragmentShader));

        context.SendCommandBatched(cmd);
    }

    protected override void Uninitialize(IContext context, Guid id, Material resource)
    {
        ResourceLibrary.UnreferenceAll(context, id);

        var cmd = UninitializeCommand.Create();
        cmd.MaterialId = id;
        context.SendCommandBatched(cmd);
    }
}