namespace Nagule.Graphics.Backend.OpenTK;

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Sia;

public class OpenTKNativeWindow : NativeWindow
{
    public World World { get; }
    public EntityRef Peripheral { get; }
    public bool IsRunning { get; set; }

    private readonly ILogger _logger;

    private readonly SimulationFrame _simFrame;
    private readonly RenderFrame _renderFrame;

    private System.Numerics.Vector4 _clearColor;
    public bool IsDebugEnabled { get; }
    private readonly GLDebugProc? _debugProc;

    private bool _adaptiveUpdateFramePeriod;
    private double _updateFramePeriod;
    private readonly double _renderFramePeriod;

    private Thread? _renderThread;
    private volatile bool _isRunningSlowly;

    public OpenTKNativeWindow(World world, EntityRef peripheral)
        : this(world, peripheral,
            ref peripheral.Get<Window>(),
            ref peripheral.Get<SimulationContext>(),
            ref peripheral.Get<GraphicsContext>())
    {
    }

    private OpenTKNativeWindow(World world, EntityRef peripheral,
        ref Window window, ref SimulationContext simulation, ref GraphicsContext graphics)
        : base(
            new NativeWindowSettings {
                    Size = window.Size,
                    AutoLoadBindings = false,
                    MaximumSize = window.MaximumSize == null
                        ? null : new Vector2i(window.MaximumSize.Value.Item1, window.MaximumSize.Value.Item2),
                    MinimumSize = window.MinimumSize == null
                        ? null : new Vector2i(window.MinimumSize.Value.Item1, window.MinimumSize.Value.Item2),
                    Location = window.Location == null
                        ? null : new Vector2i(window.Location.Value.Item1, window.Location.Value.Item2),
                    Title = window.Title,
                    APIVersion = new Version(4, 1),
                    SrgbCapable = true,
                    RedBits = 16,
                    GreenBits = 16,
                    BlueBits = 16,
                    Flags = ContextFlags.ForwardCompatible,
                    WindowBorder = window.HasBorder
                        ? (window.IsResizable ? WindowBorder.Resizable : WindowBorder.Fixed)
                        : WindowBorder.Hidden,
                    WindowState = window.IsFullscreen
                        ? TKWindowState.Fullscreen
                        : TKWindowState.Normal
                })
    {
        GLLoader.LoadBindings(new GLFWBindingsContext());

        World = world;
        Peripheral = peripheral;

        _logger = world.GetAddon<LogLibrary>().Create<OpenTKNativeWindow>();
        _simFrame = world.GetAddon<SimulationFrame>();
        _renderFrame = world.GetAddon<RenderFrame>();

        var renderFreq = graphics.RenderFrequency ?? 60;
        _renderFramePeriod = renderFreq <= 0 ? 0 : 1 / renderFreq;

        var updateFreq = simulation.UpdateFrequency;
        if (updateFreq == null) {
            _adaptiveUpdateFramePeriod = true;
        }
        else {
            _updateFramePeriod = updateFreq.Value <= 0 ? 0 : 1 / updateFreq.Value;
        }

        _clearColor = graphics.ClearColor;

        VSync = graphics.VSyncMode switch {
            VSyncMode.On => TKVSyncMode.On,
            VSyncMode.Off => TKVSyncMode.Off,
            _ => TKVSyncMode.Adaptive
        };

        IsDebugEnabled = graphics.IsDebugEnabled;
        if (IsDebugEnabled) {
            _debugProc = DebugProc;
            GCHandle.Alloc(_debugProc);
        }
    }

    private void DebugProc(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
    {
        string messageStr = Marshal.PtrToStringAnsi(message, length);
        _logger.LogError("type={Type}, severity={Severity}, message={Message}", type, severity, messageStr);
    }

    private void OnLoad()
    {
        GL.ClearDepth(1f);
        GL.ClearColor(_clearColor.X, _clearColor.Y, _clearColor.Z, _clearColor.W);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.Disable(EnableCap.Blend);
        GL.DepthMask(true);

        if (IsDebugEnabled) {
            GL.DebugMessageCallback(_debugProc!, IntPtr.Zero);
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);
        }
    }

    public unsafe void Run()
    {
        IsRunning = true;

        Context?.MakeCurrent();
        OnLoad();
        OnResize(new ResizeEventArgs(Size));

        Context?.MakeNoneCurrent();
        _renderThread = new Thread(StartRenderThread);
        _renderThread.Start();

        var frameWatch = new Stopwatch();
        double elapsed;

        frameWatch.Start();

        ref var keyboard = ref Peripheral.Get<Keyboard>();
        ref var mouse = ref Peripheral.Get<Mouse>();

        while (!GLFW.WindowShouldClose(WindowPtr)) {
            elapsed = frameWatch.Elapsed.TotalSeconds;
            double sleepTime = _updateFramePeriod - elapsed;

            if (sleepTime > 0) {
                SpinWait.SpinUntil(() => true, (int)Math.Floor(sleepTime * 1000));
                continue;
            }

            frameWatch.Restart();

            keyboard.Frame = _simFrame.FrameCount;
            mouse.Frame = _simFrame.FrameCount;

            NewInputFrame();
            ProcessWindowEvents(IsEventDriven);
            DispatchUpdate(elapsed);
        }

        IsRunning = false;
    }

    private void DispatchUpdate(double elapsed)
    {
        _simFrame.Update((float)elapsed);
        ResetMouse();
    }

