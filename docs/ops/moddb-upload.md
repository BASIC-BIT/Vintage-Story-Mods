# ModDB Upload Checklist

ModDB has no public upload API. Uploads are manual or must be automated via browser automation.

Checklist:
1. Download the packaged zip from the GitHub Release.
2. Verify the zip contents:
   - `modinfo.json`
   - `thebasics.dll`
   - `assets/` (if present)
3. Verify the version string in `modinfo.json` matches what you intend to publish.
4. Upload to ModDB and set the version appropriately.
5. Paste release notes/changelog.
6. Verify the public page reflects the new file/version.

Automation option:
- Use Playwright with a human-authenticated session.
- First do an exploratory pass and record stable UI anchors (role/name/labels).
