# Billboard-only QA refinement

BASIC's in-game feedback selected the billboard and retired the stone/model/hybrid choices. Requested a slight idle bob and removal of the editor's range explanation footer. Follow-up explicitly moved locking into the editor and removed padlock interactions and item-description lock text.

Implemented:
- Billboard only, with old stored appearance values migrated on read without changing description, creator or lock. Symbol tiles, preview and distance/Unlimited controls remain.
- Five-centimetre vertical amplitude, four-second idle cycle. Fixed selection bounds cover the bob; fade distance uses the stationary anchor.
- Creator uses Save & lock, with content and lock applied together only after server permission checks. Locked editors are read-only; creator or controlserver admin with claim access and within reach can Unlock. Reopening restores the appropriate editor state.
- No physical padlock required or returned. Existing consumed-item locks remain locked. Removed marker-specific padlock Harmony patch, reinforcement behavior, tooltip lock instructions and retired preview/model rendering.

Validation: 762 tests pass; standards and spec reviews reported no remaining blockers. Standard build/package passed. Candidate SHA256 EFD4324899EFD1197764D7C4CA01C1FEDFEF164CC333C18734536BD4E688E838.

Active QA update: uploaded to test server 8982de16 and downloaded for matching hash verification; both profiles match. Prior ZIPs backed up under .tmp/scene-billboard-qa-2026-09-08. Server restart requested. Verify gentle motion, footer removal, Save & lock followed immediately by Unlock, another user's read-only editor and stale-editor save rejection. Visual acceptance remains pending.
