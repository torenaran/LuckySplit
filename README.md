# Lucky Split

Lucky Split is a host-side Dalamud plugin for organizing 50/50 drawings at Final Fantasy XIV venues.

It provides a guided workflow for choosing a venue preset, recording confirmed ticket purchases, tracking the growing prize pool, closing sales, drawing a winner, publishing announcements, and retaining a permanent event record.

## Features

- Contextual field descriptions and workflow guidance throughout the host interface.

- Reusable presets for different venues and recurring events
- Optional venue-funded starting pot stored per preset
- Starting pot included in the configured winner/venue split
- Guided first-launch preset setup
- Nearby-player picker with character and home-world autofill
- Manual entry for players who are not currently loaded
- Automatic consecutive ticket ranges
- Live winner payout, total prize pool, ticket revenue, venue share, ticket, and entrant totals
- Searchable purchase ledger and drawing history
- Close, reopen, draw, void, and archive workflows
- Confirmation prompts for important actions
- Reproducible drawing receipts based on the finalized ledger
- Copyable opening, sales-update, winner, and verification messages
- CSV ledger exports
- Branded interface, welcome splash, and plugin icon

The total prize pool is the venue starting pot plus ticket revenue. The configured winner percentage is applied to that full pool, and the remaining amount becomes the venue share.

Lucky Split never moves gil, completes trades, inspects balances, or sends chat automatically. Venue staff remain responsible for confirming payments and distributing winnings.

## Commands

- `/luckysplit`
- `/lucky`
- `/5050`

## Installation

1. Open **Dalamud Settings** with `/xlsettings`.
2. Select **Experimental**.
3. Add this URL under **Custom Plugin Repositories**:

```text
https://raw.githubusercontent.com/torenaran/LuckySplit/main/repo.json
```

4. Save the settings.
5. Open `/xlplugins`.
6. Search for **Lucky Split** and select **Install**.

## Building from source

Requirements:

- .NET 10 SDK
- Dalamud API 15 development environment

Build and create the release package with:

```powershell
.\Build-XmaRelease.ps1
```

The completed archive is written to:

```text
dist\LuckySplit-v1.1.0-XMA.zip
```

## Collaboration roadmap

Version 1.1.0 adds stable transaction identifiers, seller attribution fields, and drawing revision tracking. These fields prepare the ledger for a future opt-in synchronized sales-team mode. Live multi-seller operation is not enabled until an authoritative relay is available to assign ticket ranges and resolve simultaneous submissions safely.

## License

Lucky Split is released under the MIT License. See `LICENSE` for details.
