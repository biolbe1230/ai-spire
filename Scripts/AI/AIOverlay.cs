using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace AISpire.AI;

/// <summary>
/// 游戏内 AI 决策理由覆盖层
/// </summary>
public static class AIOverlay
{
    private static CanvasLayer? _canvas;
    private static RichTextLabel? _label;
    private static bool _initialized;
    private static readonly Queue<string> _messages = new();
    private const int MaxMessages = 10;

    public static void Init()
    {
        if (_initialized) return;

        try
        {
            _canvas = new CanvasLayer();
            _canvas.Layer = 100;

            // 背景面板
            var panel = new PanelContainer();
            panel.AnchorLeft = 0;
            panel.AnchorTop = 1;
            panel.AnchorRight = 0;
            panel.AnchorBottom = 1;
            panel.OffsetLeft = 8;
            panel.OffsetTop = -280;
            panel.OffsetRight = 480;
            panel.OffsetBottom = -8;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0f, 0f, 0f, 0.72f);
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            panel.AddThemeStyleboxOverride("panel", style);

            // 文本标签
            _label = new RichTextLabel();
            _label.BbcodeEnabled = true;
            _label.FitContent = false;
            _label.ScrollFollowing = true;
            _label.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
            _label.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
            _label.AddThemeColorOverride("default_color", Colors.White);
            _label.AddThemeFontSizeOverride("normal_font_size", 13);

            panel.AddChild(_label);
            _canvas.AddChild(panel);

            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                tree.Root.CallDeferred(Node.MethodName.AddChild, _canvas);
                _initialized = true;
                Log.Debug("[AISpire] Overlay initialized");
            }
            else
            {
                Log.Debug("[AISpire] SceneTree not available for overlay");
            }
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] Overlay init error: {e.Message}");
        }
    }

    public static void AddMessage(string action, string reasoning)
    {
        if (!_initialized || _label == null) return;

        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var msg = $"[color=gray]{timestamp}[/color] [color=yellow]{action}[/color] {reasoning}";
            _messages.Enqueue(msg);
            while (_messages.Count > MaxMessages)
                _messages.Dequeue();

            var text = "[b][color=cyan]🤖 AI 决策日志[/color][/b]\n" + string.Join("\n", _messages);
            _label.CallDeferred(RichTextLabel.MethodName.SetText, text);
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] Overlay update error: {e.Message}");
        }
    }

    public static void SetStatus(string status)
    {
        if (!_initialized || _label == null) return;

        try
        {
            var text = "[b][color=cyan]🤖 AI 决策日志[/color][/b]\n"
                + string.Join("\n", _messages)
                + $"\n[color=gray]{status}[/color]";
            _label.CallDeferred(RichTextLabel.MethodName.SetText, text);
        }
        catch { }
    }
}
