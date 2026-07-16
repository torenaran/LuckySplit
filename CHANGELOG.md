# Changelog

## 1.1.0

- Expanded field descriptions and helper text throughout setup, drafts, purchase entry, presets, ledgers, history, and transparency sections.
- Reworked important form labels so explanations wrap above full-width controls instead of being squeezed beside the input values.
- Added an optional venue-funded starting pot to presets and individual draft drawings.
- The starting pot now joins ticket revenue before the configured winner percentage is calculated.
- Added total prize pool and projected venue share displays throughout the host workflow.
- Included the starting pot, total pool, and payout calculation in announcements, CSV exports, history, and verification receipts.
- Added the `LSPLIT3` receipt protocol so the locked starting pot and whole-pool split rule are committed when sales open.
- Preserved verification and payout behavior for existing `LSPLIT1` and `LSPLIT2` drawings.
- Added stable client transaction IDs, seller attribution, and ledger revision metadata for future synchronized sales-team support.
- Updated public installation instructions for the Lucky Split custom Dalamud repository.

## 1.0.0

- Initial public release.
- Added reusable venue presets and first-launch setup.
- Added nearby-player selection and manual purchase entry.
- Added automatic ticket ranges, live totals, history, CSV exports, and reproducible receipts.
- Added confirmation-protected drawing controls and the branded Lucky Split interface.
