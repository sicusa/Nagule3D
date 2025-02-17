namespace Nagule.Graphics.Backends.OpenTK;

using Sia;

public record struct RenderSettingsState : IAssetState
{
    public readonly bool Loaded => true;

    public RenderPassChain RenderPassChain;

    public (int, int)? Resolution;
    public EntityRef? SunLightState;
}