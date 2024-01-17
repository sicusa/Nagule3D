namespace Nagule.Graphics.Backend.OpenTK;

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Sia;

public partial class Camera3DManager
{
    internal float WindowAspectRatio { get; set; }

    [AllowNull] private RenderSettingsManager _renderSettingsManager;

    public override void OnInitialize(World world)
    {
        base.OnInitialize(world);
        _renderSettingsManager = world.GetAddon<RenderSettingsManager>();

        Listen((in EntityRef entity, ref Camera3D snapshot, in Camera3D.SetRenderSettings cmd) => {
            entity.UnreferAsset(world.GetAssetEntity(snapshot.RenderSettings));

            var renderSettingsEntity = _renderSettingsManager.Acquire(cmd.Value, entity);
            var renderSettingsStateEntity = renderSettingsEntity.GetStateEntity();
            var stateEntity = entity.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<Camera3DState>();
                state.RenderSettingsState = renderSettingsStateEntity;
            });
        });

        Listen((in EntityRef entity, in Camera3D.SetClearFlags cmd) => {
            var clearFlags = cmd.Value;
            var stateEntity = entity.GetStateEntity();
            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<Camera3DState>();
                state.ClearFlags = clearFlags;
            });
        });
    }

    protected override void LoadAsset(EntityRef entity, ref Camera3D asset, EntityRef stateEntity)
    {
        stateEntity.Get<RenderPipelineProvider>().Instance =
            new GLPipelineModule.StandardPipelineProvider(asset.RenderSettings.IsDepthOcclusionEnabled);

        ref var trans = ref entity.GetFeatureNode().Get<Transform3D>();
        var view = trans.View;
        var position = trans.Position;

        var renderSettingsEntity = _renderSettingsManager.Acquire(asset.RenderSettings, entity);
        var renderSettingsStateEntity = renderSettingsEntity.GetStateEntity();
        var camera = asset;

        RenderFramer.Enqueue(entity, () => {
            var handle = GL.GenBuffer();
            GL.BindBuffer(BufferTargetARB.UniformBuffer, handle);

            ref var state = ref stateEntity.Get<Camera3DState>();
            state = new Camera3DState {
                RenderSettingsState = renderSettingsStateEntity,
                Handle = new(handle),
                Pointer = GLUtils.InitializeBuffer(BufferTargetARB.UniformBuffer, Camera3DParameters.MemorySize),
                ClearFlags = camera.ClearFlags
            };

            UpdateCameraParameters(ref state, camera);
            UpdateCameraTransform(ref state, view, position);
        });
    }

    protected override void UnloadAsset(EntityRef entity, ref Camera3D asset, EntityRef stateEntity)
    {
        RenderFramer.Enqueue(entity, () => {
            ref var state = ref stateEntity.Get<Camera3DState>();
            GL.DeleteBuffer(state.Handle.Handle);
        });
    }

    internal void UpdateCameraParameters(EntityRef cameraEntity)
    {
        var camera = cameraEntity.Get<Camera3D>();
        var stateEntity = cameraEntity.GetStateEntity();

        ref var trans = ref cameraEntity.GetFeatureNode().Get<Transform3D>();
        var direction = trans.WorldForward;

        RenderFramer.Enqueue(cameraEntity, () => {
            ref var state = ref stateEntity.Get<Camera3DState>();
            if (!state.Loaded) {
                return;
            }
            UpdateCameraParameters(ref state, camera);
            UpdateCameraBoundingBox(ref state, camera, direction);
        });
    }

    internal void UpdateCameraTransform(EntityRef cameraEntity)
    {
        var camera = cameraEntity.Get<Camera3D>();
        var stateEntity = cameraEntity.GetStateEntity();

        ref var trans = ref cameraEntity.GetFeatureNode().Get<Transform3D>();
        var view = trans.View;
        var position = trans.WorldPosition;
        var direction = trans.WorldForward;

        RenderFramer.Enqueue(cameraEntity, () => {
            ref var state = ref stateEntity.Get<Camera3DState>();
            if (!state.Loaded) {
                return;
            }
            UpdateCameraTransform(ref state, view, position);
            UpdateCameraBoundingBox(ref state, camera, direction);
        });
    }

    private void UpdateCameraBoundingBox(ref Camera3DState state, in Camera3D camera, in Vector3 direction)
    {
        float fov = camera.FieldOfView;
        float aspect = camera.AspectRatio ?? WindowAspectRatio;
        float near = camera.NearPlaneDistance;
        float far = camera.FarPlaneDistance;
        ref var pos = ref state.Parameters.Position;

        float nh, nw; // near height & weight
        float fh, fw; // far height & weight

        if (camera.ProjectionMode == ProjectionMode.Perspective) {
            float factor = 2 * MathF.Tan(fov / 180f * MathF.PI / 2f);
            nh = factor * near;
            nw = nh * aspect;
            fh = factor * far;
            fw = fh * aspect;
        }
        else {
            var width = camera.OrthographicWidth;
            nh = fh = width / aspect;
            nw = fw = width;
        }

        var nearCenter = pos + direction * near;
        var nearHalfUp = new Vector3(0f, nh / 2f, 0f);
        var nearHalfRight = new Vector3(nw / 2f, 0f, 0f);

        var farCenter = pos + direction * far;
        var farHalfUp = new Vector3(0f, fh / 2f, 0f);
        var farHalfRight = new Vector3(fw / 2f, 0f, 0f);

        Span<Vector3> points = [
            nearCenter + nearHalfUp + nearHalfRight,
            nearCenter + nearHalfUp - nearHalfRight,
            nearCenter - nearHalfUp + nearHalfRight,
            nearCenter - nearHalfUp - nearHalfRight,

            farCenter + farHalfUp + farHalfRight,
            farCenter + farHalfUp - farHalfRight,
            farCenter - farHalfUp + farHalfRight,
            farCenter - farHalfUp - farHalfRight
        ];

        ref var aabb = ref state.BoundingBox;

        foreach (ref var point in points) {
            aabb.Min = Vector3.Min(aabb.Min, point);
            aabb.Max = Vector3.Max(aabb.Max, point);
        }
    }

    private unsafe void UpdateCameraParameters(ref Camera3DState state, in Camera3D camera)
    {
        state.ParametersVersion++;

        ref var pars = ref state.Parameters;
        float aspectRatio = camera.AspectRatio ?? WindowAspectRatio;

        if (state.ProjectionMode == ProjectionMode.Perspective) {
            state.Projection = Matrix4x4.CreatePerspectiveFieldOfView(
                camera.FieldOfView / 180 * MathF.PI,
                aspectRatio, camera.NearPlaneDistance, camera.FarPlaneDistance);
        }
        else {
            state.Projection = Matrix4x4.CreateOrthographic(
                camera.OrthographicWidth / aspectRatio, camera.OrthographicWidth,
                camera.NearPlaneDistance, camera.FarPlaneDistance);
        }

        pars.Proj = state.Projection;
        Matrix4x4.Invert(pars.Proj, out pars.ProjInv);
        pars.ViewProj = pars.View * pars.Proj;
        pars.NearPlaneDistance = camera.NearPlaneDistance;
        pars.FarPlaneDistance = camera.FarPlaneDistance;

        unsafe {
            ref var mem = ref *(Camera3DParameters*)state.Pointer;
            mem.Proj = pars.Proj;
            mem.ProjInv = pars.ProjInv;
            mem.ViewProj = pars.ViewProj;
            mem.NearPlaneDistance = pars.NearPlaneDistance;
            mem.FarPlaneDistance = pars.FarPlaneDistance;
        }
    }

    private void UpdateCameraTransform(ref Camera3DState state, in Matrix4x4 view, in Vector3 position)
    {
        ref var pars = ref state.Parameters;
        pars.View = view;
        pars.ViewProj = pars.View * pars.Proj;
        pars.Position = position;

        unsafe {
            ref var mem = ref *(Camera3DParameters*)state.Pointer;
            mem.View = pars.View;
            mem.ViewProj = pars.ViewProj;
            mem.Position = pars.Position;
        }
    }
}