    private unsafe void StartRenderThread()
    {
        Context?.MakeCurrent();

        var frameWatch = new Stopwatch();
        double elapsed;

        frameWatch.Start();

        while (IsRunning) {
            elapsed = frameWatch.Elapsed.TotalSeconds;

            if (_adaptiveUpdateFramePeriod) {
                _updateFramePeriod = elapsed;
            }

            double sleepTime = _renderFramePeriod - elapsed;

            if (sleepTime > 0) {
                SpinWait.SpinUntil(() => true, (int)Math.Floor(sleepTime * 1000));
                continue;
            }
            if (!IsRunning) { return; }

            frameWatch.Restart();
            DispatchRender(elapsed);

            if (_renderFramePeriod != 0) {
                _isRunningSlowly = elapsed - _renderFramePeriod >= _renderFramePeriod;
            }
        }
    }

    private void DispatchRender(double elapsed)
    {
        _renderFrame.Update((float)elapsed);
        if (VSync == TKVSyncMode.Adaptive) {
            GLFW.SwapInterval(_isRunningSlowly ? 0 : 1);
        }
    }

    private void ResetMouse()
    {
        ref var mouse = ref Peripheral.Get<Mouse>();
        mouse.Delta = System.Numerics.Vector2.Zero;
    }

    protected override void OnRefresh()
    {
        ref var window = ref Peripheral.Get<Window>();
        var monitor = Monitors.GetMonitorFromWindow(this);
        var size = (monitor.HorizontalResolution, monitor.VerticalResolution);
        var scale = new System.Numerics.Vector2(monitor.HorizontalScale, monitor.VerticalScale);

        if (window.ScreenSize != size) {
            window.ScreenSize = size;
            World.Send(Peripheral, new Window.OnScreenSizeChanged(size));
        }
        if (window.ScreenScale != scale) {
            window.ScreenScale = scale;
            window.PhysicalSize = ((int)(window.Size.Item1 * scale.X), (int)(window.Size.Item2 * scale.Y));
            World.Send(Peripheral, new Window.OnScreenScaleChanged(scale));
        }
        World.Send(Peripheral, Window.OnRefresh.Instance);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        ref var window = ref Peripheral.Get<Window>();
        var size = (e.Width, e.Height);
        window.Size = size;

        var scale = window.ScreenScale;
        window.PhysicalSize = ((int)(window.Size.Item1 * scale.X), (int)(window.Size.Item2 * scale.Y));

        World.Send(Peripheral, new Window.OnSizeChanged(size));
    }

    protected override void OnMove(WindowPositionEventArgs e)
    {
        var location = (e.X, e.Y);
        Peripheral.Get<Window>().Location = location;
        World.Send(Peripheral, new Window.OnLocationChanged(location));
    }

    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        Peripheral.Get<Window>().IsFocused = e.IsFocused;
        World.Send(Peripheral, new Window.OnIsFocusedChanged(e.IsFocused));
    }

    protected override void OnMaximized(MaximizedEventArgs e)
    {
        Peripheral.Get<Window>().State = Nagule.WindowState.Maximized;
        World.Send(Peripheral, new Window.OnStateChanged(Nagule.WindowState.Maximized));
    }

    protected override void OnMinimized(MinimizedEventArgs e)
    {
        Peripheral.Get<Window>().State = Nagule.WindowState.Minimized;
        World.Send(Peripheral, new Window.OnStateChanged(Nagule.WindowState.Minimized));
    }

    protected override void OnMouseEnter()
    {
        Peripheral.Get<Mouse>().InWindow = true;
        World.Send(Peripheral, new Mouse.OnInWindowChanged(true));
    }

    protected override void OnMouseLeave()
    {
        Peripheral.Get<Mouse>().InWindow = false;
        World.Send(Peripheral, new Mouse.OnInWindowChanged(false));
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.Action == InputAction.Repeat) { return; }

        var button = (MouseButton)e.Button;
        ref var mouse = ref Peripheral.Get<Mouse>();
        ref var state = ref mouse.ButtonStates[button];

        state = new(true, mouse.Frame);
        World.Send(Peripheral, new Mouse.OnButtonStateChanged(button, state));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        var button = (MouseButton)e.Button;
        ref var mouse = ref Peripheral.Get<Mouse>();
        ref var state = ref mouse.ButtonStates[button];

        state = new(false, mouse.Frame);
        World.Send(Peripheral, new Mouse.OnButtonStateChanged(button, state));
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        ref var mouse = ref Peripheral.Get<Mouse>();
        mouse.Position = new(e.X, e.Y);
        World.Send(Peripheral, new Mouse.OnPositionChanged(mouse.Position));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ref var mouse = ref Peripheral.Get<Mouse>();
        mouse.WheelOffset = new(e.OffsetX, e.OffsetY);
        World.Send(Peripheral, new Mouse.OnWheelOffsetChanged(mouse.Position));
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.IsRepeat) { return; }

        var key = (Key)e.Key;
        ref var keyboard = ref Peripheral.Get<Keyboard>();
        ref var state = ref keyboard.KeyStates[key];

        state = new(true, keyboard.Frame);
        World.Send(Peripheral, new Keyboard.OnKeyStateChanged(key, state));
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        var key = (Key)e.Key;
        ref var keyboard = ref Peripheral.Get<Keyboard>();
        ref var state = ref keyboard.KeyStates[key];

        state = new(false, keyboard.Frame);
        World.Send(Peripheral, new Keyboard.OnKeyStateChanged(key, state));
    }

    protected override void OnTextInput(TextInputEventArgs e)
        => World.Send(Peripheral, new Window.OnTextInput((char)e.Unicode));

    protected override void OnFileDrop(FileDropEventArgs e)
        => World.Send(Peripheral, new Window.OnFileDrop(e.FileNames.ToImmutableArray()));

    protected override void OnJoystickConnected(JoystickEventArgs e)
        => World.Send(Peripheral, new Window.OnJoystickConnectionChanged(e.JoystickId, e.IsConnected));
}