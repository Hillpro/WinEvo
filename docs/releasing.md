# Releasing WinEvo

How a release is cut, and the shape its notes take. Follow this for every
release so the list on the Releases page reads consistently.

## Cutting a release

1. Bump `<VersionPrefix>` in [Directory.Build.props](../Directory.Build.props).
2. Commit it on its own, with the subject `X.Y.Z - <theme>`
   (e.g. `0.2.1 - Downloaded-build elevation fix`).
3. Push `main` first, so generated notes resolve against commits that exist
   upstream.
4. Tag **annotated**, never lightweight, with the description
   `WinEvo X.Y.Z - <same theme as the version-bump commit>`:

   ```bash
   git tag -a v0.2.1 -m "WinEvo 0.2.1 - Downloaded-build elevation fix"
   git push origin v0.2.1
   ```

5. [`release-portable.yml`](../.github/workflows/release-portable.yml) fires
   on `v*`: publishes the self-contained portable zip, attests build
   provenance, and creates the GitHub Release titled **`WinEvo X.Y.Z`**.
6. Replace the auto-generated notes with hand-written ones in the format
   below. The generated changelog is only a placeholder so the release is
   never empty.

To rehearse without publishing, run the workflow via `workflow_dispatch` —
it builds and uploads the artifact but skips the attestation and release
steps, both gated on the ref being a tag.

## Release-notes format

`##` headings, in this order. **Omit any section that has nothing to say**
— an empty heading is worse than no heading. Bullets lead with a bolded
phrase, then plain-language explanation of what it means for someone using
the app, not what the commit did.

One exception to the order: on a patch release whose whole reason for
existing is a fix, **Fixes** comes first. Everywhere else Highlights leads.

Write for someone who does not know the codebase. "Downloaded copies
couldn't run anything needing administrator rights" beats "cleared the
Zone.Identifier ADS before ShellExecute".

```markdown
## Highlights

- **Short bold lead-in.** What is new and why it is worth having. One or
  two sentences.

## Fixes

- **Short bold lead-in.** What was broken, what the user saw when it broke,
  and what happens now.

## Install

1. Download `WinEvo-X.Y.Z-win-x64.zip` below.
2. Extract anywhere — somewhere under your profile
   (e.g. `%LOCALAPPDATA%\WinEvo\`) needs no admin rights.
3. Run `WinEvo.exe`. On first launch SmartScreen shows "Windows protected
   your PC" because the build is not signed by a recognized authority —
   click **More info → Run anyway**.

## Verifying the download

- GitHub shows the SHA-256 digest of the zip on this page.
- The build carries a signed provenance attestation:
  https://github.com/Hillpro/WinEvo/attestations

## Known limitations

- What does not work yet, and anything carried over from earlier releases.

## Reporting bugs

Attach both logs:

- `%LOCALAPPDATA%\WinEvo\shell.log`
- `%LOCALAPPDATA%\WinEvo\agent.log`

GPLv3. Source: https://github.com/Hillpro/WinEvo

**Full Changelog**: https://github.com/Hillpro/WinEvo/compare/vX.Y.Z-1...vX.Y.Z
```

**The Full Changelog line is always the last line.** Copy it out of the
auto-generated notes before replacing them — it is the only part of the
generated text worth keeping. Format is exactly what GitHub emits:

```markdown
**Full Changelog**: https://github.com/Hillpro/WinEvo/compare/v0.2.0...v0.2.1
```

For the very first release there is no predecessor to compare against, so
it points at the commit list instead
(`https://github.com/Hillpro/WinEvo/commits/v0.1.0`).

### Rules that are easy to get wrong

- **Never claim a distribution channel that is not live.** The 0.2.0 notes
  told readers to search the Microsoft Store; that submission was refused,
  so the instruction was wrong the moment it was published. Only list
  channels a reader can actually use today.
- **Do not rewrite history that was true at the time.** The 0.1.0 notes
  point at `%TEMP%\winevo-agent.log`, which is where that build really
  logged. Leave it. Correct only claims that are false, not ones that have
  since been superseded.
- **Carry limitations forward** until they are actually fixed. A reader
  landing on the newest release should see the current limitations, not
  have to read back through older notes.
- Mark 0.x releases as **pre-release** on GitHub.
