# Patterniha SNI-Spoofing integration

MehrN bundles the Windows release of [patterniha/SNI-Spoofing](https://github.com/patterniha/SNI-Spoofing) when packaging Windows archives.

The integration starts the upstream executable with a generated `config.json` for the selected profile. The executable uses WinDivert to inject a fake TLS ClientHello with an intentionally invalid TCP sequence number. Windows administrator privileges are required.

The upstream project is GPL-3.0 licensed. Keep its license and attribution with redistributed builds.
