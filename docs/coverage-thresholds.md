# Coverage Threshold Strategy

## Current Thresholds

| Metric | Warn | Fail | Actual (as of Phase 3) |
|--------|------|------|------------------------|
| Line   | 5%   | 3%   | ~9.7%                  |
| Branch | —    | —    | ~6.4%                  |

Thresholds are configured in two places:
- **CI enforcement**: `.github/workflows/build.yml` → `CodeCoverageSummary` step (`thresholds` parameter)
- **Exclusions**: `mods-dll/thebasics.Tests/coverlet.runsettings` → `ExcludeByFile` list

## Excluded Files

The following files are excluded from coverage calculations because they require the
Vintage Story game runtime and cannot be unit tested:

- **Rendering**: `RichTextTextureUtils.cs`, `TypingIndicatorRenderer.cs`, `RpTextEntityPlayerShapeRenderer.cs`
- **Harmony patches**: `SpeechBubbleVtmlPatches.cs`, `NameTagRenderRangePatches.cs`, `ChatUiSystem.cs`
- **UI dialogs**: `CharacterBioDialog.cs`
- **ModSystem lifecycle**: `BaseBasicModSystem.cs`, `BaseSubSystem.cs`, all `*System.cs` mod entry points
- **Network infrastructure**: `SafeClientNetworkChannel.cs`
- **Debug instrumentation**: `PerfStats.cs`

## Ratchet Plan

The threshold is intentionally set **below** current coverage to prevent regression
without blocking PRs that don't add tests.  Bump the threshold after each significant
batch of new tests:

1. **Phase 3 (done)**: Initial pure-function tests → threshold set to 3% fail / 5% warn
2. **After Phase 5** (testability refactors + mock-based tests): bump to ~15-20%
3. **Long-term target**: 40-50% line coverage (realistic ceiling given untestable game-runtime code)

### How to Bump

1. Run tests locally with coverage to get the current number:
   ```powershell
   dotnet test mods-dll\thebasics.Tests\thebasics.Tests.csproj `
     --configuration Release `
     --settings mods-dll\thebasics.Tests\coverlet.runsettings `
     --collect:"XPlat Code Coverage" `
     --results-directory TestResults
   ```
2. Check the `line-rate` in the generated `coverage.cobertura.xml`
3. Set the fail threshold ~2-3% below the current value
4. Set the warn threshold ~1% below the current value
5. Update this table

## Future Considerations

- **Diff coverage**: Consider adding Codecov for per-PR diff coverage enforcement
  (ensures new/changed files have tests, without requiring full-project coverage).
  This adds an external SaaS dependency so it was deferred.
- **Branch coverage**: Not enforced yet. Enable once line coverage is stable above 20%.
