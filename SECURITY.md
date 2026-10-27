# Security

Report a vulnerability privately with **Report a vulnerability** on the [Security tab](https://github.com/hazeliscoding/shybun/security), not in a public issue. Fixes go into the latest release.

Shybun has no accounts or servers. The tray app runs on your PC as you, without administrator rights. It mutes your mics through Windows and never opens an audio stream. The Shybun camera is a media source that Windows loads into its camera service and into apps that open the camera. It passes frames through in memory and swaps in the camera-off image while you're muted. It writes no files and opens no network connections.

Administrator rights are used once: "Install the Shybun camera..." in the tray menu starts Shybun again through a UAC prompt, and that process copies the media source to `%ProgramFiles%\Shybun` and registers it for the machine, and nothing else. A signed Shybun only installs a media source signed by the same publisher. "Remove the Shybun camera..." and uninstalling Shybun undo it the same way.

What it keeps, all on your PC:

- `%LOCALAPPDATA%\Shybun\mute-log.csv`, the time of each mute and unmute and the state read back from each device, never sound or pictures
- Mute, under `HKCU\Software\Shybun\Camera`, where the Shybun camera reads it, and the update setting under `HKCU\Software\Shybun`
- the media source in `%ProgramFiles%\Shybun` and its registration, once you install the Shybun camera

Its only network call is the update check against this repository's GitHub Releases, made by the tray app, which **Update automatically** in the tray menu turns off. There is no telemetry.

Reports we especially want:

- anything that makes the badge say muted while a mic or the camera is live
- the camera storing or sending a frame
- the elevated step doing more than install or remove the media source, or registering a file that isn't Shybun's
- any network call besides the update check

Releases are code-signed, and each one lists SHA-256 checksums in `SHA256SUMS`.
