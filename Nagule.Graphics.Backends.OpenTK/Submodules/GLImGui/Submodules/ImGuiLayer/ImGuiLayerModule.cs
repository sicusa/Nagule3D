namespace Nagule.Graphics.Backends.OpenTK;

using CommunityToolkit.HighPerformance.Buffers;
using ImGuiNET;
using Nagule.Graphics.UI;
using Sia;

public class ImGuiLayerRenderSystem()
    : SystemBase(
        matcher: Matchers.Of<ImGuiLayer>())
{
    private ImGuiEventDispatcher _dispatcher = null!;

    public override void Initialize(World world, Scheduler scheduler)
    {
        base.Initialize(world, scheduler);
        _dispatcher = world.GetAddon<ImGuiEventDispatcher>();
    }

    public unsafe override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        foreach (var entity in query) {
            var imGuiCtx = entity.GetState<ImGuiContext>().Pointer;
            ImGui.SetCurrentContext(imGuiCtx);
            ImGui.Render();

            var drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0) {
                return;
            }

            ref var state = ref entity.GetState<ImGuiLayerState>();

            var screenScale = _dispatcher.ScreenScale;
            var drawLists = MemoryOwner<ImGuiDrawList>.Allocate(drawData.CmdListsCount);
            var darwListsSpan = drawLists.Span;

            var prevDrawLists = Interlocked.Exchange(ref state.DrawLists, drawLists);
            if (prevDrawLists != null) {
                foreach (ref var list in prevDrawLists.Span) {
                    list.Dispose();
                }
                prevDrawLists.Dispose();
            }

            for (int n = 0; n < drawData.CmdListsCount; ++n) {
                var pDrawList = drawData.CmdLists[n];
                int vtxBufferSize = pDrawList.VtxBuffer.Size;
                int idxBufferSize = pDrawList.IdxBuffer.Size;
                int cmdBufferSize = pDrawList.CmdBuffer.Size;

                var drawList = ImGuiDrawList.Create(vtxBufferSize, idxBufferSize, cmdBufferSize);
                var vtxSpan = drawList.VtxBuffer.Span;
                var idxSpan = drawList.IdxBuffer.Span;
                var cmdSpan = drawList.CmdBuffer.Span;

                for (int i = 0; i < vtxBufferSize; ++i) {
                    var v = pDrawList.VtxBuffer[i];
                    v.pos.X *= screenScale.X;
                    v.pos.Y *= screenScale.Y;
                    vtxSpan[i] = *v.NativePtr;
                }

                new Span<ushort>((void*)pDrawList.IdxBuffer.Data, idxBufferSize).CopyTo(idxSpan);

                for (int i = 0; i < cmdBufferSize; ++i) {
                    cmdSpan[i] = *pDrawList.CmdBuffer[i].NativePtr;
                }

                darwListsSpan[n] = drawList;
            }

            var io = ImGui.GetIO();
            drawData.ScaleClipRects(io.DisplayFramebufferScale);
        }
    }
}

public class ImGuiLayerModule()
    : AddonSystemBase(
        children: SystemChain.Empty
            .Add<ImGuiLayerRenderSystem>())
{
    public override void Initialize(World world, Scheduler scheduler)
    {
        base.Initialize(world, scheduler);
        AddAddon<ImGuiLayerManager>(world);
    }
}