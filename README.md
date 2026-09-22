<p align="center">
  <img src="v2rayN/v2rayN.Desktop/Assets/MehrON-logo.png" width="160" height="160" alt="MehrON Logo" />
</p>

<h1 align="center">MehrON</h1>

<p align="center">
  <a href="https://github.com/yastorovsky/MehrON/releases"><img src="https://img.shields.io/github/v/release/yastorovsky/MehrON?include_prereleases&label=download" alt="Release" /></a>
  <a href="https://github.com/yastorovsky/MehrON/releases"><img src="https://img.shields.io/github/downloads/yastorovsky/MehrON/total?label=downloads" alt="Downloads" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20x64-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-purple" alt=".NET 10" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-green" alt="License" /></a>
</p>

<p align="center">
  Windows desktop proxy client with system proxy, TUN mode, and multi-core support —
  built from the PattN / Patterniha codebase, based on v2rayN.<br />
  ⭐ Built-in <strong>SNI spoofing</strong> + <strong>MHR relay</strong> for internet shutdowns and emergency conditions.
</p>

<p align="center">
  <strong>English</strong> | <a href="README.fa.md">فارسی</a>
</p>

## App screenshot

![MehrON app screenshot](docs/images/app-screenshot.png)

## Features

### Connection
- Import and manage subscription links and supported share links.
- One-click Windows system proxy control with exceptions and PAC support.
- Optional TUN mode to route device traffic through the selected profile.
- Per-subscription update controls in Subscription Settings.

### Cores
The portable release bundles ready-to-run runtimes — no separate core download needed:

| Core | Use |
| ---- | --- |
| Xray | VLESS / VMess / Trojan / Shadowsocks, including Reality and XTLS |
| sing-box | Modern protocols including Hysteria2, TUIC, WireGuard |
| mihomo | Clash Meta–compatible rule-based routing |
| Aether | Censorship circumvention (MASQUE, WireGuard, pluggable transports) |
| 🔥 SNI Spoofing | DPI bypass with IP/TCP-header manipulation — no server needed |
| 🛟 MHR Relay | Domain-fronted relay via Google Apps Script — only a free Google account needed |

> [!IMPORTANT]
> **🛡️ Shutdown & emergency ready:** SNI Spoofing and MHR are built for
> heavily filtered networks, throttling, and partial / full internet shutdowns —
> when normal profiles and servers stop working, these modes can keep you connected.
>
> - **SNI Spoofing:** bypasses DPI by manipulating IP/TCP headers. No subscription or VPS required.
> - **MHR:** routes traffic through your own Google Apps Script relay with domain fronting
>   (`Browser -> Local proxy -> Google front -> Your Apps Script relay -> Target site`);
>   the network filter only sees a Google-facing connection. Optional Cloudflare / VPS exit node for sites blocking Google IPs.

> [!TIP]
> Official portable releases ship with the `bin/` core binaries and up-to-date
> `geoip.dat` / `geosite.dat` routing databases already in place — no extra
> downloads are needed before first connect.

### Routing and DNS
- Visual routing rules editor with bypass / proxy / direct presets.
- DNS settings with hijack and FakeIP support.
- SNI spoofing engine and MHR relay are configured from the Cores section above.

### Settings for everyone
- Option settings are grouped by topic (connection, core, system proxy, TUN, appearance…).
- **Novice mode:** turn off *Show advanced settings* on top of the Option window
  to hide Fragment, MUX, update sources, and advanced TUN options.

### Test and maintain
- Profile latency / speed test with configurable endpoints.
- Server statistics, subscription auto-update, backup and restore.
- Beta update channel with automatic update checks.

## Quick start (portable release)

The portable Windows package is distributed as `MehrON-windows-64.zip`
(one archive — no nested zips).

1. Extract the archive to a writable folder.
2. Run `MehrON.exe`.
3. Accept the Windows administrator prompt when using TUN mode or other features that require elevated permissions.
4. Import a profile or add a subscription link.

> [!IMPORTANT]
> TUN mode installs a virtual network adapter and needs administrator rights.
> If you only use the system proxy mode, elevation is not required.

The release intentionally contains no saved profiles, subscriptions, logs, or generated runtime configuration files.

## Build from source

Requirements:

- Windows
- .NET SDK 10.0 (see `global.json`)

Build the WPF desktop client from the repository root:

```powershell
dotnet build .\v2rayN\v2rayN\v2rayN.csproj -c Release
```

The output is written to `v2rayN\v2rayN\bin\Release\`.

> [!NOTE]
> Runtime binaries are not produced by the .NET build. A portable release must
> include the required Xray, sing-box, mihomo, and Aether files beneath its
> `bin` directory before it can run standalone.

## Repository layout

```text
v2rayN/                      Main desktop client source
_upstream_sni_spoofing/      SNI spoofing integration source
_upstream_mhr/               MHR integration source
_upstream_mhr_cfw/           MHR-CFW integration source
```

## Security and privacy

> [!WARNING]
> Do not commit or publish personal profiles, subscription URLs, generated
> `guiNConfig` folders, logs, or runtime `config.json` files containing
> credentials. Use placeholders in examples and keep private connection data
> outside the repository.

## License and acknowledgements

MehrON is distributed under the GPL-3.0 license; see [LICENSE](LICENSE). It was created from the PattN / Patterniha codebase and includes or integrates with third-party projects that have their own licenses and notices, including v2rayN, Xray-core, sing-box, Aether, MHR, MHR-CFW, and the SNI spoofing component.
