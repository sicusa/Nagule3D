namespace Nagule.Graphics;

using System.Collections.Immutable;
using Sia;

[SiaTemplate(nameof(GLSLProgram), Immutable = true)]
[NaAsset]
public record RGLSLProgram : AssetBase
{
    public static RGLSLProgram Standard { get; } =
        new RGLSLProgram() { Name = "standard" }
            .WithShaders(
                new(ShaderType.Vertex, ShaderUtils.LoadCore("standard.vert.glsl")),
                new(ShaderType.Fragment, ShaderUtils.LoadCore("blinn_phong.frag.glsl")))
            .WithParameters(
                MaterialKeys.Tiling,
                MaterialKeys.Offset,
                MaterialKeys.Diffuse,
                MaterialKeys.DiffuseTex,
                MaterialKeys.Specular,
                MaterialKeys.SpecularTex,
                MaterialKeys.RoughnessTex,
                MaterialKeys.Emission,
                MaterialKeys.EmissiveTex,
                MaterialKeys.Shininess,
                MaterialKeys.Reflectivity,
                MaterialKeys.ReflectionTex,
                MaterialKeys.OpacityTex,
                MaterialKeys.Threshold,
                MaterialKeys.NormalTex,
                MaterialKeys.HeightTex,
                MaterialKeys.LightmapTex,
                MaterialKeys.Ambient,
                MaterialKeys.AmbientTex,
                MaterialKeys.OcclusionTex,
                MaterialKeys.OcclusionStrength,
                MaterialKeys.ParallaxScale,
                MaterialKeys.EnableParallaxEdgeClip,
                MaterialKeys.EnableParallaxShadow);

    public static RGLSLProgram White { get; } =
        new RGLSLProgram()
            .WithShaders(
                new(ShaderType.Vertex, ShaderUtils.LoadCore("nagule.common.simple.vert.glsl")),
                new(ShaderType.Fragment, ShaderUtils.LoadCore("nagule.common.white.frag.glsl")));
        
    public static RGLSLProgram Depth { get; } =
        new RGLSLProgram()
            .WithShaders(
                new(ShaderType.Vertex, ShaderUtils.LoadCore("nagule.common.simple.vert.glsl")),
                new(ShaderType.Fragment, ShaderUtils.EmptyFragmentShader));

    public ImmutableDictionary<ShaderType, string> Shaders { get; init; }
        = ImmutableDictionary<ShaderType, string>.Empty;

    public ImmutableHashSet<string> Macros { get; init; } = [];

    public ImmutableDictionary<string, ShaderParameterType> Parameters { get; init; }
        = ImmutableDictionary<string, ShaderParameterType>.Empty;

    public ImmutableList<string> Feedbacks { get; init; } = [];
    
    public ImmutableDictionary<ShaderType, ImmutableArray<string>> Subroutines { get; init; }
        = ImmutableDictionary<ShaderType, ImmutableArray<string>>.Empty;

    public RGLSLProgram WithShader(ShaderType shaderType, string source)
        => this with { Shaders = Shaders.SetItem(shaderType, source) };
    public RGLSLProgram WithShaders(params KeyValuePair<ShaderType, string>[] shaders)
        => this with { Shaders = Shaders.SetItems(shaders) };
    public RGLSLProgram WithShaders(IEnumerable<KeyValuePair<ShaderType, string>> shaders)
        => this with { Shaders = Shaders.SetItems(shaders) };

    public RGLSLProgram WithMacro(string macro)
        => this with { Macros = Macros.Add(macro) };
    public RGLSLProgram WithMacros(params string[] macros)
        => this with { Macros = Macros.Union(macros) };
    public RGLSLProgram WithMacros(IEnumerable<string> macros)
        => this with { Macros = Macros.Union(macros) };

    public RGLSLProgram WithParameter(string name, ShaderParameterType parameterType)
        => this with { Parameters = Parameters.SetItem(name, parameterType) };
    public RGLSLProgram WithParameter(ShaderParameter parameter)
        => this with { Parameters = Parameters.SetItem(parameter.Name, parameter.Type) };
    public RGLSLProgram WithParameters(params ShaderParameter[] parameters)
        => this with { Parameters = Parameters.SetItems(parameters.Select(ShaderParameter.ToPair)) };
    public RGLSLProgram WithParameters(IEnumerable<ShaderParameter> parameters)
        => this with { Parameters = Parameters.SetItems(parameters.Select(ShaderParameter.ToPair)) };

    public RGLSLProgram WithFeedback(string feedback)
        => this with { Feedbacks = Feedbacks.Add(feedback) };
    public RGLSLProgram WithFeedbacks(params string[] feedbacks)
        => this with { Feedbacks = Feedbacks.AddRange(feedbacks) };
    public RGLSLProgram WithFeedbacks(IEnumerable<string> feedbacks)
        => this with { Feedbacks = Feedbacks.AddRange(feedbacks) };

    public RGLSLProgram WithSubroutine(ShaderType shaderType, ImmutableArray<string> names)
        => this with { Subroutines = Subroutines.SetItem(shaderType, names) };
    public RGLSLProgram WithSubroutines(params KeyValuePair<ShaderType, ImmutableArray<string>>[] subroutines)
        => this with { Subroutines = Subroutines.SetItems(subroutines) };
    public RGLSLProgram WithSubroutines(IEnumerable<KeyValuePair<ShaderType, ImmutableArray<string>>> subroutines)
        => this with { Subroutines = Subroutines.SetItems(subroutines) };
}