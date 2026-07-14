using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using LuckySplit.Models;

namespace LuckySplit;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 6;

    // Compatibility defaults used when creating the first venue preset.
    public string DefaultVenueName { get; set; } = "Everbloom";
    public int DefaultTicketPrice { get; set; } = 100_000;
    public int DefaultWinnerPercent { get; set; } = 50;
    public int DefaultMaxTicketsPerPlayer { get; set; }

    public bool HasSeenWelcomeSplash { get; set; }
    public bool HasCompletedPresetSetup { get; set; }
    public Guid SelectedPresetId { get; set; }
    public List<VenuePreset> Presets { get; set; } = new();

    public DrawingRecord CurrentDrawing { get; set; } = new();
    public List<DrawingRecord> History { get; set; } = new();

    public bool RequiresPresetSetup => !HasCompletedPresetSetup || Presets.Count == 0;

    public VenuePreset? GetSelectedPreset()
    {
        var selected = Presets.FirstOrDefault(p => p.Id == SelectedPresetId);
        return selected ?? Presets.FirstOrDefault();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
