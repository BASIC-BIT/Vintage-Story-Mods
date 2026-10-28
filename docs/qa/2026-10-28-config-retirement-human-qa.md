# Configuration retirement: pending human QA

This is a new QA batch for the sidebar editor's replacement actions and command compatibility. None of these cards has been run in-game or marked passed. Profile2 is unchanged. Existing QA history in other files remains unchanged.

Review the standalone previews first. Starting this batch needs owner approval and a separate staging decision. Use a disposable server and backed-up configuration for reload and reviewed-metadata tests. Record the observed result against each card before deciding whether command switching is ready.

## Current routing

`/tb config`, its language/sheet subcommands, and the direct sheet aliases still open the legacy editors. `/tb config advanced` is the explicit fallback. `/tb setup languages` and `/tb setup charsheetfields` open pages in the shared sidebar draft. Their `language`, `sheetfields`, and `biofields` aliases allow testing before the old routes switch. This temporarily leaves two command paths for the same settings.

## Batch A: draft and navigation, no disk changes

1. **Shared draft through direct commands.** Open `/tb setup`, change Play speech voices, a language description, and a sheet field label. Run `/tb setup languages`, `/tb setup sheetfields`, and repeat each twice. Expect the selected page to change while all three edits remain in Review. Failure: a fresh draft, missing edit, or competing editor.
2. **Help return.** Scroll to a lower field, change its text, and open Help. Close the handbook. Expect the same page, text and scroll position in setup. Repeat from search results. Failure: lost query, changed draft or incorrect return page.
3. **Dirty reload cancellation.** With unsaved edits, choose Server tools > Diagnostics > Reload from disk. Cancel and repeat using Escape. Expect every edit to remain and no reload request. Failure: values change or the editor closes permanently.
4. **Compatibility and permissions.** Confirm `/tb config advanced` opens the legacy panel, and existing config/language/sheet aliases keep their current destinations. Try the new setup aliases as a non-root player and from the server console. Expect root enforcement and player-only errors. Revoke root while setup is open; attempt a save and a maintenance action. Expect denial with edits retained. Restore root and reopen to recover the draft.
5. **Delayed save.** In an approved local network-delay fixture, save scalar, language and sheet edits together. Repeat targeted setup commands before the result arrives and after a timeout. Expect one retained draft, no second save while status is unknown, and correct recovery after acknowledgement or readback. A delayed success or failure must leave the selected language/sheet page in place. Repeat while Help is open over scrolled search results, then close Help and verify the query, result position and scroll remain. Include a restart-required setting in a delayed successful save. Expect a restart notice without a forced page change, and the saved setting name in Review changes alongside any newer unsaved edits. Failure: duplicate save, cleared edits or a fresh session.

## Batch B: reviewed metadata

6. **Explicit review only.** Seed a disposable config with two registered keys absent from `ReviewedConfigSettingKeys`. Open New settings and confirm exactly those two appear. Visit both pages and save an unrelated change. Reopen and confirm both are still new. Choose Mark all new settings reviewed. Confirm the filter clears, the reviewed metadata persists after reopening, and unsaved settings remain unsaved. Failure: visits or ordinary saves mark keys reviewed, or the metadata action saves the draft.
7. **Older server.** Connect a compatible older server that omits maintenance capability and reviewed metadata. Expect maintenance and New settings filtering to be unavailable while ordinary search and editing still work. Failure: guessed new-setting labels or a metadata/reload packet sent to the unsupported server.

## Batch C: disk reload and atomic save

8. **Malformed disk file.** Back up the disposable server config, make a recognizable unsaved draft, and write malformed JSON to the disposable config file. Confirm Reload from disk. Expect an error, unchanged runtime settings and the intact unsaved draft. Restore the original file before continuing.
9. **Successful disk reload.** Make a valid disk-only change to Play speech voices, then make a different unsaved draft edit. Confirm Reload from disk. Expect the disk values to replace the draft only after success. Reopening setup alone must not reload the file. Restore the backup afterward.
10. **Atomic mixed save.** Change one scalar, a language and a sheet field. Submit with one invalid field. Expect no component to persist. Correct it and save. Expect all three changes in the single accepted snapshot. Verify restart-labelled settings separately after an approved restart; a successful save does not prove activation.

11. **Valid load with activation failure.** In an approved disposable failure fixture, load a valid config while forcing a live side effect or client broadcast to throw. Expect a distinct message that disk settings loaded but some changes failed and a restart is needed. Expect unsaved edits to remain, clean fields to refresh from the loaded server values, and restart status to remain visible. Do not expect rollback of side effects that already ran. A later malformed-file reload must still report a file-load failure and leave those runtime values unchanged. Keep this fixture separate from normal reload testing.

## Acceptance record

All cards are pending. Standalone renders and automated fixture tests do not replace these observations. Default-route switching and legacy-window deletion remain separate acceptance decisions.
