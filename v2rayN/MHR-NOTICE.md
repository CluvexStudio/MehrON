# MHR and MHR-CFW integration

MehrN packages the source releases of [MasterHttpRelayVPN](https://github.com/masterking32/MasterHttpRelayVPN) and [mhr-cfw](https://github.com/denuitt1/mhr-cfw) under `bin/mhr/`.

When a user saves MHR Settings, MehrN copies the selected `config.example.json` to `config.json` and writes `script_id`, `auth_key`, `http_port` or `listen_port`, and `socks5_port`.

Both upstream clients require Python 3.10+ and their `requirements.txt`. MHR-CFW also requires a deployed Cloudflare Worker and that worker URL configured in its deployed Google Apps Script `Code.gs`; it is not a local config field.

Both upstream projects are MIT licensed. Preserve their licenses and notices in redistributed builds.
