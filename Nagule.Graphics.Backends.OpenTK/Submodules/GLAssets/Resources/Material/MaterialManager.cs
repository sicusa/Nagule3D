namespace Nagule.Graphics.Backends.OpenTK;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using Sia;

public partial class MaterialManager
{
    public override void OnInitialize(World world)
    {
        base.OnInitialize(world);
    
        static void RecreateShaderPrograms(
            World world, in EntityRef entity, ref MaterialReferences references, RGLSLProgram colorProgramAsset)
        {
            entity.Unrefer(references.ColorProgram);
            references.ColorProgramAsset = colorProgramAsset;
            references.ColorProgram = world.AcquireAsset(references.ColorProgramAsset, entity);

            var depthProgramAsset = CreateDepthShaderProgramAsset(colorProgramAsset);
            var depthProgram = world.AcquireAsset(depthProgramAsset, entity);

            entity.Unrefer(references.DepthProgram);
            references.DepthProgramAsset = depthProgramAsset;
            references.DepthProgram = depthProgram;
        }

        Listen((in EntityRef entity, ref Material snapshot, in Material.SetRenderMode cmd) => {
            var prevMode = snapshot.RenderMode;
            var mode = cmd.Value;

            var stateEntity = entity.GetStateEntity();
            ref var matRefs = ref stateEntity.Get<MaterialReferences>();

            var prevMacro = "RenderMode_" + Enum.GetName(prevMode);
            var macro = "RenderMode_" + Enum.GetName(mode);

            RecreateShaderPrograms(world, entity, ref matRefs, matRefs.ColorProgramAsset with {
                Macros = matRefs.ColorProgramAsset.Macros
                    .Remove(prevMacro)
                    .Add(macro)
            });

            var colorProgramState = matRefs.ColorProgram.GetStateEntity();
            var depthProgramState = matRefs.DepthProgram.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();
                state.RenderMode = mode;
                state.ColorProgramState = colorProgramState;
                state.DepthProgramState = depthProgramState;
            });
        });

        Listen((in EntityRef entity, ref Material snapshot, in Material.SetLightingMode cmd) => {
            var mode = cmd.Value;

            var stateEntity = entity.GetStateEntity();
            ref var matRefs = ref stateEntity.Get<MaterialReferences>();

            RecreateShaderPrograms(world, entity, ref matRefs, matRefs.ColorProgramAsset with {
                Macros = matRefs.ColorProgramAsset.Macros
                    .Remove("LightingMode_" + Enum.GetName(snapshot.LightingMode))
                    .Add("LightingMode_" + Enum.GetName(mode))
            });

            var colorProgramState = matRefs.ColorProgram.GetStateEntity();
            var depthProgramState = matRefs.DepthProgram.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();
                state.LightingMode = mode;
                state.ColorProgramState = colorProgramState;
                state.DepthProgramState = depthProgramState;
            });
        });

        Listen((in EntityRef entity, in Material.SetIsTwoSided cmd) => {
            var value = cmd.Value;
            var stateEntity = entity.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();
                state.IsTwoSided = value;
            });
        });

        Listen((in EntityRef entity, in Material.SetShaderProgram cmd) => {
            ref var material = ref entity.Get<Material>();
            ref var matRefs = ref entity.GetState<MaterialReferences>();

            RecreateShaderPrograms(world, entity, ref matRefs,
                TransformMaterialShaderProgramAsset(material));

            var stateEntity = entity.GetStateEntity();
            var colorProgramState = matRefs.ColorProgram.GetStateEntity();
            var depthProgramState = matRefs.DepthProgram.GetStateEntity();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();
                state.ColorProgramState = colorProgramState;
                state.DepthProgramState = depthProgramState;
            });
        });

        Listen((in EntityRef entity, in Material.SetProperties cmd) => {
            var props = cmd.Value;

            ref var matRefs = ref entity.GetState<MaterialReferences>();
            ref var textures = ref matRefs.Textures;

            if (textures != null) {
                foreach (var texture in textures.Values) {
                    entity.Unrefer(texture);
                }
                textures.Clear();
            }

            List<(string, EntityRef)>? newTexStates = null;

            if (props.Count != 0) {
                foreach (var (propName, dyn) in props) {
                    if (TryLoadTexture(entity, propName, dyn, out var texEntity)) {
                        textures ??= [];
                        textures.Add(propName, texEntity);
                    }
                }
            }

            if (textures != null) {
                newTexStates ??= [];
                foreach (var (propName, texEntity) in newTexStates.AsSpan()) {
                    newTexStates.Add((propName, texEntity.GetStateEntity()));
                }
            }

            var stateEntity = entity.GetStateEntity();
            var displayName = entity.GetDisplayName();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();

                var texStates = state.TextureStates;
                texStates?.Clear();

                if (newTexStates != null) {
                    texStates ??= [];
                    foreach (var (propName, texEntity) in newTexStates.AsSpan()) {
                        texStates.Add(propName, texEntity);
                    }
                }

                var colorProgramState = state.ColorProgramState;
                var pointer = state.Pointer;

                ref var programState = ref colorProgramState.Get<GLSLProgramState>();
                if (!programState.Loaded) {
                    return false;
                }
                unsafe {
                    new Span<byte>((void*)pointer, programState.MaterialBlockSize).Clear();
                }
                SetMaterialParameters(displayName, pointer, programState, props);
                return true;
            });
        });

        void SetProperty(EntityRef entity, string name, Dyn value)
        {
            ref var matRefs = ref entity.GetState<MaterialReferences>();
            var textures = matRefs.Textures;

            if (textures != null && textures.Remove(name, out var prevTexEntity)) {
                entity.Unrefer(prevTexEntity);
            }

            bool isTexture = false;
            if (TryLoadTexture(entity, name, value, out var texEntity)) {
                textures ??= [];
                textures.Add(name, texEntity);
                isTexture = true;
            }

            var stateEntity = entity.GetStateEntity();
            var texState = texEntity.GetStateEntity();
            var displayName = entity.GetDisplayName();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();
                ref var programState = ref state.ColorProgramState.Get<GLSLProgramState>();

                if (!programState.Loaded) {
                    return false;
                }
                if (isTexture) {
                    state.TextureStates ??= [];
                    state.TextureStates[name] = texState;
                }
                SetMaterialParameter(
                    displayName, state.Pointer, programState.Parameters, name, value);
                return true;
            });
        }

        Listen((in EntityRef entity, in Material.AddProperty cmd) => SetProperty(entity, cmd.Key, cmd.Value));
        Listen((in EntityRef entity, in Material.SetProperty cmd) => SetProperty(entity, cmd.Key, cmd.Value));
        Listen((in EntityRef entity, in Material.RemoveProperty cmd) => {
            var name = cmd.Key;

            ref var matRefs = ref entity.GetState<MaterialReferences>();
            var textures = matRefs.Textures;

            bool isTexture = false;
            if (textures != null && textures.Remove(name, out var texEntity)) {
                entity.Unrefer(texEntity);
                isTexture = true;
            }

            var stateEntity = entity.GetStateEntity();
            var displayName = entity.GetDisplayName();

            RenderFramer.Enqueue(entity, () => {
                ref var state = ref stateEntity.Get<MaterialState>();
                ref var programState = ref state.ColorProgramState.Get<GLSLProgramState>();

                if (programState.Handle == ProgramHandle.Zero) {
                    return false;
                }
                
                if (isTexture) {
                    state.TextureStates!.Remove(name);
                }

                ClearMaterialParameter(
                    displayName, state.Pointer, programState.Parameters, name);
                return true;
            });
        });
    }

    public override void LoadAsset(in EntityRef entity, ref Material asset, EntityRef stateEntity)
    {
        var colorProgramAsset = CreateColorShaderProgramAsset(entity, asset, out var textures);
        var colorProgram = World.AcquireAsset(colorProgramAsset, entity);
        var colorProgramState = colorProgram.GetStateEntity();

        var depthProgramAsset = CreateDepthShaderProgramAsset(colorProgramAsset);
        var depthProgram = World.AcquireAsset(depthProgramAsset, entity);
        var depthProgramState = depthProgram.GetStateEntity();

        ref var matRefs = ref stateEntity.Get<MaterialReferences>();
        matRefs.Textures = textures;
        matRefs.ColorProgram = colorProgram;
        matRefs.ColorProgramAsset = colorProgramAsset;
        matRefs.DepthProgram = depthProgram;
        matRefs.DepthProgramAsset = depthProgramAsset;

        var name = entity.GetDisplayName();
        var renderMode = asset.RenderMode;
        var lightingMode = asset.LightingMode;

        var isTwoSided = asset.IsTwoSided;
        var isShadowCaster = asset.IsShadowCaster;
        var IsShadowReceiver = asset.IsShadowReceiver;

        var properties = asset.Properties;
        var texStates = textures?
            .Select(t => KeyValuePair.Create(t.Key, t.Value.GetStateEntity()))
            .ToDictionary();

        RenderFramer.Enqueue(entity, () => {
            ref var programState = ref colorProgramState.Get<GLSLProgramState>();
            if (!programState.Loaded) {
                return false;
            }

            ref var state = ref stateEntity.Get<MaterialState>();
            state = new MaterialState {
                ColorProgramState = colorProgramState,
                DepthProgramState = depthProgramState,
                UniformBufferHandle = new(GL.GenBuffer()),
                RenderMode = renderMode,
                LightingMode = lightingMode,
                IsTwoSided = isTwoSided,
                IsShadowCaster = isShadowCaster,
                IsShadowReceiver = IsShadowReceiver,
                TextureStates = texStates
            };

            GL.BindBuffer(BufferTargetARB.UniformBuffer, state.UniformBufferHandle.Handle);
            state.Pointer = GLUtils.InitializeBuffer(
                BufferTargetARB.UniformBuffer, programState.MaterialBlockSize);

            SetMaterialParameters(name ?? "(no name)", state, programState, properties);
            return true;
        });
    }

    public override void UnloadAsset(in EntityRef entity, in Material asset, EntityRef stateEntity)
    {
        RenderFramer.Enqueue(entity, () => {
            ref var state = ref stateEntity.Get<MaterialState>();
            GL.DeleteBuffer(state.UniformBufferHandle.Handle);
        });
    }

    private void SetMaterialParameters(
        string name, in MaterialState state, in GLSLProgramState programState, ImmutableDictionary<string, Dyn> properties)
        => SetMaterialParameters(name, state.Pointer, programState, properties);

    private void SetMaterialParameters(
        string name, nint pointer, in GLSLProgramState programState, ImmutableDictionary<string, Dyn> properties)
    {
        var pars = programState.Parameters;
        foreach (var (propName, value) in properties) {
            SetMaterialParameter(name, pointer, pars, propName, value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetMaterialParameter(
        string name, nint pointer, FrozenDictionary<string, ShaderParameterEntry>? parameters, string propName, Dyn value)
    {
        if (parameters == null || !parameters.TryGetValue(propName, out var entry)) {
            Logger.LogWarning("[{Name}] Unrecognized property '{Property}' in material, skip.", name, propName);
            return;
        }
        if (!ShaderUtils.SetParameter(pointer + entry.Offset, entry.Type, value)) {
            Logger.LogError("[{Name}] Parameter '{Parameter}' requires type {ParameterType} that does not match with actual type {ActualType}.",
                name, propName, Enum.GetName(entry.Type), value.GetType());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearMaterialParameter(
        string name, nint pointer, FrozenDictionary<string, ShaderParameterEntry>? parameters, string propName)
    {
        if (parameters == null || !parameters.TryGetValue(propName, out var entry)) {
            Logger.LogWarning("[{Name}] Unrecognized property '{Property}' in material, skip.", name, propName);
            return;
        }
        ShaderUtils.ClearParameter(pointer + entry.Offset, entry.Type);
    }

    private bool TryLoadTexture(in EntityRef entity, string propName, Dyn dyn, out EntityRef resultTexEntity)
    {
        if (dyn is not TextureDyn textureDyn || textureDyn.Value == null) {
            resultTexEntity = default;
            return false;
        }
        try {
            resultTexEntity = World.AcquireAsset(textureDyn.Value, entity);
        }
        catch (Exception e) {
            Logger.LogError("[{Name}] Failed to create texture entity for property '{Property}': {Message}",
                entity.GetDisplayName(), propName, e.Message);
            resultTexEntity = default;
            return false;
        }
        return true;
    }

    public RGLSLProgram CreateColorShaderProgramAsset(
        EntityRef entity, in Material material, out Dictionary<string, EntityRef>? resultTextures)
    {
        Dictionary<string, EntityRef>? textures = null;

        var program = TransformMaterialShaderProgramAsset(
            material, (world, name, value) => {
                if (TryLoadTexture(entity, name, value, out var texEntity)) {
                    textures ??= [];
                    textures.Add(name, texEntity);
                }
            }
        );

        resultTextures = textures;
        return program;
    }

    public static RGLSLProgram CreateDepthShaderProgramAsset(RGLSLProgram colorProgramAsset)
        => colorProgramAsset with {
            Macros = [
                colorProgramAsset.Macros.Contains("RenderMode_Opaque")
                    ? "RenderMode_Opaque" : "RenderMode_Cutoff",
                "LightingMode_Unlit",
                ..colorProgramAsset.Macros.Where(m => m.StartsWith('_'))
            ],
        };

    public RGLSLProgram TransformMaterialShaderProgramAsset(
        in Material material, Action<World, string, Dyn>? propertyHandler = null)
    {
        var program = material.ShaderProgram;
        var renderMode = material.RenderMode;
        var lightingMode = material.LightingMode;
        var props = material.Properties;

        var macros = program.Macros.ToBuilder();
        macros.Add("RenderMode_" + Enum.GetName(renderMode));
        macros.Add("LightingMode_" + Enum.GetName(lightingMode));

        if (material.IsShadowCaster) { macros.Add("_IsShadowCaster"); }
        if (material.IsShadowReceiver) { macros.Add("_IsShadowReceiver"); }
        
        if (props.Count == 0) {
            return program with { Macros = macros.ToImmutable() };
        }

        var programPars = program.Parameters;
        if (propertyHandler != null) {
            foreach (var (name, value) in props) {
                if (!programPars.ContainsKey(name)) {
                    continue;
                }
                macros.Add("_" + name);
                try {
                    propertyHandler(World, name, value);
                }
                catch (Exception e) {
                    Logger.LogError("Uncaught execption when handling material property: {Message}", e);
                }
            }
        }
        else {
            foreach (var name in props.Keys) {
                if (!programPars.ContainsKey(name)) {
                    continue;
                }
                macros.Add("_" + name);
            }
        }

        return program with { Macros = macros.ToImmutable() };
    }
}