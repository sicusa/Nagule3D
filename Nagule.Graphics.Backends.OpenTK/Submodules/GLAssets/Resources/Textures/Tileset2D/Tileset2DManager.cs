namespace Nagule.Graphics.Backends.OpenTK;

using Sia;

public partial class Tileset2DManager
{
    protected override TextureTarget TextureTarget => TextureTarget.Texture2dArray;

    public override void OnInitialize(World world)
    {
        base.OnInitialize(world);

        RegisterCommonListeners(
            (Tileset2D.SetMinFilter cmd) => cmd.Value,
            (Tileset2D.SetMagFilter cmd) => cmd.Value,
            (Tileset2D.SetBorderColor cmd) => cmd.Value,
            (Tileset2D.SetIsMipmapEnabled cmd) => cmd.Value);
        
        RegisterParameterListener((ref Tileset2DState state, in Tileset2D.SetWrapU cmd) =>
            GL.TexParameteri(TextureTarget, TextureParameterName.TextureWrapS, TextureUtils.Cast(cmd.Value)));

        RegisterParameterListener((ref Tileset2DState state, in Tileset2D.SetWrapV cmd) =>
            GL.TexParameteri(TextureTarget, TextureParameterName.TextureWrapT, TextureUtils.Cast(cmd.Value)));

        void Regenerate(in EntityRef entity)
        {
            var stateEntity = entity.GetStateEntity();

            ref var tex = ref entity.Get<Tileset2D>();
            var usage = tex.Usage;
            var image = tex.Image;
            var count = tex.Count;
            var tileWidth = tex.TileWidth;
            var tileHeight = tex.TileHeight;

            RegenerateTexture(entity, () => {
                ref var state = ref stateEntity.Get<Tileset2DState>();
                state.Width = image.Width;
                state.Height = image.Height;
                state.TileWidth = tileWidth;
                state.TileHeight = tileHeight;
                LoadImage(usage, tileWidth, tileHeight, count, image);
            });
        }
        
        Listen((in EntityRef e, in Tileset2D.SetUsage cmd) => Regenerate(e));
        Listen((in EntityRef e, in Tileset2D.SetImage cmd) => Regenerate(e));
        Listen((in EntityRef e, in Tileset2D.SetTileWidth cmd) => Regenerate(e));
        Listen((in EntityRef e, in Tileset2D.SetTileHeight cmd) => Regenerate(e));
        Listen((in EntityRef e, in Tileset2D.SetCount cmd) => Regenerate(e));
    }

    public unsafe override void LoadAsset(in EntityRef entity, ref Tileset2D asset, EntityRef stateEntity)
    {
        var usage = asset.Usage;

        var image = asset.Image;
        var count = asset.Count;
        var tileWidth = asset.TileWidth;
        var tileHeight = asset.TileHeight;

        var wrapU = asset.WrapU;
        var wrapV = asset.WrapV;

        var minFilter = asset.MinFilter;
        var magFilter = asset.MagFilter;
        var borderColor = asset.BorderColor;
        var mipmapEnabled = asset.IsMipmapEnabled;

        RenderFramer.Enqueue(entity, () => {
            ref var state = ref stateEntity.Get<Tileset2DState>();
            state = new Tileset2DState {
                Handle = new(GL.GenTexture()),
                Width = image.Width,
                Height = image.Height,
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                MinFilter = minFilter,
                MagFilter = magFilter,
                IsMipmapEnabled = mipmapEnabled
            };

            GL.BindTexture(TextureTarget, state.Handle.Handle);
            LoadImage(usage, tileWidth, tileHeight, count, image);
            GL.TexParameteri(TextureTarget, TextureParameterName.TextureWrapS, TextureUtils.Cast(wrapU));
            GL.TexParameteri(TextureTarget, TextureParameterName.TextureWrapT, TextureUtils.Cast(wrapV));
            
            SetCommonParameters(minFilter, magFilter, borderColor, mipmapEnabled);
            SetTextureInfo(stateEntity, state);
        });
    }

    private unsafe void LoadImage(
        TextureUsage usage, int tileWidth, int tileHeight, int? optionalCount, RImageBase image)
    {
        var tileXCount = image.Width / tileWidth;
        var tileYCount = image.Height / tileHeight;
        var count = optionalCount ?? tileXCount * tileYCount;

        var pixelFormat = image.PixelFormat;
        var (internalFormat, pixelType) = GLUtils.GetTexPixelInfo(image);
        var glPixelFormat = GLUtils.SetPixelFormat(TextureTarget, pixelFormat, internalFormat, pixelType);

        if (GLUtils.IsSRGBTexture(usage)) {
            internalFormat = GLUtils.ToSRGBColorSpace(internalFormat);
        }

        GL.TexImage3D(TextureTarget, 0, internalFormat, tileWidth, tileHeight, count, 0, glPixelFormat, pixelType, (void*)0);

        if (count == 0 || image.Length == 0) {
            return;
        }

        int channelByteCount = image.ChannelSize;
        int channelCount = image.PixelFormat switch {
            PixelFormat.Grey => 1,
            PixelFormat.GreyAlpha => 2,
            PixelFormat.RGB => 3,
            PixelFormat.RGBA => 4,
            _ => throw new NaguleInternalException("Invalid pixel format")
        };
        int pixelByteCount = channelByteCount * channelCount;
        var imageBytes = image.AsByteSpan();

        GL.PixelStorei(PixelStoreParameter.UnpackRowLength, image.Width);
        GL.PixelStorei(PixelStoreParameter.UnpackImageHeight, image.Height);

        for (int y = 0; y < tileYCount; ++y) {
            for (int x = 0; x < tileXCount; ++x) {
                int i = (tileYCount - y - 1) * tileXCount + x;
                if (i >= count) {
                    goto Stop;
                }
                int offset = (y * tileHeight * image.Width + x * tileWidth) * pixelByteCount;
                GL.TexSubImage3D(TextureTarget, 0, 0, 0, i, tileWidth, tileHeight, 1, glPixelFormat, pixelType, imageBytes[offset]);
            }
        }
        
    Stop:
        GL.PixelStorei(PixelStoreParameter.UnpackRowLength, 0);
        GL.PixelStorei(PixelStoreParameter.UnpackImageHeight, 0);
    }
}