namespace Nagule.Graphics.Backend.OpenTK;

using System.Runtime.CompilerServices;
using Sia;

public record struct MaterialReferences
{
    public GLSLProgramAsset ColorProgramAsset;
    public EntityRef ColorProgram;

    public EntityRef DepthProgram;
    public GLSLProgramAsset DepthProgramAsset;

    public Dictionary<string, EntityRef>? Textures;
}

public record struct MaterialState : IAssetState
{
    public readonly bool Loaded => UniformBufferHandle != BufferHandle.Zero;

    public BufferHandle UniformBufferHandle;
    public IntPtr Pointer;

    public EntityRef ColorProgram;
    public EntityRef DepthProgram;

    public Dictionary<string, EntityRef>? Textures;

    public RenderMode RenderMode;
    public LightingMode LightingMode;
    public bool IsTwoSided;

    public readonly int EnableTextures(
        in GLSLProgramState programState, int startIndex)
    {
        var textureLocations = programState.TextureLocations;
        if (Textures == null || textureLocations == null) {
            return startIndex;
        }

        foreach (var (name, texEntity) in Textures) {
            if (!textureLocations.TryGetValue(name, out var location)) {
                continue;
            }

            ref var texHandle = ref texEntity.GetState<TextureHandle>();
            if (Unsafe.IsNullRef(ref texHandle)) {
                GL.Uniform1i(location, 0);
                continue;
            }

            GL.ActiveTexture(TextureUnit.Texture0 + (uint)startIndex);
            GL.BindTexture(TextureTarget.Texture2d, texHandle.Handle);
            GL.Uniform1i(location, startIndex);
            
            ++startIndex;
        }

        return startIndex;
    }
}