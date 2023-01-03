namespace Nagule.Graphics.Backend.OpenTK;

using System.Collections.Concurrent;

using global::OpenTK.Graphics;
using global::OpenTK.Graphics.OpenGL;

using Nagule.Graphics;

public class TextureManager : ResourceManagerBase<Texture, TextureData>, IRenderListener
{
    private ConcurrentQueue<(bool, Guid, Texture)> _commandQueue = new();
    private ConcurrentQueue<(Guid, TextureHandle)> _uiTextures = new();
    private float[] _tempBorderColor = new float[4];

    public override void OnUpdate(IContext context, float deltaTime)
    {
        base.OnUpdate(context, deltaTime);

        while (_uiTextures.TryDequeue(out var tuple)) {
            var (id, handle) = tuple;
            context.Acquire<ImGuiTextureId>(id).Value = (IntPtr)(int)handle;
        }
    }

    protected override void Initialize(
        IContext context, Guid id, Texture resource, ref TextureData data, bool updating)
    {
        if (updating) {
            Uninitialize(context, id, resource, in data);
        }
        _commandQueue.Enqueue((true, id, resource));
    }

    protected override void Uninitialize(IContext context, Guid id, Texture resource, in TextureData data)
    {
        _commandQueue.Enqueue((false, id, resource));
    }

    public unsafe void OnRender(IContext context, float deltaTime)
    {
        while (_commandQueue.TryDequeue(out var command)) {
            var (commandType, id, resource) = command;
            ref var data = ref context.Require<TextureData>(id);

            if (commandType) {
                data.Handle = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2d, data.Handle);

                var image = resource.Image ?? Image.Hint;
                GLHelper.TexImage2D(resource.Type, image.PixelFormat, image.Width, image.Height, image.Bytes.AsSpan());

                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, TextureHelper.Cast(resource.WrapU));
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, TextureHelper.Cast(resource.WrapV));
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, TextureHelper.Cast(resource.MinFilter));
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, TextureHelper.Cast(resource.MaxFilter));

                resource.BorderColor.CopyTo(_tempBorderColor);
                GL.TexParameterf(TextureTarget.Texture2d, TextureParameterName.TextureBorderColor, _tempBorderColor);

                if (resource.MipmapEnabled) {
                    GL.GenerateMipmap(TextureTarget.Texture2d);
                }
                GL.BindTexture(TextureTarget.Texture2d, TextureHandle.Zero);

                if (resource.Type == TextureType.UI) {
                    _uiTextures.Enqueue((id, data.Handle));
                }
            }
            else {
                GL.DeleteTexture(data.Handle);
            }
        }
    }
}