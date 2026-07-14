using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace LuckySplit.Windows;

internal static class LuckySplitTheme
{
    public static readonly Vector4 Window = new(0.035f, 0.047f, 0.078f, 0.98f);
    public static readonly Vector4 Panel = new(0.065f, 0.082f, 0.125f, 1f);
    public static readonly Vector4 PanelRaised = new(0.085f, 0.105f, 0.155f, 1f);
    public static readonly Vector4 PanelHover = new(0.105f, 0.135f, 0.195f, 1f);
    public static readonly Vector4 Border = new(0.19f, 0.23f, 0.32f, 1f);
    public static readonly Vector4 BorderSoft = new(0.14f, 0.17f, 0.24f, 1f);
    public static readonly Vector4 Text = new(0.91f, 0.93f, 0.97f, 1f);
    public static readonly Vector4 Muted = new(0.54f, 0.59f, 0.70f, 1f);
    public static readonly Vector4 Gold = new(0.88f, 0.70f, 0.34f, 1f);
    public static readonly Vector4 GoldHover = new(0.98f, 0.79f, 0.40f, 1f);
    public static readonly Vector4 Teal = new(0.29f, 0.82f, 0.77f, 1f);
    public static readonly Vector4 Violet = new(0.65f, 0.47f, 0.88f, 1f);
    public static readonly Vector4 Success = new(0.36f, 0.83f, 0.54f, 1f);
    public static readonly Vector4 Danger = new(0.91f, 0.34f, 0.38f, 1f);

    private const int ColorCount = 24;
    private const int VarCount = 7;

    public static void Push()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Window);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Window);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Border);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, Muted);
        ImGui.PushStyleColor(ImGuiCol.Button, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Border);
        ImGui.PushStyleColor(ImGuiCol.Header, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Border);
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, Border);
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, BorderSoft);
        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(9f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(9f, 8f));
    }

    public static void Pop()
    {
        ImGui.PopStyleVar(VarCount);
        ImGui.PopStyleColor(ColorCount);
    }
}
