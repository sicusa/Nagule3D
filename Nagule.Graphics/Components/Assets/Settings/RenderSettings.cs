namespace Nagule.Graphics;

using Sia;

[SiaTemplate(nameof(RenderSettings))]
[NaAsset]
public record RRenderSettings : AssetBase
{
    public static RRenderSettings Default { get; } = new();

    public IRenderPipelineProvider? PipelineProvider { get; init; }

    public (int, int)? Resolution { get; init; }
    public bool IsOcclusionCullingEnabled { get; init; } = true;

    public AssetRefer<RLight3D>? SunLight { get; init; }
}