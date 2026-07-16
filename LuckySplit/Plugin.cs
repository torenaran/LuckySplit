using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LuckySplit.Models;
using LuckySplit.Services;
using LuckySplit.Windows;

namespace LuckySplit;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    private static readonly string[] Commands =
    {
        "/luckysplit",
        "/lucky",
        "/5050",
    };

    public Configuration Configuration { get; }
    public DrawingService DrawingService { get; } = new();
    public WindowSystem WindowSystem { get; } = new("LuckySplit");
    public MainWindow MainWindow { get; }

    public Plugin()
    {
        Configuration = LoadConfiguration();
        Configuration.Presets ??= new();
        Configuration.History ??= new();

        if (Configuration.Version < 8)
        {
            MigrateDrawing(Configuration.CurrentDrawing);
            foreach (var drawing in Configuration.History)
                MigrateDrawing(drawing);
        }

        Configuration.Version = Math.Max(Configuration.Version, 8);

        if (Configuration.SelectedPresetId == Guid.Empty && Configuration.Presets.Count > 0)
            Configuration.SelectedPresetId = Configuration.Presets[0].Id;

        Configuration.CurrentDrawing ??= DrawingService.CreateDraft(Configuration);

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        foreach (var command in Commands)
        {
            CommandManager.AddHandler(command, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Lucky Split's 50/50 drawing manager.",
            });
        }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
    }

    public void Dispose()
    {
        Configuration.Save();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        foreach (var command in Commands)
            CommandManager.RemoveHandler(command);

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
    }

    public void ArchiveAndCreateNew()
    {
        var current = Configuration.CurrentDrawing;
        if (current.Status is not DrawingStatus.Draft || current.Purchases.Count > 0)
            Configuration.History.Insert(0, current);

        Configuration.CurrentDrawing = DrawingService.CreateDraft(Configuration);
        Configuration.Save();
    }

    private static Configuration LoadConfiguration()
    {
        return PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    }

    private void MigrateDrawing(DrawingRecord? drawing)
    {
        if (drawing is null)
            return;

        drawing.Purchases ??= new();

        if (string.IsNullOrWhiteSpace(drawing.ProtocolVersion))
        {
            drawing.ProtocolVersion = drawing.Status == DrawingStatus.Draft && drawing.Purchases.Count == 0
                ? "LSPLIT3"
                : "LSPLIT1";
        }
        else if (drawing.Status == DrawingStatus.Draft && drawing.Purchases.Count == 0)
        {
            drawing.ProtocolVersion = "LSPLIT3";
        }

        DrawingService.NormalizeCollaborationMetadata(drawing);
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    private void ToggleMainUi() => MainWindow.Toggle();
}
