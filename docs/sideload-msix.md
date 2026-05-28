# Sideloading the MSIX

Sideload-installing an MSIX means: sign with a certificate, install that certificate as trusted on the target machine, then install the package. This is the install path for any MSIX that isn't signed by a CA already trusted by Windows — useful for testing the packaged Shell + Agent end-to-end (the `windows.fullTrustProcess` extension and the UAC-elevation flow under MSIX sandbox rules) without going through a Store distribution.

> **Status.** Local cert generation, signing, and sideload install are wired and verified. Public code-signing (a CA-issued cert, or a Store-signed package) is still TODO.

## One-time on the dev machine: generate a self-signed cert

The cert's subject **must** match the `Publisher` attribute in [src/WinEvo.Shell/Package.appxmanifest](../src/WinEvo.Shell/Package.appxmanifest) exactly. Today that's `CN=Hillpro` — keep these in sync if either changes.

Run from the repo root in PowerShell:

```powershell
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=Hillpro" `
    -KeyUsage DigitalSignature `
    -FriendlyName "WinEvo Dev Cert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter (Get-Date).AddYears(2)

# Export the public CER for sideload-install on target machines.
Export-Certificate `
    -Cert ("Cert:\CurrentUser\My\" + $cert.Thumbprint) `
    -FilePath "src/WinEvo.Shell/WinEvo.Shell_DevCert.cer" | Out-Null

"Cert thumbprint: $($cert.Thumbprint)"
```

Take the printed thumbprint and put it in `src/WinEvo.Shell/WinEvo.Shell.csproj.user` (file is gitignored — your local override only):

```xml
<Project>
  <PropertyGroup>
    <AppxPackageSigningEnabled>True</AppxPackageSigningEnabled>
    <PackageCertificateThumbprint>YOUR-THUMBPRINT-HERE</PackageCertificateThumbprint>
  </PropertyGroup>
</Project>
```

If the file already exists (VS creates it for launch-profile state), append the `<PropertyGroup>` block — don't replace what's there.

The cert lives in your `CurrentUser\My` store; MSBuild reads it from there at sign time. The `.cer` file is just the public-key export for sideload-install; the private key never leaves your store.

## Build a signed MSIX

With the `.csproj.user` in place, the Shell csproj engages signing automatically:

```bash
dotnet publish src/WinEvo.Shell -c Release -r win-x64 \
  -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true
```

Output: `src/WinEvo.Shell/AppPackages/WinEvo.Shell_<ver>_x64_Test/WinEvo.Shell_<ver>_x64.msix`. Verify it's signed:

```powershell
Get-AuthenticodeSignature src/WinEvo.Shell/AppPackages/WinEvo.Shell_*_x64_Test/*.msix
```

`Status` should be `UnknownError` with `StatusMessage` indicating an untrusted root — that's the expected state for a self-signed cert before the user installs it. `NotSigned` would mean signing didn't engage.

## Install on the target machine

On the machine where you want to install WinEvo (your dev box or the smoke-test VM):

1. **Copy** both the `.msix` and the `.cer` to the target.
2. **Install the cert as Trusted People.** PowerShell **as Administrator**:

   ```powershell
   Import-Certificate `
       -FilePath WinEvo.Shell_DevCert.cer `
       -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   This is the minimum trust needed for sideload-install of MSIX packages. It does **not** make the cert trusted for general code-signing on the system.

3. **Install the package.** PowerShell:

   ```powershell
   Add-AppxPackage WinEvo.Shell_<ver>_x64.msix
   ```

   Or double-click the `.msix` to launch the App Installer GUI.

4. **Launch.** The packaged Shell appears in Start as "WinEvo".

## Uninstall

```powershell
Get-AppxPackage Hillpro.WinEvo | Remove-AppxPackage
```

Or use Settings → Apps → Installed apps. The logs at `%LOCALAPPDATA%\WinEvo\` (`shell.log` and `agent.log`) are not removed automatically — clear that folder manually if you want a fully clean state.

## Removing the trusted dev cert

Once you no longer need to install signed-by-dev-cert WinEvo builds:

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople `
  | Where-Object { $_.Subject -eq "CN=Hillpro" } `
  | Remove-Item
```

## When this changes

This whole flow is dev-time scaffolding. Once the project ships on the Microsoft Store, end-users get a Store-signed package and don't need the cert install step at all. The portable zip remains unsigned (SmartScreen "Run anyway") until a code-signing cert is procured.
