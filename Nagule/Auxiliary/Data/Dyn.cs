namespace Nagule;

using System.Collections.Immutable;

public abstract record Dyn
{
    public record Unit : Dyn;
    public static Unit UnitValue { get; } = new();

    public record Int(int Value) : Dyn;
    public record UInt(uint Value) : Dyn;
    public record Long(long Value) : Dyn;
    public record ULong(ulong Value) : Dyn;
    public record Bool(bool Value) : Dyn;
    public record Half(System.Half Value) : Dyn;
    public record Float(float Value) : Dyn;
    public record Double(double Value) : Dyn;

    public record Vector2(System.Numerics.Vector2 Value) : Dyn;
    public record Vector3(System.Numerics.Vector3 Value) : Dyn;
    public record Vector4(System.Numerics.Vector4 Value) : Dyn;

    public record DoubleVector2(double X, double Y) : Dyn;
    public record DoubleVector3(double X, double Y, double Z) : Dyn;
    public record DoubleVector4(double X, double Y, double Z, double W) : Dyn;

    public record IntVector2(int X, int Y) : Dyn;
    public record IntVector3(int X, int Y, int Z) : Dyn;
    public record IntVector4(int X, int Y, int Z, int W) : Dyn;

    public record UIntVector2(uint X, uint Y) : Dyn;
    public record UIntVector3(uint X, uint Y, uint Z) : Dyn;
    public record UIntVector4(uint X, uint Y, uint Z, uint W) : Dyn;

    public record BoolVector2(bool X, bool Y) : Dyn;
    public record BoolVector3(bool X, bool Y, bool Z) : Dyn;
    public record BoolVector4(bool X, bool Y, bool Z, bool W) : Dyn;

    public record Matrix4x4(System.Numerics.Matrix4x4 Value) : Dyn;
    public record Matrix4x3(ImmutableArray<float> Value) : Dyn;
    public record Matrix3x3(ImmutableArray<float> Value) : Dyn;
    public record Matrix3x2(System.Numerics.Matrix3x2 Value) : Dyn;
    public record Matrix2x2(ImmutableArray<float> Value) : Dyn;

    public record DoubleMatrix4x4(ImmutableArray<double> Value) : Dyn;
    public record DoubleMatrix4x3(ImmutableArray<double> Value) : Dyn;
    public record DoubleMatrix3x3(ImmutableArray<double> Value) : Dyn;
    public record DoubleMatrix3x2(ImmutableArray<double> Value) : Dyn;
    public record DoubleMatrix2x2(ImmutableArray<double> Value) : Dyn;

    public record String(string Value) : Dyn;
    public record StringMap(ImmutableDictionary<string, Dyn> map) : Dyn;

    public record Array(ImmutableArray<Dyn> Elements) : Dyn;

    public static Int From(int v) => new(v);
    public static UInt From(uint v) => new(v);
    public static Long From(long v) => new(v);
    public static ULong From(ulong v) => new(v);
    public static Bool From(bool v) => new(v);
    public static Half From(System.Half v) => new(v);
    public static Float From(float v) => new(v);
    public static Double From(double v) => new(v);

    public static Vector2 From(System.Numerics.Vector2 v) => new(v);
    public static Vector3 From(System.Numerics.Vector3 v) => new(v);
    public static Vector4 From(System.Numerics.Vector4 v) => new(v);

    public static Vector2 From(float x, float y) => new(new System.Numerics.Vector2(x, y));
    public static Vector3 From(float x, float y, float z) => new(new System.Numerics.Vector3(x, y, z));
    public static Vector4 From(float x, float y, float z, float w) => new(new System.Numerics.Vector4(x, y, z, w));

    public static DoubleVector2 From(double x, double y) => new(x, y);
    public static DoubleVector3 From(double x, double y, double z) => new(x, y, z);
    public static DoubleVector4 From(double x, double y, double z, double w) => new(x, y, z, w);

    public static IntVector2 From(int x, int y) => new(x, y);
    public static IntVector3 From(int x, int y, int z) => new(x, y, z);
    public static IntVector4 From(int x, int y, int z, int w) => new(x, y, z, w);

    public static BoolVector2 From(bool x, bool y) => new(x, y);
    public static BoolVector3 From(bool x, bool y, bool z) => new(x, y, z);
    public static BoolVector4 From(bool x, bool y, bool z, bool w) => new(x, y, z, w);

    public static Matrix4x4 From(System.Numerics.Matrix4x4 v) => new(v);
    public static Matrix3x2 From(System.Numerics.Matrix3x2 v) => new(v);

    public static String From(string v) => new(v);
    public static StringMap From(ImmutableDictionary<string, Dyn> v) => new(v);

    public static Array From(ImmutableArray<Dyn> v) => new(v);
}