# Scene-marker appearance picker

## Accepted design

BASIC requested all three approaches together: modeled 3D symbol, camera-facing billboard with no visible base, and hybrid physical base plus billboard. The icon invites nearby players to approach. Billboard visibility distance is configurable per marker in the editor, with Unlimited available. Only the billboard fades; physical geometry remains visible normally. Descriptions remain targeted-only, walls obscure the icon, and appearance changes preserve content, creator and padlock permissions.

## Implementation choices

- Retain Stone for old markers and as a subtle appearance choice. Add Model, Billboard and Hybrid modes; symbols are exclamation, question and information.
- Editor uses mode/symbol tiles and a live appearance preview. Shared shape cuboids supply modeled geometry, billboard artwork and preview artwork.
- Default billboard distance is 24 blocks. Fade runs from full opacity at 75% of the distance to zero at the configured distance. Finite values accept 1-1024 blocks; Unlimited removes distance fading but cannot load terrain beyond the client's normal loaded area.
- Billboards have a fixed world size, face the camera and use terrain depth testing. Block entities register on client initialization and unregister on removal/unload; no world-wide scanning or forced chunk loading.
- Save packets append fields and use the existing server-side claim, distance and lock checks. Appearance settings persist through world and item attributes; old data defaults to Stone.

## QA follow-up (not yet observed)

1. Open the editor on ground and wall markers. Click every mode and symbol; verify preview, saved selection and readability at the current GUI scale. Cancel must discard changes.
2. Compare 3D, Billboard and Hybrid simultaneously. Walk around and above them: only billboards face the camera, models retain their orientation, hybrid retains its base, billboard has no visible base. Target each symbol to inspect/edit it.
3. Set distance 24: invisible beyond 24 blocks, half opacity near 21, fully visible by 18. Switch to Unlimited, walk farther away within loaded terrain and confirm it remains visible with natural distance shrinkage. Walls must obscure the icon.
4. Change symbol and mode from the other claim-authorized client, then pick up/replace and save/reload. Check content, appearance, distance, creator and padlock persistence. Locked markers must reject appearance edits, including an editor opened before locking.
5. Verify physical models/bases never distance-fade; old stone markers retain their appearance. Leave/re-enter loaded terrain and reconnect without ghost icons, exceptions or duplicated symbols.

Review baseline: 65b71f6c1ce88c69fc89699d7fe9ba29bb8ea257. This implementation stays on the current branch. No public release or merge is implied.

## Verification checkpoint

- Full test suite: 763 passed, zero failed or skipped (24 additional cases beyond the padlock checkpoint).
- Standard build-and-package completed; packaged DLL is byte-identical to the DLL in the passing test output. New shape assets, English localization and handbook JSON parse successfully from the ZIP.
- Candidate ZIP SHA256: DF44C2C746A597FBB9023D9CED4718598D2AC7FC21B931E167FD3C190E118BD9. Package is at mods-dll/thebasics/thebasics_5_9_1.zip, also staged under .tmp/scene-appearance-package/Mods inside this worktree.
- Standards review: no remaining blockers. Spec review: no remaining blockers. Review fixes preserve hybrid-base selection, use a depth-tested world billboard, sort transparent symbols back-to-front, and apply standard-alpha fading so fog fades with the symbol.
- The new appearance package has not been installed into game profiles or uploaded to the QA server. Runtime visual acceptance remains pending; include fog, overlapping symbols, steep camera pitch and partial wall occlusion in the cards above.
