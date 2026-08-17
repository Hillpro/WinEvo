# Distribution

How WinEvo reaches users, and why the packaged (MSIX / Store) path was
abandoned. Read the post-mortem before proposing MSIX again — it has been
tried, and it failed for a reason that no amount of manifest work fixes.

## Channels

| Channel | Status | Notes |
|---|---|---|
| **Portable zip** (`win-x64`, self-contained) | ✅ shipping | Download, extract, run. No install, no admin, no prerequisites. Built and published by [release-portable.yml](../.github/workflows/release-portable.yml) on every `v*` tag. |
| **Signed installer** (`.msi` / `.exe`) | 🔲 planned | The replacement for the packaged path. Also the route to a Microsoft Store listing, via Store policy **10.2.9** — non-gaming products may submit a download URL to a signed installer instead of a package. |
| **MSIX / Store package** | ❌ abandoned | See below. |

The portable zip is not a fallback — it is the channel that serves users
who cannot install software or obtain admin rights, and it stays
self-contained for exactly that reason.

## Packaged path post-mortem

Two Store submissions were refused, both citing the
`unvirtualizedResources` restricted capability rather than the
justification text.

### The technical problem

Under MSIX the agent — even declared `windows.fullTrustProcess` — inherits
the package's Windows Container silo. Procmon showed `RegSetValue` landing
at `\REGISTRY\WC\Silo<guid>\…\BingSearchEnabled` instead of real HKCU. The
agent's own `registry-read` went through the same silo, so a toggle looked
correct in the UI across restarts while SearchUI, regedit, and every
non-packaged process saw the unchanged real value. The setting never
actually changed.

Modern MSIX uses kernel Application Silos, not the old
`…\Packages\<PFN>\RegistryUserSettings\*.dat` overlay, which is why nothing
appears on disk to explain it. `runFullTrust` grants Win32 API access but
does not escape the silo, and
`uap10:RuntimeBehavior="packagedClassicApp"` + `uap10:TrustLevel="mediumIL"`
are already the implicit defaults — both were tested and reverted.

### The fix that worked technically and failed commercially

`<rescap:Capability Name="unvirtualizedResources" />` **plus**
`<desktop6:RegistryWriteVirtualization>disabled</desktop6:RegistryWriteVirtualization>`
in `<Properties>` — the capability is the gate, the property is the actual
switch, and both are required. Verified working under Procmon: the elevated
agent's writes reached real HKCU.

Then certification refused it, because
[the documented scope](https://learn.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations)
is:

> "This capability is designed for certain types of desktop PC games that
> are published by Microsoft and our partners. It's also needed for apps
> packaged with external location. It is not intended to be used for other
> scenarios, because it could compromise the system's ability to uninstall
> cleanly."

A tweaker is the excluded scenario by construction: settings surviving
uninstall is the whole point, and policy 10.2.7 requires clean uninstall.

### Every alternative, and why each is closed

**Per-key exclusion needs the same capability.** Windows 11 added
`virtualization:ExcludedKeys`, unvirtualizing *named* HKCU keys rather than
the whole hive — plausible for a build shipping only curated first-party
actions, where every key can be enumerated. It does not help:
[virtualization:ExcludedKey](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-virtualization-excludedkey)
states "This element requires the `unvirtualizedResources` restricted
capability", and MSDN's own example carries the `rescap:Capability`
declaration alongside it. Fine-grained or whole-hive, same refused gate.

**A packaged service is worse, not better.** It requires
`packagedServices`, plus `localSystemServices` for a LocalSystem account.
Both are documented as for "Microsoft partners and enterprises" and both
carry the strongest refusal language in the capability table — *"In most
cases, the use of this capability won't be approved."* That trades one
restricted capability for two more hostile ones, and it is unverified
whether a packaged service escapes the container at all.

**Installing a service from a separate MSI is blocked by policy.** 10.1.5
permits post-download acquisition of add-ons "**excluding** non-Microsoft
drivers or NT services"; 10.2.5 requires products submitted as packages to
be installed and updated only through the Store; 10.2.4 calls dependency on
non-Microsoft NT services "generally not allowed". Technically it would
have worked — an MSI-installed service runs outside the container — but it
also needs the service to resolve the caller's SID and write `HKU\<sid>\…`,
because a session-0 LocalSystem service has no HKCU of the logged-in user.

**Escaping the container is not an option even where it would work.**
Launching the agent outside package context (for example via a registered
scheduled task) may well defeat the silo, but policy 10.6 states "You must
not circumvent operating system checks for capability usage." That is a
bad-faith submission and risks the developer account, not just the app.

### What still works under MSIX, and why it is not enough

HKLM writes are not virtualized:

> "Writes under **HKLM** are allowed as long as a corresponding key/value
> doesn't exist in the package hive and the user has the correct access
> permissions (which effectively means this is only available to a
> Centennial app running elevated)."

So a packaged build with *no* restricted capability could still perform
machine-scope tweaks through the already-elevated agent. But
`BingSearchEnabled`, `CortanaConsent`, and essentially every
consumer-facing tweak — Explorer, taskbar, search, personalization — live
in HKCU. The surviving slice is not the product.

This is documented behaviour, never verified here: the Procmon session only
covered HKCU. Re-test before relying on it.

## Consequences of leaving MSIX

- **No automatic updates.** The Store does not auto-update EXE/MSI
  products — "the application is fully responsible for upgrading itself."
  Submitting a new version to Partner Center only ensures *new* downloads
  are current. WinEvo has no update mechanism today; users re-download.
- **Code signing becomes mandatory rather than optional.** Store policy
  10.2.9 requires the installer and all its PE files to chain to a CA in
  the Microsoft Trusted Root Program. It is also the durable fix for
  SmartScreen blocking downloaded builds.
- **The Shell/Agent split stays**, but its justification changes. It was
  originally forced by MSIX being unable to run elevated; it is now a
  deliberate least-privilege design — the UI runs as a standard user and
  only a separate helper is ever elevated. Do not collapse it.

## Related

- [architecture.md](architecture.md) — the process model behind the split.
- [releasing.md](releasing.md) — how a release is cut and what its notes look like.
- [security-model.md](security-model.md) — trust boundaries and elevation.
