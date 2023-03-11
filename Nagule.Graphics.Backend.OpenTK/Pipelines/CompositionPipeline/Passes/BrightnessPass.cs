namespace Nagule.Graphics.Backend.OpenTK;

public class BrightnessPass : CompositionPassBase
{
    public override IEnumerable<MaterialProperty> Properties { get; } 

    public override string EntryPoint { get; } = "Brightness";
    public override string Source { get; }
        = GraphicsHelper.LoadEmbededShader("nagule.pipeline.brightness.comp.glsl");

    public BrightnessPass(float value = 1f)
    {
        Properties = new MaterialProperty[] {
            new("Brightness_Value", value)
        };
    }
}