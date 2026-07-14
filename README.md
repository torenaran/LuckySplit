# Lucky Split

Lucky Split is a host-side Dalamud plugin for organizing 50/50 drawings at Final Fantasy XIV venues.

It provides a clear workflow for choosing a venue preset, recording confirmed ticket purchases, tracking the live pot, closing sales, drawing a winner, publishing announcements, and retaining a permanent event record.

## Features

- Reusable presets for different venues and recurring events
- Guided first-launch preset setup
- Nearby-player picker with character and home-world autofill
- Manual entry for players who are not currently loaded
- Automatic consecutive ticket ranges
- Live pot, winner prize, venue share, ticket, and entrant totals
- Searchable purchase ledger and drawing history
- Close, reopen, draw, void, and archive workflows
- Confirmation prompts for important actions
- Reproducible drawing receipts based on the finalized ledger
- Copyable opening, sales-update, winner, and verification messages
- CSV ledger exports
- Branded interface, welcome splash, and plugin icon

Lucky Split never moves gil, completes trades, inspects balances, or sends chat automatically. Venue staff remain responsible for confirming payments and distributing winnings.

## Commands

- `/luckysplit`
- `/lucky`
- `/5050`

## Installation

1. Download `LuckySplit-v1.0.0-XMA.zip` from the latest release.
2. Extract it to a permanent folder.
3. Open **Dalamud Settings** in FFXIV.
4. Open **Experimental**.
5. Add the extracted Lucky Split folder under **Dev Plugin Locations**.
6. Open the Dalamud Plugin Installer and enable **Lucky Split** under developer plugins.
7. Enter `/luckysplit` in chat.

Manual installations do not update automatically. Replace the installed files when a newer release is published.

## Building from source

Requirements:

- .NET 10 SDK
- Dalamud API 15 development environment

Build and create the XMA release package with:

```powershell
.\Build-XmaRelease.ps1
```

The completed archive is written to:

```text
dist\LuckySplit-v1.0.0-XMA.zip
```

## License

Lucky Split is released under the MIT License. See `LICENSE` for details.
