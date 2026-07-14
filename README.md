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

Custom repository URL:
https://raw.githubusercontent.com/torenaran/LuckySplit/main/repo.json

1. Open Dalamud Settings in FFXIV.
2. Select Experimental.
3. Paste the URL above under Custom Plugin Repositories.
4. Select the + button, then Save and Close.
5. Open /xlplugins.
6. Search for Lucky Split and select Install.
7. Open Lucky Split with /luckysplit, /lucky, or /5050.

Updates are delivered through the Dalamud Plugin Installer.

Lucky Split does not move gil or complete trades. Venue staff must confirm payments and distribute winnings manually.

## Building from source

Requirements:

- .NET 10 SDK
- Dalamud API 15 development environment

```

## License

Lucky Split is released under the MIT License. See `LICENSE` for details.
