using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using LuckySplit.Models;
using LuckySplit.Services;

namespace LuckySplit.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private enum MainSection
    {
        Drawing,
        Presets,
        History,
        Settings,
    }

    private const string BannerResource = "LuckySplit.Assets.LuckySplitBanner.png";
    private const string MarkResource = "LuckySplit.Assets.LuckySplitMark.png";
    private const string AccentResource = "LuckySplit.Assets.LuckySplitAccent.png";
    private const string SplashResource = "LuckySplit.Assets.LuckySplitSplash.png";

    private const string CloseSalesPopup = "Close ticket sales?##LuckySplitCloseSales";
    private const string DrawWinnerPopup = "Draw the winning ticket?##LuckySplitDrawWinner";
    private const string VoidDrawingPopup = "Void this drawing?##LuckySplitVoidDrawing";
    private const string ArchiveDrawingPopup = "Archive this drawing?##LuckySplitArchiveDrawing";

    private static readonly Vector4 Gold = new(0.93f, 0.73f, 0.28f, 1f);
    private static readonly Vector4 GoldMuted = new(0.72f, 0.57f, 0.27f, 1f);
    private static readonly Vector4 Success = new(0.35f, 0.82f, 0.52f, 1f);
    private static readonly Vector4 Danger = new(0.92f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 Muted = new(0.62f, 0.64f, 0.70f, 1f);
    private static readonly Vector4 Purple = new(0.72f, 0.52f, 0.95f, 1f);

    private static readonly float[] NearbyPlayerRanges = { 20f, 50f, 100f, float.MaxValue };
    private static readonly string[] NearbyPlayerRangeLabels = { "20 yalms", "50 yalms", "100 yalms", "All loaded" };

    private readonly Plugin plugin;
    private readonly ISharedImmediateTexture bannerTexture;
    private readonly ISharedImmediateTexture markTexture;
    private readonly ISharedImmediateTexture accentTexture;
    private readonly ISharedImmediateTexture splashTexture;

    private MainSection activeSection = MainSection.Drawing;
    private Guid selectedHistoryId = Guid.Empty;
    private string historySearch = string.Empty;
    private string presetSearch = string.Empty;

    private string buyerName = string.Empty;
    private string buyerWorld = string.Empty;
    private int ticketQuantity = 1;
    private string statusMessage = string.Empty;
    private string lastExportPath = string.Empty;
    private string ledgerSearch = string.Empty;
    private string nearbyPlayerSearch = string.Empty;
    private readonly List<NearbyPlayerOption> nearbyPlayers = new();
    private DateTime nextNearbyPlayerRefreshUtc = DateTime.MinValue;
    private int nearbyPlayerRangeIndex = 1;

    private string onboardingPresetName = string.Empty;
    private string onboardingVenueName = string.Empty;
    private string onboardingDrawingTitle = string.Empty;
    private int onboardingTicketPrice = 100_000;
    private int onboardingWinnerPercent = 50;
    private int onboardingPlayerLimit;

    private Guid presetEditorId = Guid.Empty;
    private bool creatingPreset;
    private Guid deleteArmedPresetId = Guid.Empty;
    private string presetName = string.Empty;
    private string presetVenueName = string.Empty;
    private string presetDrawingTitle = string.Empty;
    private int presetTicketPrice = 100_000;
    private int presetWinnerPercent = 50;
    private int presetPlayerLimit;

    public MainWindow(Plugin plugin)
        : base("Lucky Split##LuckySplitMain")
    {
        this.plugin = plugin;
        var assembly = typeof(MainWindow).Assembly;
        bannerTexture = Plugin.TextureProvider.GetFromManifestResource(assembly, BannerResource);
        markTexture = Plugin.TextureProvider.GetFromManifestResource(assembly, MarkResource);
        accentTexture = Plugin.TextureProvider.GetFromManifestResource(assembly, AccentResource);
        splashTexture = Plugin.TextureProvider.GetFromManifestResource(assembly, SplashResource);

        AllowBackgroundBlur = true;
        Size = new Vector2(1120, 780);
        SizeCondition = ImGuiCond.FirstUseEver;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        LoadFirstRunDefaults();
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        LuckySplitTheme.Push();
    }

    public override void PostDraw()
    {
        LuckySplitTheme.Pop();
    }

    public override void Draw()
    {
        if (!plugin.Configuration.HasSeenWelcomeSplash)
        {
            DrawWelcomeSplash();
            return;
        }

        DrawHeroBanner();

        if (plugin.Configuration.RequiresPresetSetup)
        {
            DrawFirstRunSetup();
            return;
        }

        DrawControlStrip();
        DrawNavigation();
        ImGui.Spacing();

        switch (activeSection)
        {
            case MainSection.Drawing:
                DrawSectionHeading("DRAWING CONSOLE", "Run tonight's setup, ticket sales, review, and winner flow.");
                DrawActiveDrawing();
                break;
            case MainSection.Presets:
                DrawSectionHeading("VENUE PRESETS", "Switch between recurring venue setups without re-entering every rule.");
                DrawPresets();
                break;
            case MainSection.History:
                DrawSectionHeading("DRAWING ARCHIVE", "Review completed and voided drawings, receipts, and exports.");
                DrawHistory();
                break;
            case MainSection.Settings:
                DrawSectionHeading("TOOLS & INFORMATION", "Commands, safety notes, file locations, and plugin information.");
                DrawAbout();
                break;
        }

        DrawFooterToolbar();
    }

    private void DrawHeroBanner()
    {
        var texture = bannerTexture.GetWrapOrEmpty();
        var availableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        var ratio = texture.Width > 0 ? (float)texture.Height / texture.Width : 0.1875f;
        var height = Math.Clamp(availableWidth * ratio, 112f, 190f);
        ImGui.Image(texture.Handle, new Vector2(availableWidth, height));
    }

    private void DrawWelcomeSplash()
    {
        var texture = splashTexture.GetWrapOrEmpty();
        var availableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        var ratio = texture.Width > 0 ? (float)texture.Height / texture.Width : 0.5625f;
        var imageHeight = Math.Min(availableWidth * ratio, 610f);

        ImGui.Image(texture.Handle, new Vector2(availableWidth, imageHeight));
        ImGui.Spacing();

        var setupRequired = plugin.Configuration.RequiresPresetSetup;
        var message = setupRequired
            ? "Welcome. Lucky Split will begin by creating your first reusable venue preset."
            : "Welcome back. Your existing presets and drawing records are ready.";
        var messageSize = ImGui.CalcTextSize(message);
        ImGui.SetCursorPosX(Math.Max(0f, (ImGui.GetContentRegionAvail().X - messageSize.X) / 2f));
        ImGui.TextColored(LuckySplitTheme.Muted, message);
        ImGui.Spacing();

        var buttonLabel = setupRequired ? "Begin Venue Setup" : "Open Lucky Split";
        var buttonSize = new Vector2(220, 42);
        ImGui.SetCursorPosX(Math.Max(0f, (ImGui.GetContentRegionAvail().X - buttonSize.X) / 2f));
        if (PrimaryButton(buttonLabel, buttonSize))
        {
            plugin.Configuration.HasSeenWelcomeSplash = true;
            plugin.Configuration.Save();
        }
    }

    private void DrawControlStrip()
    {
        var drawing = plugin.Configuration.CurrentDrawing;
        var preset = plugin.Configuration.GetSelectedPreset();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild("LuckySplitControlStrip", new Vector2(0, 52), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.TextColored(LuckySplitTheme.Teal, "ACTIVE PRESET");
            ImGui.SameLine();
            ImGui.TextUnformatted(preset?.DisplayName ?? "No preset selected");
            ImGui.SameLine();
            ImGui.TextDisabled("  •  ");
            ImGui.SameLine();
            ImGui.TextColored(LuckySplitTheme.Gold, drawing.VenueName);
            ImGui.SameLine();
            ImGui.TextDisabled("  •  ");
            ImGui.SameLine();
            ImGui.TextUnformatted(drawing.Title);

            var status = GetStatusLabel(drawing.Status);
            var statusWidth = ImGui.CalcTextSize(status).X + 30f;
            ImGui.SameLine(Math.Max(ImGui.GetCursorPosX() + 20f, ImGui.GetContentRegionMax().X - statusWidth));
            DrawStatusPill(status, GetStatusColor(drawing.Status));
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawNavigation()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.Window);
        if (ImGui.BeginChild("LuckySplitNavigation", new Vector2(0, 48), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            DrawNavButton(MainSection.Drawing, "DRAWING", plugin.Configuration.CurrentDrawing.TotalTickets > 0
                ? plugin.Configuration.CurrentDrawing.TotalTickets.ToString("N0")
                : null);
            ImGui.SameLine();
            DrawNavButton(MainSection.Presets, "PRESETS", plugin.Configuration.Presets.Count.ToString("N0"));
            ImGui.SameLine();
            DrawNavButton(MainSection.History, "HISTORY", plugin.Configuration.History.Count.ToString("N0"));
            ImGui.SameLine();
            DrawNavButton(MainSection.Settings, "TOOLS", null);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        DrawAccentRule();
    }

    private void DrawNavButton(MainSection section, string label, string? count)
    {
        var selected = activeSection == section;
        ImGui.PushStyleColor(ImGuiCol.Button, selected ? LuckySplitTheme.PanelRaised : LuckySplitTheme.Window);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, LuckySplitTheme.PanelHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, LuckySplitTheme.PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.Text, selected ? LuckySplitTheme.Gold : LuckySplitTheme.Muted);

        var text = string.IsNullOrWhiteSpace(count) ? label : $"{label}   {count}";
        if (ImGui.Button($"{text}##Nav{section}", new Vector2(145, 38)))
            activeSection = section;

        ImGui.PopStyleColor(4);
    }

    private void DrawSectionHeading(string title, string subtitle)
    {
        ImGui.TextColored(LuckySplitTheme.Text, title);
        ImGui.TextDisabled(subtitle);
        ImGui.Spacing();
    }

    private void DrawAccentRule()
    {
        var texture = accentTexture.GetWrapOrEmpty();
        ImGui.Image(texture.Handle, new Vector2(ImGui.GetContentRegionAvail().X, 4f));
    }

    private void DrawStatusPill(string label, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(color.X * 0.38f, color.Y * 0.38f, color.Z * 0.38f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(color.X * 0.38f, color.Y * 0.38f, color.Z * 0.38f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(color.X * 0.38f, color.Y * 0.38f, color.Z * 0.38f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.Button($"{label}##StatusPill", new Vector2(0, 28));
        ImGui.PopStyleColor(4);
    }

    private void DrawFooterToolbar()
    {
        ImGui.Spacing();
        DrawAccentRule();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild("LuckySplitFooter", new Vector2(0, 48), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var drawing = plugin.Configuration.CurrentDrawing;

            if (ImGui.Button("DRAWING"))
                activeSection = MainSection.Drawing;
            ImGui.SameLine();
            if (ImGui.Button("PRESETS"))
                activeSection = MainSection.Presets;
            ImGui.SameLine();
            if (ImGui.Button("EXPORT") && drawing.Purchases.Count > 0)
                ExportCsv(drawing);
            ImGui.SameLine();
            if (ImGui.Button("REFRESH PLAYERS") && drawing.Status == DrawingStatus.Open)
            {
                nextNearbyPlayerRefreshUtc = DateTime.MinValue;
                RefreshNearbyPlayers();
                statusMessage = $"Nearby player list refreshed: {nearbyPlayers.Count:N0} loaded player(s).";
            }

            const string versionText = "LUCKY SPLIT  v1.0.0";
            var versionWidth = ImGui.CalcTextSize(versionText).X;
            ImGui.SameLine(Math.Max(ImGui.GetCursorPosX() + 20f, ImGui.GetContentRegionMax().X - versionWidth));
            ImGui.TextDisabled(versionText);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawBrandHeader(DrawingRecord drawing)
    {
        if (ImGui.BeginTable("LuckySplitHeader", 2, ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Brand");
            ImGui.TableSetupColumn("Status");
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextColored(Gold, "LUCKY SPLIT");
            ImGui.SameLine();
            ImGui.TextDisabled("50/50 drawing manager");

            ImGui.TableSetColumnIndex(1);
            ImGui.TextColored(GetStatusColor(drawing.Status), GetStatusLabel(drawing.Status));

            ImGui.EndTable();
        }

        var venueLine = string.IsNullOrWhiteSpace(drawing.VenueName)
            ? "Choose a venue preset to begin"
            : $"{drawing.VenueName}  •  {drawing.Title}";
        ImGui.TextWrapped(venueLine);
        ImGui.Separator();
    }

    private void DrawActiveDrawing()
    {
        var drawing = plugin.Configuration.CurrentDrawing;

        DrawWorkflow(drawing.Status);
        ImGui.Spacing();

        switch (drawing.Status)
        {
            case DrawingStatus.Draft:
                DrawDraft(drawing);
                break;
            case DrawingStatus.Open:
                DrawOpen(drawing);
                break;
            case DrawingStatus.Closed:
                DrawClosed(drawing);
                break;
            case DrawingStatus.Drawn:
                DrawResult(drawing);
                break;
            case DrawingStatus.Voided:
                DrawVoided(drawing);
                break;
        }

        DrawStatusMessage();
        DrawConfirmationPopups(drawing);
    }

    private void DrawWorkflow(DrawingStatus status)
    {
        var activeStep = status switch
        {
            DrawingStatus.Draft => 0,
            DrawingStatus.Open => 1,
            DrawingStatus.Closed => 2,
            DrawingStatus.Drawn => 3,
            DrawingStatus.Voided => 2,
            _ => 0,
        };

        var labels = new[] { "1  SETUP", "2  TICKET SALES", "3  REVIEW", "4  WINNER" };
        if (!ImGui.BeginTable("LuckySplitWorkflow", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            return;

        for (var index = 0; index < labels.Length; index++)
        {
            ImGui.TableSetupColumn(labels[index]);
        }

        ImGui.TableNextRow();
        for (var index = 0; index < labels.Length; index++)
        {
            ImGui.TableSetColumnIndex(index);
            if (index < activeStep)
                ImGui.TextColored(Success, $"✓ {labels[index]}");
            else if (index == activeStep)
                ImGui.TextColored(Gold, labels[index]);
            else
                ImGui.TextColored(Muted, labels[index]);
        }

        ImGui.EndTable();
    }

    private void DrawDraft(DrawingRecord drawing)
    {
        ImGui.TextColored(Gold, "Choose tonight's preset");
        ImGui.TextWrapped("Start with a saved venue setup, then adjust anything unique to this drawing. Draft changes do not overwrite the preset.");
        ImGui.Spacing();

        DrawDraftPresetSelector(drawing);
        ImGui.Separator();

        if (ImGui.BeginTable("LuckySplitDraftFields", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Drawing details");
            ImGui.TableSetupColumn("Ticket rules");
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var title = drawing.Title;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Drawing name##Draft", ref title, 120))
            {
                drawing.Title = title;
                plugin.Configuration.Save();
            }

            var venue = drawing.VenueName;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Venue##Draft", ref venue, 80))
            {
                drawing.VenueName = venue;
                plugin.Configuration.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var price = drawing.TicketPrice;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("Ticket price (gil)##Draft", ref price, 10_000, 100_000))
            {
                drawing.TicketPrice = Math.Clamp(price, 1, DrawingServiceLimits.MaxTicketPrice);
                plugin.Configuration.Save();
            }

            var split = drawing.WinnerPercent;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderInt("Winner share (%)##Draft", ref split, 1, 100))
            {
                drawing.WinnerPercent = split;
                plugin.Configuration.Save();
            }

            var limit = drawing.MaxTicketsPerPlayer;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("Player ticket limit (0 = unlimited)##Draft", ref limit))
            {
                drawing.MaxTicketsPerPlayer = Math.Clamp(limit, 0, DrawingServiceLimits.MaxTicketsPerDrawing);
                plugin.Configuration.Save();
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"Projected prize per {drawing.TicketPrice:N0}-gil ticket sale: {(long)drawing.TicketPrice * drawing.WinnerPercent / 100:N0} gil added to the winner's share.");
        ImGui.Spacing();

        if (PrimaryButton("Open Ticket Sales", new Vector2(210, 38)))
        {
            statusMessage = plugin.DrawingService.OpenDrawing(drawing);
            if (string.IsNullOrEmpty(statusMessage))
                statusMessage = "Ticket sales opened. Copy the opening announcement before accepting purchases.";
            plugin.Configuration.Save();
        }
    }

    private void DrawOpen(DrawingRecord drawing)
    {
        DrawSummaryCards(drawing);
        ImGui.Spacing();

        if (ImGui.GetContentRegionAvail().X < 900)
        {
            ImGui.TextColored(Gold, "Record purchase");
            DrawPurchaseForm(drawing);
            ImGui.Separator();
            ImGui.TextColored(Gold, "Live ticket ledger");
            DrawLedgerSearch();
            DrawPurchases(drawing, allowRemoval: true, height: 260);
        }
        else if (ImGui.BeginTable("LuckySplitSalesLayout", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Record purchase");
            ImGui.TableSetupColumn("Live ticket ledger");
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            DrawPurchaseForm(drawing);

            ImGui.TableSetColumnIndex(1);
            DrawLedgerSearch();
            DrawPurchases(drawing, allowRemoval: true, height: 300);

            ImGui.EndTable();
        }

        ImGui.Spacing();
        DrawOpenActions();

        if (ImGui.CollapsingHeader("Transparency commitment"))
        {
            ImGui.TextWrapped("Publish this commitment before or while accepting entries. The hidden seed is revealed only after a winner is drawn.");
            ImGui.TextWrapped(drawing.CommitmentHash);
            if (ImGui.Button("Copy Commitment"))
            {
                ImGui.SetClipboardText(drawing.CommitmentHash);
                statusMessage = "Commitment copied to the clipboard.";
            }
        }
    }

    private void DrawPurchaseForm(DrawingRecord drawing)
    {
        ImGui.TextWrapped("Confirm the gil trade in-game, then record the purchase here.");
        ImGui.Spacing();

        DrawNearbyPlayerPicker();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("Character name##Purchase", ref buyerName, 80);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("Home world (optional)##Purchase", ref buyerWorld, 40);

        ImGui.Text("Number of tickets");
        if (ImGui.SmallButton("−##TicketQuantity"))
            ticketQuantity = Math.Max(1, ticketQuantity - 1);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        ImGui.InputInt("##TicketQuantityInput", ref ticketQuantity);
        ticketQuantity = Math.Clamp(ticketQuantity, 1, DrawingServiceLimits.MaxTicketsPerPurchase);
        ImGui.SameLine();
        if (ImGui.SmallButton("+##TicketQuantity"))
            ticketQuantity = Math.Min(DrawingServiceLimits.MaxTicketsPerPurchase, ticketQuantity + 1);

        var purchaseAmount = (long)ticketQuantity * drawing.TicketPrice;
        ImGui.Spacing();
        ImGui.TextDisabled("Payment due");
        ImGui.TextColored(Gold, $"{purchaseAmount:N0} gil");

        if (drawing.MaxTicketsPerPlayer > 0)
            ImGui.TextDisabled($"Limit: {drawing.MaxTicketsPerPlayer:N0} tickets per character and world.");

        ImGui.Spacing();
        if (PrimaryButton("Confirm Purchase", new Vector2(180, 34)))
        {
            var enteredName = buyerName.Trim();
            var enteredQuantity = ticketQuantity;
            statusMessage = plugin.DrawingService.AddPurchase(drawing, buyerName, buyerWorld, ticketQuantity);
            if (string.IsNullOrEmpty(statusMessage))
            {
                statusMessage = $"Added {enteredQuantity:N0} ticket(s) for {enteredName}.";
                buyerName = string.Empty;
                ticketQuantity = 1;
            }

            plugin.Configuration.Save();
        }
    }


    private void DrawNearbyPlayerPicker()
    {
        if (DateTime.UtcNow >= nextNearbyPlayerRefreshUtc)
            RefreshNearbyPlayers();

        ImGui.Text("Select from nearby players");

        var rangeLabel = NearbyPlayerRangeLabels[Math.Clamp(nearbyPlayerRangeIndex, 0, NearbyPlayerRangeLabels.Length - 1)];
        ImGui.SetNextItemWidth(145);
        if (ImGui.BeginCombo("Range##NearbyPlayerRange", rangeLabel))
        {
            for (var index = 0; index < NearbyPlayerRangeLabels.Length; index++)
            {
                var selected = index == nearbyPlayerRangeIndex;
                if (ImGui.Selectable(NearbyPlayerRangeLabels[index], selected))
                {
                    nearbyPlayerRangeIndex = index;
                    nextNearbyPlayerRefreshUtc = DateTime.MinValue;
                    RefreshNearbyPlayers();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh##NearbyPlayers"))
        {
            nextNearbyPlayerRefreshUtc = DateTime.MinValue;
            RefreshNearbyPlayers();
        }

        ImGui.SetNextItemWidth(-1);
        var preview = nearbyPlayers.Count == 0
            ? "No loaded players found"
            : $"Choose a player ({nearbyPlayers.Count})";

        if (ImGui.BeginCombo("##NearbyPlayerPicker", preview))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##NearbyPlayerSearch", "Search name or home world", ref nearbyPlayerSearch, 80);
            ImGui.Separator();

            var matches = nearbyPlayers
                .Where(player => string.IsNullOrWhiteSpace(nearbyPlayerSearch)
                    || player.Name.Contains(nearbyPlayerSearch, StringComparison.OrdinalIgnoreCase)
                    || player.World.Contains(nearbyPlayerSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                ImGui.TextDisabled(nearbyPlayers.Count == 0
                    ? "No player characters are currently loaded in this range."
                    : "No nearby players match that search.");
            }
            else
            {
                for (var index = 0; index < matches.Count; index++)
                {
                    var player = matches[index];
                    var label = string.IsNullOrWhiteSpace(player.World)
                        ? $"{player.Name}  —  {player.Distance:F1}y"
                        : $"{player.Name} @ {player.World}  —  {player.Distance:F1}y";

                    if (ImGui.Selectable($"{label}##NearbyPlayer{index}"))
                    {
                        buyerName = player.Name;
                        buyerWorld = player.World;
                        nearbyPlayerSearch = string.Empty;
                        statusMessage = $"Selected {player.DisplayName}.";
                        ImGui.CloseCurrentPopup();
                    }
                }
            }

            ImGui.EndCombo();
        }

        ImGui.TextDisabled("Shows player characters currently loaded by your game client. Manual entry remains available below.");
    }

    private void RefreshNearbyPlayers()
    {
        nearbyPlayers.Clear();
        nextNearbyPlayerRefreshUtc = DateTime.UtcNow.AddSeconds(1);

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null)
            return;

        var rangeIndex = Math.Clamp(nearbyPlayerRangeIndex, 0, NearbyPlayerRanges.Length - 1);
        var maximumDistance = NearbyPlayerRanges[rangeIndex];
        var seenPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var battleCharacter in Plugin.ObjectTable.PlayerObjects)
        {
            if (battleCharacter is not IPlayerCharacter player)
                continue;

            var name = player.Name.TextValue.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var distance = Vector3.Distance(localPlayer.Position, player.Position);
            if (maximumDistance != float.MaxValue && distance > maximumDistance)
                continue;

            var world = player.HomeWorld.Value.Name.ToString();
            var uniqueKey = $"{name}\u001f{world}";
            if (!seenPlayers.Add(uniqueKey))
                continue;

            nearbyPlayers.Add(new NearbyPlayerOption(name, world, distance));
        }

        nearbyPlayers.Sort((left, right) =>
        {
            var distanceComparison = left.Distance.CompareTo(right.Distance);
            if (distanceComparison != 0)
                return distanceComparison;

            var nameComparison = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return nameComparison != 0
                ? nameComparison
                : string.Compare(left.World, right.World, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void DrawLedgerSearch()
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##LedgerSearch", "Search buyer or world", ref ledgerSearch, 80);
    }

    private void DrawOpenActions()
    {
        if (ImGui.Button("Copy Opening Announcement"))
        {
            ImGui.SetClipboardText(plugin.DrawingService.BuildOpeningAnnouncement(plugin.Configuration.CurrentDrawing));
            statusMessage = "Opening announcement copied to the clipboard.";
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy Sales Update"))
        {
            ImGui.SetClipboardText(plugin.DrawingService.BuildSalesUpdate(plugin.Configuration.CurrentDrawing));
            statusMessage = "Sales update copied to the clipboard.";
        }

        ImGui.SameLine();
        if (ImGui.Button("Export Ledger"))
            ExportCsv(plugin.Configuration.CurrentDrawing);

        ImGui.SameLine();
        if (PrimaryButton("Close Ticket Sales", new Vector2(180, 0)))
            ImGui.OpenPopup(CloseSalesPopup);

        ImGui.SameLine();
        if (DangerButton("Void…", new Vector2(80, 0)))
            ImGui.OpenPopup(VoidDrawingPopup);
    }

    private void DrawClosed(DrawingRecord drawing)
    {
        DrawSummaryCards(drawing);
        ImGui.Spacing();

        ImGui.TextColored(Gold, "Review the final ledger");
        ImGui.TextWrapped("Sales are closed and the ledger is locked. Confirm the totals before drawing the winner.");
        ImGui.Spacing();

        DrawLedgerSearch();
        DrawPurchases(drawing, allowRemoval: false, height: 320);

        ImGui.Spacing();
        if (PrimaryButton("Draw Winning Ticket", new Vector2(210, 40)))
            ImGui.OpenPopup(DrawWinnerPopup);

        ImGui.SameLine();
        if (ImGui.Button("Reopen Sales", new Vector2(130, 40)))
        {
            statusMessage = plugin.DrawingService.ReopenSales(drawing);
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Export Ledger", new Vector2(130, 40)))
            ExportCsv(drawing);

        ImGui.SameLine();
        if (DangerButton("Void…", new Vector2(90, 40)))
            ImGui.OpenPopup(VoidDrawingPopup);
    }

    private void DrawResult(DrawingRecord drawing)
    {
        DrawSummaryCards(drawing);
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild("LuckySplitWinnerCard", new Vector2(0, 190), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var mark = markTexture.GetWrapOrEmpty();
            if (ImGui.BeginTable("WinnerPresentation", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Mark", ImGuiTableColumnFlags.WidthFixed, 150f);
                ImGui.TableSetupColumn("Winner", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Image(mark.Handle, new Vector2(128, 128));

                ImGui.TableSetColumnIndex(1);
                ImGui.TextColored(LuckySplitTheme.Gold, "WINNING TICKET");
                ImGui.TextColored(LuckySplitTheme.Violet, $"#{drawing.WinningTicket:N0}");
                ImGui.TextColored(LuckySplitTheme.Text, drawing.WinnerDisplayName);
                ImGui.Spacing();
                ImGui.TextColored(LuckySplitTheme.Success, $"{drawing.WinnerPayout:N0} GIL");
                ImGui.TextDisabled($"Venue share: {drawing.VenueShare:N0} gil");

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Spacing();
        if (PrimaryButton("COPY WINNER ANNOUNCEMENT", new Vector2(235, 38)))
        {
            ImGui.SetClipboardText(plugin.DrawingService.BuildWinnerAnnouncement(drawing));
            statusMessage = "Winner announcement copied to the clipboard.";
        }

        ImGui.SameLine();
        if (ImGui.Button("EXPORT LEDGER", new Vector2(145, 38)))
            ExportCsv(drawing);

        ImGui.SameLine();
        if (ImGui.Button("ARCHIVE DRAWING...", new Vector2(175, 38)))
            ImGui.OpenPopup(ArchiveDrawingPopup);

        ImGui.Spacing();
        var commitmentValid = plugin.DrawingService.VerifyCommitment(drawing);
        ImGui.TextColored(commitmentValid ? Success : Danger,
            commitmentValid ? "✓ Drawing receipt verified" : "⚠ Drawing commitment could not be verified");

        if (ImGui.CollapsingHeader("Transparency receipt"))
        {
            ImGui.TextWrapped("The result is reproducible from the committed seed and final ledger. These details are intended for audit and recordkeeping.");
            ImGui.TextWrapped($"Commitment: {drawing.CommitmentHash}");
            ImGui.TextWrapped($"Revealed seed: {drawing.SeedHex}");
            ImGui.TextWrapped($"Ledger hash: {drawing.LedgerHash}");
            ImGui.TextWrapped($"Result digest: {drawing.ResultDigestHex}");
            if (ImGui.Button("COPY VERIFICATION RECEIPT"))
            {
                ImGui.SetClipboardText(plugin.DrawingService.BuildVerificationReceipt(drawing));
                statusMessage = "Verification receipt copied to the clipboard.";
            }
        }

        ImGui.Spacing();
        if (ImGui.CollapsingHeader($"Final ledger ({drawing.Purchases.Count:N0} purchases)"))
        {
            DrawLedgerSearch();
            DrawPurchases(drawing, allowRemoval: false, height: 260);
        }
    }

    private void DrawVoided(DrawingRecord drawing)
    {
        DrawSummaryCards(drawing);
        ImGui.Spacing();
        ImGui.TextColored(Danger, "This drawing was voided.");
        ImGui.TextWrapped("It cannot accept tickets or produce a winner. Archive it when you are ready to begin a new drawing.");
        ImGui.Spacing();

        if (ImGui.Button("Export Ledger", new Vector2(130, 34)))
            ExportCsv(drawing);
        ImGui.SameLine();
        if (PrimaryButton("Archive and Start New…", new Vector2(210, 34)))
            ImGui.OpenPopup(ArchiveDrawingPopup);
    }

    private void DrawSummaryCards(DrawingRecord drawing)
    {
        var projectedPayout = drawing.TotalPot * drawing.WinnerPercent / 100;
        if (!ImGui.BeginTable("LuckySplitSummaryCards", 4, ImGuiTableFlags.SizingStretchSame))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawMetricCard("TOTAL POT", $"{drawing.TotalPot:N0} gil", LuckySplitTheme.Gold, "Pot");

        ImGui.TableSetColumnIndex(1);
        DrawMetricCard("WINNER PRIZE", $"{(drawing.Status == DrawingStatus.Drawn ? drawing.WinnerPayout : projectedPayout):N0} gil", LuckySplitTheme.Teal, "Prize");

        ImGui.TableSetColumnIndex(2);
        DrawMetricCard("TICKETS SOLD", drawing.TotalTickets.ToString("N0"), LuckySplitTheme.Violet, "Tickets");

        ImGui.TableSetColumnIndex(3);
        DrawMetricCard("ENTRANTS", drawing.UniquePlayers.ToString("N0"), LuckySplitTheme.Success, "Entrants");

        ImGui.EndTable();
    }

    private void DrawMetricCard(string label, string value, Vector4 accent, string id)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild($"MetricCard{id}", new Vector2(0, 78), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var position = ImGui.GetWindowPos();
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(position, position + new Vector2(5f, ImGui.GetWindowSize().Y), ImGui.GetColorU32(accent));

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            ImGui.TextDisabled(label);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            ImGui.TextColored(accent, value);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawPurchases(DrawingRecord drawing, bool allowRemoval, float height)
    {
        var filtered = drawing.Purchases
            .Where(p => string.IsNullOrWhiteSpace(ledgerSearch)
                || p.PlayerName.Contains(ledgerSearch, StringComparison.OrdinalIgnoreCase)
                || p.World.Contains(ledgerSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (drawing.Purchases.Count == 0)
        {
            ImGui.TextDisabled("No purchases recorded yet.");
            return;
        }

        if (filtered.Count == 0)
        {
            ImGui.TextDisabled("No ledger entries match that search.");
            return;
        }

        if (ImGui.BeginTable("LuckySplitTicketLedger", allowRemoval ? 6 : 5,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                new Vector2(0, height)))
        {
            ImGui.TableSetupColumn("Buyer");
            ImGui.TableSetupColumn("Tickets");
            ImGui.TableSetupColumn("Range");
            ImGui.TableSetupColumn("Paid");
            ImGui.TableSetupColumn("Local time");
            if (allowRemoval)
                ImGui.TableSetupColumn("Action");
            ImGui.TableHeadersRow();

            foreach (var purchase in filtered)
            {
                ImGui.PushID(purchase.Id.ToString());
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(purchase.DisplayName);

                ImGui.TableSetColumnIndex(1);
                ImGui.Text($"{purchase.Quantity:N0}");

                ImGui.TableSetColumnIndex(2);
                ImGui.Text($"#{purchase.StartTicket:N0}–#{purchase.EndTicket:N0}");

                ImGui.TableSetColumnIndex(3);
                ImGui.Text($"{(long)purchase.Quantity * drawing.TicketPrice:N0}");

                ImGui.TableSetColumnIndex(4);
                ImGui.Text(purchase.PurchasedAt.LocalDateTime.ToString("h:mm tt"));

                if (allowRemoval)
                {
                    ImGui.TableSetColumnIndex(5);
                    if (ImGui.SmallButton("Remove"))
                    {
                        plugin.DrawingService.RemovePurchase(drawing, purchase.Id);
                        plugin.Configuration.Save();
                        statusMessage = $"Removed the purchase for {purchase.DisplayName}; ticket ranges were recalculated.";
                    }
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private void DrawConfirmationPopups(DrawingRecord drawing)
    {
        var closeSalesPopupOpen = true;
        if (ImGui.BeginPopupModal(CloseSalesPopup, ref closeSalesPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Close ticket sales?");
            ImGui.Separator();
            ImGui.TextWrapped("The ledger will be locked for review. You can still reopen sales before drawing a winner.");
            ImGui.Spacing();
            ImGui.Text($"Tickets: {drawing.TotalTickets:N0}");
            ImGui.Text($"Entrants: {drawing.UniquePlayers:N0}");
            ImGui.Text($"Projected winner prize: {drawing.TotalPot * drawing.WinnerPercent / 100:N0} gil");
            ImGui.Spacing();

            if (ImGui.Button("Cancel", new Vector2(110, 32)))
                ImGui.CloseCurrentPopup();
            ImGui.SameLine();
            if (PrimaryButton("Close Sales", new Vector2(140, 32)))
            {
                statusMessage = plugin.DrawingService.CloseSales(drawing);
                if (string.IsNullOrEmpty(statusMessage))
                {
                    statusMessage = "Ticket sales closed. Review the ledger, then draw the winner.";
                    plugin.Configuration.Save();
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }

        var drawWinnerPopupOpen = true;
        if (ImGui.BeginPopupModal(DrawWinnerPopup, ref drawWinnerPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Draw the winning ticket?");
            ImGui.Separator();
            ImGui.TextWrapped("This permanently locks the ledger, reveals the drawing seed, and selects one winning ticket.");
            ImGui.Spacing();
            ImGui.Text($"Total tickets: {drawing.TotalTickets:N0}");
            ImGui.Text($"Total pot: {drawing.TotalPot:N0} gil");
            ImGui.Text($"Winner prize: {drawing.TotalPot * drawing.WinnerPercent / 100:N0} gil");
            ImGui.Spacing();

            if (ImGui.Button("Cancel", new Vector2(110, 32)))
                ImGui.CloseCurrentPopup();
            ImGui.SameLine();
            if (PrimaryButton("Draw Winner", new Vector2(150, 32)))
            {
                statusMessage = plugin.DrawingService.DrawWinner(drawing);
                if (string.IsNullOrEmpty(statusMessage))
                {
                    statusMessage = $"Winning ticket #{drawing.WinningTicket:N0}: {drawing.WinnerDisplayName}.";
                    plugin.Configuration.Save();
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }

        var voidDrawingPopupOpen = true;
        if (ImGui.BeginPopupModal(VoidDrawingPopup, ref voidDrawingPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(Danger, "Void this drawing?");
            ImGui.Separator();
            ImGui.TextWrapped("The drawing will be permanently marked void and cannot produce a winner. Recorded purchases remain available for export and refund tracking.");
            ImGui.Spacing();

            if (ImGui.Button("Keep Drawing", new Vector2(130, 32)))
                ImGui.CloseCurrentPopup();
            ImGui.SameLine();
            if (DangerButton("Void Drawing", new Vector2(140, 32)))
            {
                plugin.DrawingService.VoidDrawing(drawing);
                plugin.Configuration.Save();
                statusMessage = "Drawing voided.";
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        var archiveDrawingPopupOpen = true;
        if (ImGui.BeginPopupModal(ArchiveDrawingPopup, ref archiveDrawingPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Archive this drawing?");
            ImGui.Separator();
            ImGui.TextWrapped("Lucky Split will save this drawing to History and create a fresh draft from your selected preset.");
            ImGui.Spacing();

            if (ImGui.Button("Cancel", new Vector2(110, 32)))
                ImGui.CloseCurrentPopup();
            ImGui.SameLine();
            if (PrimaryButton("Archive & New", new Vector2(150, 32)))
            {
                plugin.ArchiveAndCreateNew();
                ledgerSearch = string.Empty;
                statusMessage = "Drawing archived. A new preset-based draft is ready.";
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawStatusMessage()
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
            return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped(statusMessage);
    }

    private void DrawHistory()
    {
        var history = plugin.Configuration.History;
        if (history.Count == 0)
        {
            DrawEmptyState("NO DRAWINGS ARCHIVED", "Completed or voided drawings will appear here after they are archived.");
            return;
        }

        var filtered = history
            .Where(drawing => string.IsNullOrWhiteSpace(historySearch)
                || drawing.Title.Contains(historySearch, StringComparison.OrdinalIgnoreCase)
                || drawing.VenueName.Contains(historySearch, StringComparison.OrdinalIgnoreCase)
                || drawing.WinnerDisplayName.Contains(historySearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (selectedHistoryId == Guid.Empty || history.All(drawing => drawing.Id != selectedHistoryId))
            selectedHistoryId = filtered.FirstOrDefault()?.Id ?? history[0].Id;

        if (ImGui.BeginTable("LuckySplitHistoryLayout", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Archive", ImGuiTableColumnFlags.WidthStretch, 0.38f);
            ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, 0.62f);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.Panel);
            if (ImGui.BeginChild("HistoryList", new Vector2(0, 520), true))
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##HistorySearch", "Search venue, drawing, or winner", ref historySearch, 100);
                ImGui.Spacing();

                if (filtered.Count == 0)
                {
                    ImGui.TextDisabled("No archived drawings match that search.");
                }
                else
                {
                    foreach (var drawing in filtered)
                        DrawHistoryListCard(drawing);
                }
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(1);
            var selected = history.FirstOrDefault(drawing => drawing.Id == selectedHistoryId);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.Panel);
            if (ImGui.BeginChild("HistoryDetails", new Vector2(0, 520), true))
            {
                if (selected is null)
                {
                    ImGui.TextDisabled("Select a drawing to view its details.");
                }
                else
                {
                    DrawHistoryDetails(selected);
                }
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.EndTable();
        }
    }

    private void DrawHistoryListCard(DrawingRecord drawing)
    {
        var selected = drawing.Id == selectedHistoryId;
        ImGui.PushStyleColor(ImGuiCol.Button, selected ? LuckySplitTheme.PanelHover : LuckySplitTheme.PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, LuckySplitTheme.PanelHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, LuckySplitTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, selected ? LuckySplitTheme.Gold : LuckySplitTheme.Text);

        var label = $"{drawing.CreatedAt.LocalDateTime:MMM d, yyyy}   {drawing.Title}##History{drawing.Id}";
        if (ImGui.Button(label, new Vector2(-1, 42)))
            selectedHistoryId = drawing.Id;

        ImGui.PopStyleColor(4);
        ImGui.TextDisabled($"{drawing.VenueName}  •  {GetStatusLabel(drawing.Status)}  •  {drawing.TotalTickets:N0} tickets");
        ImGui.Spacing();
    }

    private void DrawHistoryDetails(DrawingRecord drawing)
    {
        ImGui.TextColored(LuckySplitTheme.Gold, drawing.Title);
        ImGui.TextDisabled($"{drawing.VenueName}  •  {drawing.CreatedAt.LocalDateTime:MMM d, yyyy h:mm tt}");
        ImGui.Spacing();
        DrawSummaryCards(drawing);
        ImGui.Spacing();

        DrawStatusPill(GetStatusLabel(drawing.Status), GetStatusColor(drawing.Status));
        if (drawing.Status == DrawingStatus.Drawn)
        {
            ImGui.Spacing();
            ImGui.TextColored(LuckySplitTheme.Teal, $"WINNER  {drawing.WinnerDisplayName}");
            ImGui.TextColored(LuckySplitTheme.Violet, $"Ticket #{drawing.WinningTicket:N0}  •  {drawing.WinnerPayout:N0} gil");
            ImGui.Spacing();

            if (PrimaryButton($"Copy Winner##History{drawing.Id}", new Vector2(150, 34)))
                ImGui.SetClipboardText(plugin.DrawingService.BuildWinnerAnnouncement(drawing));
            ImGui.SameLine();
            if (ImGui.Button($"Copy Receipt##History{drawing.Id}", new Vector2(140, 34)))
                ImGui.SetClipboardText(plugin.DrawingService.BuildVerificationReceipt(drawing));
            ImGui.SameLine();
        }

        if (ImGui.Button($"Export Ledger##History{drawing.Id}", new Vector2(140, 34)))
            ExportCsv(drawing);

        ImGui.Spacing();
        if (ImGui.CollapsingHeader($"Ledger ({drawing.Purchases.Count:N0} purchases)##HistoryLedger{drawing.Id}"))
        {
            DrawPurchases(drawing, allowRemoval: false, height: 240);
        }
    }

    private void LoadFirstRunDefaults()
    {
        var config = plugin.Configuration;
        var drawing = config.CurrentDrawing;
        var legacyVenue = string.IsNullOrWhiteSpace(drawing.VenueName)
            ? config.DefaultVenueName
            : drawing.VenueName;

        onboardingPresetName = string.IsNullOrWhiteSpace(legacyVenue) ? "My Venue" : legacyVenue;
        onboardingVenueName = onboardingPresetName;
        onboardingDrawingTitle = string.IsNullOrWhiteSpace(drawing.Title)
            ? "Tonight's 50/50 Drawing"
            : drawing.Title;
        onboardingTicketPrice = Math.Clamp(
            drawing.TicketPrice > 0 ? drawing.TicketPrice : config.DefaultTicketPrice,
            1,
            DrawingServiceLimits.MaxTicketPrice);
        onboardingWinnerPercent = Math.Clamp(
            drawing.WinnerPercent > 0 ? drawing.WinnerPercent : config.DefaultWinnerPercent,
            1,
            100);
        onboardingPlayerLimit = Math.Clamp(
            drawing.MaxTicketsPerPlayer,
            0,
            DrawingServiceLimits.MaxTicketsPerDrawing);
    }

    private void DrawFirstRunSetup()
    {
        ImGui.TextColored(Gold, "WELCOME TO LUCKY SPLIT");
        ImGui.TextDisabled("Set up your first venue preset");
        ImGui.Separator();
        ImGui.TextWrapped("Presets let you switch between venues and recurring event setups without re-entering ticket rules every night. You can create more presets later.");
        ImGui.Spacing();

        if (ImGui.BeginTable("LuckySplitFirstRun", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Venue identity");
            ImGui.TableSetupColumn("Drawing defaults");
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("Preset name##Onboarding", ref onboardingPresetName, 80);
            ImGui.TextDisabled("Example: Everbloom Fridays");

            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("Venue name##Onboarding", ref onboardingVenueName, 80);

            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("Default drawing name##Onboarding", ref onboardingDrawingTitle, 120);

            ImGui.TableSetColumnIndex(1);
            ImGui.SetNextItemWidth(-1);
            ImGui.InputInt("Ticket price (gil)##Onboarding", ref onboardingTicketPrice, 10_000, 100_000);
            onboardingTicketPrice = Math.Clamp(onboardingTicketPrice, 1, DrawingServiceLimits.MaxTicketPrice);

            ImGui.SetNextItemWidth(-1);
            ImGui.SliderInt("Winner share (%)##Onboarding", ref onboardingWinnerPercent, 1, 100);

            ImGui.SetNextItemWidth(-1);
            ImGui.InputInt("Player ticket limit (0 = unlimited)##Onboarding", ref onboardingPlayerLimit);
            onboardingPlayerLimit = Math.Clamp(onboardingPlayerLimit, 0, DrawingServiceLimits.MaxTicketsPerDrawing);

            ImGui.EndTable();
        }

        ImGui.Spacing();
        if (PrimaryButton("Create First Preset", new Vector2(200, 38)))
        {
            var validation = ValidatePreset(
                onboardingPresetName,
                onboardingVenueName,
                onboardingDrawingTitle,
                onboardingTicketPrice,
                onboardingWinnerPercent,
                onboardingPlayerLimit);

            if (!string.IsNullOrEmpty(validation))
            {
                statusMessage = validation;
            }
            else
            {
                var preset = new VenuePreset
                {
                    Name = onboardingPresetName.Trim(),
                    VenueName = onboardingVenueName.Trim(),
                    DefaultDrawingTitle = onboardingDrawingTitle.Trim(),
                    TicketPrice = onboardingTicketPrice,
                    WinnerPercent = onboardingWinnerPercent,
                    MaxTicketsPerPlayer = onboardingPlayerLimit,
                };

                var config = plugin.Configuration;
                config.Presets.Add(preset);
                config.SelectedPresetId = preset.Id;
                config.HasCompletedPresetSetup = true;
                config.Version = 6;

                var applied = plugin.DrawingService.ApplyPresetToDraft(config.CurrentDrawing, preset);
                config.Save();
                LoadPresetEditor(preset);

                statusMessage = applied
                    ? $"Created preset '{preset.DisplayName}' and applied it to the current draft."
                    : $"Created preset '{preset.DisplayName}'. Your active drawing was left unchanged.";
            }
        }

        DrawStatusMessage();
    }

    private void DrawDraftPresetSelector(DrawingRecord drawing)
    {
        var config = plugin.Configuration;
        var selected = config.Presets.FirstOrDefault(p => p.Id == drawing.PresetId)
            ?? config.GetSelectedPreset();
        var preview = selected?.DisplayName ?? "Choose a preset";

        ImGui.SetNextItemWidth(360);
        if (ImGui.BeginCombo("Venue preset", preview))
        {
            foreach (var preset in config.Presets)
            {
                var isSelected = selected?.Id == preset.Id;
                if (ImGui.Selectable(preset.DisplayName, isSelected))
                {
                    ActivatePreset(preset);
                    selected = preset;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        if (selected is not null)
        {
            ImGui.TextDisabled($"{selected.VenueName}  •  {selected.TicketPrice:N0} gil  •  {selected.WinnerPercent}% to winner" +
                (selected.MaxTicketsPerPlayer > 0 ? $"  •  Limit {selected.MaxTicketsPerPlayer:N0}" : "  •  Unlimited tickets"));
        }
    }

    private void DrawPresets()
    {
        var config = plugin.Configuration;
        EnsurePresetEditorLoaded();

        var filtered = config.Presets
            .Where(preset => string.IsNullOrWhiteSpace(presetSearch)
                || preset.Name.Contains(presetSearch, StringComparison.OrdinalIgnoreCase)
                || preset.VenueName.Contains(presetSearch, StringComparison.OrdinalIgnoreCase)
                || preset.DefaultDrawingTitle.Contains(presetSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var available = ImGui.GetContentRegionAvail();
        var useSideBySideLayout = available.X >= 920f;

        if (useSideBySideLayout)
        {
            var panelHeight = Math.Max(460f, available.Y - 34f);
            if (ImGui.BeginTable("LuckySplitPresetLayout", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Preset library", ImGuiTableColumnFlags.WidthStretch, 0.42f);
                ImGui.TableSetupColumn("Preset editor", ImGuiTableColumnFlags.WidthStretch, 0.58f);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                DrawPresetLibraryPanel(filtered, panelHeight);

                ImGui.TableSetColumnIndex(1);
                DrawPresetEditorPanel(config, panelHeight);

                ImGui.EndTable();
            }
        }
        else
        {
            // Stack the panels on smaller windows so controls and labels never get squeezed off-screen.
            DrawPresetLibraryPanel(filtered, 360f);
            ImGui.Spacing();
            DrawPresetEditorPanel(config, 500f);
        }

        DrawStatusMessage();
    }

    private void DrawPresetLibraryPanel(IReadOnlyCollection<VenuePreset> filtered, float height)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.Panel);
        if (ImGui.BeginChild("PresetLibrary", new Vector2(0, height), true))
        {
            ImGui.TextColored(LuckySplitTheme.Gold, "PRESET LIBRARY");
            var activePreset = plugin.Configuration.GetSelectedPreset();
            ImGui.TextDisabled(activePreset is null
                ? "No active preset"
                : $"Active: {activePreset.DisplayName}");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##PresetSearch", "Filter presets...", ref presetSearch, 100);
            if (PrimaryButton("+ NEW PRESET", new Vector2(-1, 36)))
                BeginNewPresetEditor();

            ImGui.Spacing();
            foreach (var preset in filtered)
                DrawPresetListCard(preset);

            if (filtered.Count == 0)
                ImGui.TextDisabled("No presets match that filter.");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawPresetEditorPanel(Configuration config, float height)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.Panel);
        if (ImGui.BeginChild("PresetEditor", new Vector2(0, height), true))
            DrawPresetEditor(config);
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawPresetListCard(VenuePreset preset)
    {
        var selectedForFuture = plugin.Configuration.SelectedPresetId == preset.Id;
        var editing = !creatingPreset && presetEditorId == preset.Id;
        var accent = selectedForFuture ? LuckySplitTheme.Teal : LuckySplitTheme.Violet;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, editing ? LuckySplitTheme.PanelHover : LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild($"PresetCard{preset.Id}", new Vector2(0, 142), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var position = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddRectFilled(
                position,
                position + new Vector2(selectedForFuture ? 7f : 5f, ImGui.GetWindowSize().Y),
                ImGui.GetColorU32(accent));

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            ImGui.PushStyleColor(ImGuiCol.Text, selectedForFuture ? LuckySplitTheme.Teal : LuckySplitTheme.Text);
            var selectorLabel = selectedForFuture
                ? $"{preset.DisplayName}   •   ACTIVE PRESET##SelectPreset{preset.Id}"
                : $"{preset.DisplayName}##SelectPreset{preset.Id}";
            if (ImGui.Selectable(selectorLabel, selectedForFuture))
                ActivatePreset(preset);
            ImGui.PopStyleColor();

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            ImGui.TextDisabled($"Venue: {preset.VenueName}");
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            ImGui.TextDisabled($"{preset.TicketPrice:N0} gil per ticket  •  {preset.WinnerPercent}% to winner");
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            ImGui.TextDisabled(preset.MaxTicketsPerPlayer > 0
                ? $"Limit: {preset.MaxTicketsPerPlayer:N0} tickets per player"
                : "Limit: Unlimited tickets per player");

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            if (!selectedForFuture)
            {
                if (ImGui.SmallButton($"ACTIVATE##Preset{preset.Id}"))
                    ActivatePreset(preset);
                ImGui.SameLine();
            }

            if (ImGui.SmallButton($"EDIT DETAILS##Preset{preset.Id}"))
            {
                LoadPresetEditor(preset);
                deleteArmedPresetId = Guid.Empty;
                statusMessage = selectedForFuture
                    ? $"Editing active preset '{preset.DisplayName}'."
                    : $"Editing '{preset.DisplayName}'. The active preset has not changed.";
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    private void DrawPresetEditor(Configuration config)
    {
        var activePreset = config.GetSelectedPreset();
        var editedPreset = creatingPreset
            ? null
            : config.Presets.FirstOrDefault(preset => preset.Id == presetEditorId);
        var editingActivePreset = editedPreset is not null && activePreset?.Id == editedPreset.Id;

        ImGui.TextColored(LuckySplitTheme.Gold, creatingPreset ? "CREATE PRESET" : "EDIT PRESET");
        if (creatingPreset)
        {
            ImGui.TextDisabled("Create a reusable venue and ticket-rule setup.");
        }
        else if (editingActivePreset)
        {
            ImGui.TextColored(LuckySplitTheme.Teal, "ACTIVE PRESET");
            ImGui.TextDisabled("Saving changes updates this preset. Use Apply to Draft to refresh a draft already in progress.");
        }
        else
        {
            ImGui.TextColored(LuckySplitTheme.Violet,
                activePreset is null ? "NOT ACTIVE" : $"EDITING ONLY  •  ACTIVE: {activePreset.DisplayName}");
            ImGui.TextDisabled("Editing this preset does not activate it. Press Activate Preset when you are ready to switch.");
        }

        ImGui.Spacing();
        DrawEditorTextLabel("Preset name", "A friendly label such as Everbloom Fridays.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##PresetNameEditor", ref presetName, 80);

        DrawEditorTextLabel("Venue name", "The venue name shown in announcements and drawing records.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##PresetVenueEditor", ref presetVenueName, 80);

        DrawEditorTextLabel("Default drawing name", "The starting title for a new drawing using this preset.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##PresetDrawingTitleEditor", ref presetDrawingTitle, 120);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawEditorTextLabel("Ticket price", "Gil due for each ticket.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputInt("##PresetTicketPriceEditor", ref presetTicketPrice, 10_000, 100_000);
        presetTicketPrice = Math.Clamp(presetTicketPrice, 1, DrawingServiceLimits.MaxTicketPrice);

        DrawEditorTextLabel("Winner share", "Percentage of the final pot awarded to the winner.");
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderInt("##PresetWinnerPercentEditor", ref presetWinnerPercent, 1, 100);
        ImGui.TextDisabled($"Current winner share: {presetWinnerPercent}%");

        DrawEditorTextLabel("Player ticket limit", "Use 0 for unlimited tickets per player.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputInt("##PresetPlayerLimitEditor", ref presetPlayerLimit);
        presetPlayerLimit = Math.Clamp(presetPlayerLimit, 0, DrawingServiceLimits.MaxTicketsPerDrawing);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var stackActions = availableWidth < 520f;
        var buttonWidth = stackActions ? -1f : 165f;
        var saveLabel = creatingPreset ? "CREATE PRESET" : "SAVE CHANGES";
        if (PrimaryButton(saveLabel, new Vector2(buttonWidth, 38)))
            SavePresetEditor();

        if (!creatingPreset)
        {
            if (!stackActions)
                ImGui.SameLine();

            if (ImGui.Button("ACTIVATE PRESET", new Vector2(buttonWidth, 38)))
            {
                var preset = config.Presets.FirstOrDefault(p => p.Id == presetEditorId);
                if (preset is not null)
                    ActivatePreset(preset);
            }

            if (!stackActions)
                ImGui.SameLine();

            var canDelete = config.Presets.Count > 1;
            if (!canDelete)
                ImGui.BeginDisabled();

            var deleteLabel = deleteArmedPresetId == presetEditorId ? "CONFIRM DELETE" : "DELETE PRESET";
            if (DangerButton(deleteLabel, new Vector2(buttonWidth, 38)))
            {
                if (deleteArmedPresetId != presetEditorId)
                {
                    deleteArmedPresetId = presetEditorId;
                    statusMessage = "Press Confirm Delete to permanently remove this preset.";
                }
                else
                {
                    DeletePreset(presetEditorId);
                }
            }

            if (!canDelete)
                ImGui.EndDisabled();

            if (editingActivePreset && config.CurrentDrawing.Status == DrawingStatus.Draft)
            {
                ImGui.Spacing();
                if (ImGui.Button("APPLY SAVED PRESET TO CURRENT DRAFT", new Vector2(-1, 36)))
                {
                    var preset = config.Presets.FirstOrDefault(p => p.Id == presetEditorId);
                    if (preset is not null)
                    {
                        plugin.DrawingService.ApplyPresetToDraft(config.CurrentDrawing, preset);
                        config.Save();
                        statusMessage = $"Applied saved preset '{preset.DisplayName}' to the current draft.";
                    }
                }
            }
        }
        else
        {
            if (!stackActions)
                ImGui.SameLine();
            if (ImGui.Button("CANCEL", new Vector2(buttonWidth, 38)))
                EnsurePresetEditorLoaded(forceReload: true);
        }
    }

    private static void DrawEditorTextLabel(string label, string helpText)
    {
        ImGui.TextUnformatted(label);
        ImGui.TextDisabled(helpText);
    }

    private void ActivatePreset(VenuePreset preset)
    {
        var config = plugin.Configuration;
        config.SelectedPresetId = preset.Id;
        var applied = plugin.DrawingService.ApplyPresetToDraft(config.CurrentDrawing, preset);
        config.Save();
        LoadPresetEditor(preset);
        deleteArmedPresetId = Guid.Empty;
        statusMessage = applied
            ? $"Activated '{preset.DisplayName}' and applied it to the current draft."
            : $"Activated '{preset.DisplayName}' for the next drawing. The current drawing was not changed because it is already underway.";
    }

    private void EnsurePresetEditorLoaded(bool forceReload = false)
    {
        var config = plugin.Configuration;
        if (!forceReload && (creatingPreset || config.Presets.Any(p => p.Id == presetEditorId)))
            return;

        var preset = config.GetSelectedPreset() ?? config.Presets.First();
        LoadPresetEditor(preset);
    }

    private void LoadPresetEditor(VenuePreset preset)
    {
        creatingPreset = false;
        presetEditorId = preset.Id;
        presetName = preset.Name;
        presetVenueName = preset.VenueName;
        presetDrawingTitle = preset.DefaultDrawingTitle;
        presetTicketPrice = preset.TicketPrice;
        presetWinnerPercent = preset.WinnerPercent;
        presetPlayerLimit = preset.MaxTicketsPerPlayer;
    }

    private void BeginNewPresetEditor()
    {
        var source = plugin.Configuration.GetSelectedPreset();
        creatingPreset = true;
        presetEditorId = Guid.Empty;
        presetName = source is null ? "New Venue" : $"{source.DisplayName} Copy";
        presetVenueName = source?.VenueName ?? "New Venue";
        presetDrawingTitle = source?.DefaultDrawingTitle ?? "Tonight's 50/50 Drawing";
        presetTicketPrice = source?.TicketPrice ?? 100_000;
        presetWinnerPercent = source?.WinnerPercent ?? 50;
        presetPlayerLimit = source?.MaxTicketsPerPlayer ?? 0;
        deleteArmedPresetId = Guid.Empty;
        statusMessage = string.Empty;
    }

    private void SavePresetEditor()
    {
        var validation = ValidatePreset(
            presetName,
            presetVenueName,
            presetDrawingTitle,
            presetTicketPrice,
            presetWinnerPercent,
            presetPlayerLimit);

        if (!string.IsNullOrEmpty(validation))
        {
            statusMessage = validation;
            return;
        }

        var config = plugin.Configuration;
        var wasCreating = creatingPreset;
        VenuePreset preset;

        if (creatingPreset)
        {
            preset = new VenuePreset();
            config.Presets.Add(preset);
        }
        else
        {
            var existingPreset = config.Presets.FirstOrDefault(p => p.Id == presetEditorId);
            if (existingPreset is null)
            {
                statusMessage = "That preset could not be found.";
                return;
            }

            preset = existingPreset;
        }

        preset.Name = presetName.Trim();
        preset.VenueName = presetVenueName.Trim();
        preset.DefaultDrawingTitle = presetDrawingTitle.Trim();
        preset.TicketPrice = presetTicketPrice;
        preset.WinnerPercent = presetWinnerPercent;
        preset.MaxTicketsPerPlayer = presetPlayerLimit;

        var appliedToDraft = false;
        if (wasCreating)
        {
            config.SelectedPresetId = preset.Id;
            appliedToDraft = plugin.DrawingService.ApplyPresetToDraft(config.CurrentDrawing, preset);
        }

        config.Save();
        LoadPresetEditor(preset);
        statusMessage = wasCreating
            ? $"Created preset '{preset.DisplayName}'."
            : $"Saved changes to '{preset.DisplayName}'.";

        if (config.SelectedPresetId == preset.Id)
            statusMessage += " It is selected for new drawings.";
        if (appliedToDraft)
            statusMessage += " It was also applied to the current draft.";
    }

    private void DeletePreset(Guid presetId)
    {
        var config = plugin.Configuration;
        if (config.Presets.Count <= 1)
        {
            statusMessage = "At least one preset is required.";
            return;
        }

        var preset = config.Presets.FirstOrDefault(p => p.Id == presetId);
        if (preset is null)
            return;

        config.Presets.Remove(preset);
        if (config.SelectedPresetId == presetId)
            config.SelectedPresetId = config.Presets[0].Id;

        var replacement = config.GetSelectedPreset()!;
        if (config.CurrentDrawing.Status == DrawingStatus.Draft &&
            config.CurrentDrawing.PresetId == presetId)
        {
            plugin.DrawingService.ApplyPresetToDraft(config.CurrentDrawing, replacement);
        }

        config.Save();
        LoadPresetEditor(replacement);
        deleteArmedPresetId = Guid.Empty;
        statusMessage = $"Deleted preset '{preset.DisplayName}'.";
    }

    private static string ValidatePreset(
        string name,
        string venueName,
        string drawingTitle,
        int ticketPrice,
        int winnerPercent,
        int playerLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a preset name.";
        if (string.IsNullOrWhiteSpace(venueName))
            return "Enter a venue name.";
        if (string.IsNullOrWhiteSpace(drawingTitle))
            return "Enter a default drawing name.";
        if (ticketPrice <= 0 || ticketPrice > DrawingServiceLimits.MaxTicketPrice)
            return $"Ticket price must be between 1 and {DrawingServiceLimits.MaxTicketPrice:N0} gil.";
        if (winnerPercent is < 1 or > 100)
            return "Winner share must be between 1 and 100 percent.";
        if (playerLimit < 0 || playerLimit > DrawingServiceLimits.MaxTicketsPerDrawing)
            return $"Player ticket limit must be between 0 and {DrawingServiceLimits.MaxTicketsPerDrawing:N0}.";

        return string.Empty;
    }

    private void DrawAbout()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild("LuckySplitAboutCard", new Vector2(0, 190), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var mark = markTexture.GetWrapOrEmpty();
            if (ImGui.BeginTable("LuckySplitAboutLayout", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Mark", ImGuiTableColumnFlags.WidthFixed, 150f);
                ImGui.TableSetupColumn("Information", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Image(mark.Handle, new Vector2(128, 128));

                ImGui.TableSetColumnIndex(1);
                ImGui.TextColored(LuckySplitTheme.Gold, "LUCKY SPLIT");
                ImGui.TextDisabled("Host-side 50/50 drawing console for FFXIV venues");
                ImGui.Spacing();
                ImGui.TextWrapped("Lucky Split never moves gil, completes trades, or sends chat automatically. Staff confirm payments in-game, record purchases, and use copy buttons for announcements.");
                ImGui.Spacing();
                ImGui.TextColored(LuckySplitTheme.Teal, "Version 1.0.0");

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Spacing();
        if (ImGui.Button("Show Welcome Splash Again", new Vector2(220, 34)))
        {
            plugin.Configuration.HasSeenWelcomeSplash = false;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("The splash will appear immediately and only repeats when requested.");

        ImGui.Spacing();
        if (ImGui.BeginTable("LuckySplitToolsGrid", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawInformationPanel(
                "COMMANDS",
                "/luckysplit\n/lucky\n/5050");

            ImGui.TableSetColumnIndex(1);
            DrawInformationPanel(
                "SAFETY",
                "Maximum ticket price: 1,000,000,000 gil\nMaximum purchase: 100,000 tickets\nMaximum drawing: 1,000,000 tickets");

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawInformationPanel(
                "TRANSPARENCY",
                "The receipt commits to a hidden seed when sales open, then hashes the final ledger before selecting the winner. The result is reproducible for auditing, but the host still controls the local ledger.");

            ImGui.TableSetColumnIndex(1);
            var exportText = string.IsNullOrWhiteSpace(lastExportPath)
                ? "No CSV has been exported during this session."
                : $"Last CSV export:\n{lastExportPath}";
            DrawInformationPanel("FILES", exportText);

            ImGui.EndTable();
        }
    }

    private void DrawInformationPanel(string title, string body)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild($"InfoPanel{title}", new Vector2(0, 135), true))
        {
            ImGui.TextColored(LuckySplitTheme.Gold, title);
            ImGui.Separator();
            ImGui.TextWrapped(body);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawEmptyState(string title, string message)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, LuckySplitTheme.PanelRaised);
        if (ImGui.BeginChild($"EmptyState{title}", new Vector2(0, 220), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var mark = markTexture.GetWrapOrEmpty();
            var size = new Vector2(96, 96);
            ImGui.SetCursorPosX(Math.Max(0f, (ImGui.GetContentRegionAvail().X - size.X) / 2f));
            ImGui.Image(mark.Handle, size);
            var titleSize = ImGui.CalcTextSize(title);
            ImGui.SetCursorPosX(Math.Max(0f, (ImGui.GetContentRegionAvail().X - titleSize.X) / 2f));
            ImGui.TextColored(LuckySplitTheme.Gold, title);
            ImGui.TextWrapped(message);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void ExportCsv(DrawingRecord drawing)
    {
        try
        {
            var exportDirectory = System.IO.Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "exports");
            lastExportPath = plugin.DrawingService.ExportCsv(drawing, exportDirectory);
            ImGui.SetClipboardText(lastExportPath);
            statusMessage = $"CSV exported and path copied: {lastExportPath}";
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to export Lucky Split CSV.");
            statusMessage = $"CSV export failed: {ex.Message}";
        }
    }

    private static bool PrimaryButton(string label, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.62f, 0.43f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.78f, 0.57f, 0.17f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.88f, 0.67f, 0.24f, 1f));
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }

    private static bool DangerButton(string label, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.58f, 0.18f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.76f, 0.24f, 0.24f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.88f, 0.30f, 0.30f, 1f));
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }


    private sealed class NearbyPlayerOption
    {
        public NearbyPlayerOption(string name, string world, float distance)
        {
            Name = name;
            World = world;
            Distance = distance;
        }

        public string Name { get; }
        public string World { get; }
        public float Distance { get; }
        public string DisplayName => string.IsNullOrWhiteSpace(World) ? Name : $"{Name} @ {World}";
    }

    private static string GetStatusLabel(DrawingStatus status) => status switch
    {
        DrawingStatus.Draft => "DRAFT",
        DrawingStatus.Open => "SALES OPEN",
        DrawingStatus.Closed => "SALES CLOSED",
        DrawingStatus.Drawn => "WINNER DRAWN",
        DrawingStatus.Voided => "VOIDED",
        _ => status.ToString().ToUpperInvariant(),
    };

    private static Vector4 GetStatusColor(DrawingStatus status) => status switch
    {
        DrawingStatus.Draft => Muted,
        DrawingStatus.Open => Success,
        DrawingStatus.Closed => Gold,
        DrawingStatus.Drawn => Purple,
        DrawingStatus.Voided => Danger,
        _ => Muted,
    };
}
