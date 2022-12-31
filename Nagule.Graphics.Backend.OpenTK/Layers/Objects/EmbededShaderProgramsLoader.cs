namespace Nagule.Graphics.Backend.OpenTK;

using System.Collections.Immutable;

using Aeco;

using Nagule.Graphics;

public class EmbededShaderProgramsLoader : VirtualLayer, ILoadListener
{
    private static string LoadShader(string resourceId)
        => InternalAssets.LoadText("Nagule.Graphics.Backend.OpenTK.Embeded.Shaders." + resourceId);

    public void OnLoad(IContext context)
    {
        var emptyVertShader = LoadShader("nagule.utils.empty.vert.glsl");
        var simpleVertShader = LoadShader("nagule.utils.simple.vert.glsl");
        var whiteFragShader = LoadShader("nagule.utils.white.frag.glsl");
        var quadGeoShader = LoadShader("nagule.utils.quad.geo.glsl");
        var blinnPhongVert = LoadShader("blinn_phong.vert.glsl");
        var unlitVert = LoadShader("unlit.vert.glsl");

        // load default opaque shader program

        var resource = new ShaderProgram()
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, blinnPhongVert),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("blinn_phong.frag.glsl")));

        ref var program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DefaultOpaqueProgramId);
        program.Value = resource;
        Console.WriteLine("Default opaque shader program loaded: " + Graphics.DefaultOpaqueProgramId);

        // load default transparent shader program

        resource = new ShaderProgram()
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, blinnPhongVert),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("blinn_phong_transparent.frag.glsl")));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DefaultTransparentShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Default transparent shader program loaded: " + Graphics.DefaultTransparentShaderProgramId);

        // load default cutoff shader program

        resource = new ShaderProgram()
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, blinnPhongVert),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("blinn_phong_cutoff.frag.glsl")))
            .WithParameter("Threshold", ShaderParameterType.Float);

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DefaultCutoffShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Default cutoff shader program loaded: " + Graphics.DefaultCutoffShaderProgramId);

        // load default unlit shader program

        resource = new ShaderProgram()
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, unlitVert),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("unlit.frag.glsl")));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DefaultUnlitProgramId);
        program.Value = resource;
        Console.WriteLine("Default unlit shader program loaded: " + Graphics.DefaultUnlitProgramId);

        // load default unlit transparent shader program

        resource = new ShaderProgram()
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, unlitVert),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("unlit_transparent.frag.glsl")));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DefaultUnlitTransparentShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Default unlit transparent shader program loaded: " + Graphics.DefaultUnlitTransparentShaderProgramId);

        // load default unlit cutoff shader program

        resource = new ShaderProgram()
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, unlitVert),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("unlit_cutoff.frag.glsl")))
            .WithParameter("Threshold", ShaderParameterType.Float);

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DefaultUnlitCutoffShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Default unlit cutoff shader program loaded: " + Graphics.DefaultUnlitCutoffShaderProgramId);

        // load culling shader program

        resource = ShaderProgram.NonMaterial
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, LoadShader("nagule.pipeline.cull.vert.glsl")),
                KeyValuePair.Create(ShaderType.Geometry, LoadShader("nagule.pipeline.cull.geo.glsl")))
            .WithTransformFeedbackVarying("CulledObjectToWorld");

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.CullingShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Culling shader program loaded: " + Graphics.CullingShaderProgramId);

        // load hierarchical-Z shader program

        resource = ShaderProgram.NonMaterial
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, emptyVertShader),
                KeyValuePair.Create(ShaderType.Geometry, quadGeoShader),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("nagule.pipeline.hiz.frag.glsl")))
            .WithParameters(
                KeyValuePair.Create("LastMip", ShaderParameterType.Texture),
                KeyValuePair.Create("LastMipSize", ShaderParameterType.IntVector2));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.HierarchicalZShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Hierarchical-Z shader program loaded: " + Graphics.HierarchicalZShaderProgramId);

        // transparency compose shader program

        resource = ShaderProgram.NonMaterial
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, emptyVertShader),
                KeyValuePair.Create(ShaderType.Geometry, quadGeoShader),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("nagule.pipeline.transparency_compose.frag.glsl")))
            .WithParameters(
                KeyValuePair.Create("AccumTex", ShaderParameterType.Texture),
                KeyValuePair.Create("RevealTex", ShaderParameterType.Texture));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.TransparencyComposeShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Transparency compose shader program loaded: " + Graphics.TransparencyComposeShaderProgramId);

        // load post-processing shader program

        resource = ShaderProgram.NonMaterial
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, emptyVertShader),
                KeyValuePair.Create(ShaderType.Geometry, quadGeoShader),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("nagule.pipeline.post.frag.glsl")));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.PostProcessingShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Post-processing shader program loaded: " + Graphics.PostProcessingShaderProgramId);

        // load debugging post-processing shader program

        resource = ShaderProgram.NonMaterial
            .WithShaders(
                KeyValuePair.Create(ShaderType.Vertex, emptyVertShader),
                KeyValuePair.Create(ShaderType.Geometry, quadGeoShader),
                KeyValuePair.Create(ShaderType.Fragment, LoadShader("nagule.pipeline.post_debug.frag.glsl")))
            .WithParameters(
                KeyValuePair.Create("ColorBuffer", ShaderParameterType.Texture),
                KeyValuePair.Create("TransparencyAccumBuffer", ShaderParameterType.Texture),
                KeyValuePair.Create("TransparencyRevealBuffer", ShaderParameterType.Texture))
            .WithSubroutine(
                ShaderType.Fragment,
                ImmutableArray.Create(
                    "ShowColor",
                    "ShowTransparencyAccum",
                    "ShowTransparencyReveal",
                    "ShowDepth",
                    "ShowClusters"));

        program = ref context.Acquire<Resource<ShaderProgram>>(Graphics.DebugPostProcessingShaderProgramId);
        program.Value = resource;
        Console.WriteLine("Post-processing debug shader program loaded: " + Graphics.DebugPostProcessingShaderProgramId);
    }
}