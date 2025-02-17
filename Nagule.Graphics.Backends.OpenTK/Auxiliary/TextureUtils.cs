namespace Nagule.Graphics.Backends.OpenTK;

public static class TextureUtils
{
    public static int Cast(TextureWrapMode mode)
        => (int)(mode switch {
            TextureWrapMode.ClampToBorder => GLTextureWrapMode.ClampToBorder,
            TextureWrapMode.ClampToEdge => GLTextureWrapMode.ClampToEdge,
            TextureWrapMode.MirroredRepeat => GLTextureWrapMode.MirroredRepeat,
            TextureWrapMode.Repeat => GLTextureWrapMode.Repeat,
            _ => throw new NotSupportedException("Invalid texture wrap mode")
        });

    public static int Cast(TextureMinFilter filter, bool mipmapEnabled)
        => (int)(filter switch {
            TextureMinFilter.Linear => GLTextureMinFilter.Linear,
            TextureMinFilter.LinearMipmapLinear => mipmapEnabled
                ? GLTextureMinFilter.LinearMipmapLinear : GLTextureMinFilter.Linear,
            TextureMinFilter.LinearMipmapNearest => mipmapEnabled
                ? GLTextureMinFilter.LinearMipmapNearest : GLTextureMinFilter.Linear,
            TextureMinFilter.Nearest => GLTextureMinFilter.Nearest,
            TextureMinFilter.NearestMipmapLinear => mipmapEnabled
                ? GLTextureMinFilter.NearestMipmapLinear : GLTextureMinFilter.Nearest,
            TextureMinFilter.NearestMipmapNearest => mipmapEnabled
                ? GLTextureMinFilter.NearestMipmapNearest : GLTextureMinFilter.Nearest,
            _ => throw new NotSupportedException("Invalid texture wrap mode")
        });

    public static int Cast(TextureMagFilter filter)
        => (int)(filter switch {
            TextureMagFilter.Linear => GLTextureMagFilter.Linear,
            TextureMagFilter.Nearest => GLTextureMagFilter.Nearest,
            _ => throw new NotSupportedException("Invalid texture wrap mode")
        });
    
    public static TextureTarget Cast(CubemapFace target)
        => target switch {
            CubemapFace.Right => TextureTarget.TextureCubeMapPositiveX,
            CubemapFace.Left => TextureTarget.TextureCubeMapNegativeX,
            CubemapFace.Top => TextureTarget.TextureCubeMapNegativeY,
            CubemapFace.Bottom => TextureTarget.TextureCubeMapPositiveY,
            CubemapFace.Back => TextureTarget.TextureCubeMapPositiveZ,
            CubemapFace.Front => TextureTarget.TextureCubeMapNegativeZ,
            _ => throw new NotSupportedException("Invalid cubemap texture target")
        };
}