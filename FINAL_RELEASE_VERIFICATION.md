# Final Release Verification

Before publishing Lucky Split 1.0.0:

1. Run `Build-XmaRelease.cmd`.
2. Confirm the build ends with `Build succeeded`.
3. Confirm `dist\LuckySplit-v1.0.0-XMA.zip` contains:
   - `LuckySplit.dll`
   - `LuckySplit.json`
   - `INSTALLATION.txt`
   - `LICENSE`
   - `CHANGELOG.md`
4. Extract the release ZIP to a fresh folder.
5. Add that folder to Dalamud Dev Plugin Locations.
6. Confirm the first-launch splash, preset setup, nearby-player picker, ticket sales, winner draw, history, CSV export, and restart persistence.
7. Confirm the active preset is clearly marked and switches correctly.
8. Check `/xllog` for errors.
9. Upload the exact tested ZIP to the public release and XMA listing.
