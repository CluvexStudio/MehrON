# MehrN

MehrN is a Windows desktop proxy client created from the PattN / Patterniha codebase, which is based on v2rayN. It provides a graphical interface for importing, organizing, testing, and running supported proxy profiles.

## Features

- Import and manage subscription links and supported share links.
- Test profile latency and control the Windows system proxy.
- Optional TUN mode for routing device traffic through the selected profile.
- Bundled Xray and sing-box runtime support in the portable release.
- Per-subscription update controls in Subscription Settings.
- Optional advanced settings for SNI spoofing and MHR integrations.

## Portable release

The portable Windows package is distributed as `MehrN-clean-release.zip`.

1. Extract the archive to a writable folder.
2. Run `MehrN.exe`.
3. Accept the Windows administrator prompt when using TUN mode or other features that require elevated permissions.
4. Import a profile or add a subscription link.

The release intentionally contains no saved profiles, subscriptions, logs, or generated runtime configuration files.

## Build from source

Requirements:

- Windows
- .NET SDK 10.0 (see `global.json`)

Build the desktop client from the repository root:

```powershell
dotnet build .\v2rayN\v2rayN.Desktop\v2rayN.Desktop.csproj -c Release
```

The output is written to:

```text
v2rayN\v2rayN.Desktop\bin\Release\net10.0\MehrN.exe
```

Runtime binaries are not produced by the .NET build. A portable release must include the required Xray and sing-box files beneath its `bin` directory.

## Repository layout

```text
v2rayN/                     Main desktop client source
_upstream_sni_spoofing/      SNI spoofing integration source
_upstream_mhr/               MHR integration source
_upstream_mhr_cfw/           MHR-CFW integration source
```

## Security and privacy

Do not commit or publish personal profiles, subscription URLs, generated `guiConfigs` folders, logs, or runtime `config.json` files containing credentials. Use placeholders in examples and keep private connection data outside the repository.

## License and acknowledgements

MehrN is distributed under the GPL-3.0 license; see [LICENSE](LICENSE). It was created from the PattN / Patterniha codebase and includes or integrates with third-party projects that have their own licenses and notices, including v2rayN, Xray-core, sing-box, MHR, MHR-CFW, and the SNI spoofing component.
