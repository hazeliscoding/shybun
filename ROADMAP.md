# Roadmap

Shybun is a Windows tray app that mutes your mic and camera with one key, in every app, and shows you that you're muted. This file tracks the decisions already made, what gets built in what order, and what's out of scope.

## Architecture characteristics

What the design must protect. Everything else gets ordinary good design. These are Claude's proposal, recorded for the owner to change.

1. **Truth.** The badge never says muted while a device is live. It shows what Windows reports, read back after every change, and when Shybun can't confirm a device is quiet, it says so.
2. **One key, from anywhere.** The key works whichever app has focus, and pressing it never moves focus or brings a window forward.
3. **Fits a locked-down work laptop.** No driver, no injection, and it runs unelevated. Admin at most once, at install, and the mic half needs none.

## Decisions (2026-10-24)

The owner's:

- **Build Shybun, free and open source.** Claude recommended parking the idea: demand is small (88 people across 14 PowerToys issues since December 2024, only 17 of them about the camera) and mostly about the mic. The owner mutes mic and camera all day at work, so they're the first user. This gives up that time for ideas with more demand, and a paid layer.
- **The name is Shybun:** a shy bunny hides its face and goes quiet, as the camera and the mic do. Nothing called Shybun turned up on GitHub, winget or the Microsoft Store, and `shybun.com` is registered but serves nothing. Trademarks aren't searched yet. This gives up the runner-up, Hushkin.
- **Linux is Not planned.** A Linux virtual camera means v4l2loopback, a kernel module, which is the kind of driver Shybun avoids on Windows. GNOME and KDE already have a mic-mute shortcut, and Wayland limits global keys and always-on-top windows. Reopen it if issues ask for it, or once PipeWire cameras work in the main browsers and meeting apps. This gives up Linux users. *(The Linux facts weren't checked this session.)*
- **Planned without the usual questions,** at the owner's call. Claude stated the calls below; the owner can reopen any of them. This gives up a design the owner chose point by point.
- **Shiori Burrows is the mascot, beside the placeholder mark.** She is a shy homebody who would rather stay unseen and unheard, drawn in the owner's anime-pipeline from the app's line: hiding her face is the camera, and going quiet is the mic. She appears in the README, in `docs/brand/shiori/` and in launch posts, and M2 plans the camera-off image from her hidden-and-quiet sticker. The bunny mark stays the logo and the app icon, and the real logo is still an owner item. Her art was drawn with an AI image model, and it is Apache-2.0 like the code; the README says both. Her dusty rose (`#C08182`) stays apart from the mark's blush until the real logo is picked, rather than retinting her or moving the brand. This gives up a single mark, a matched pink for now, and exclusive rights to her art: anyone may reuse or change it. *(The real logo is settled: the bunny mark stays, below.)*
- **Shybun updates itself, as Sweepling does (2026-10-25).** Velopack checks GitHub Releases a minute after start and every 4 hours, downloads in the background, and restarts into the update. An "Update automatically" tick in the tray menu turns it off, and a build from source never checks. The check is the only network call: the tray app makes it, the Shybun camera never does, and offline it fails quietly. *Claude's call within it:* a downloaded update waits until no app is using a mic or a camera, because the key does nothing while Shybun restarts. This gives up "no network at all", about 6 MB of exe (Velopack's HTTP stack grew Sweepling's from 19.9 to 25.6 MB), and updates for the Shybun camera without admin: it sits in `%ProgramFiles%\Shybun`, so M3 settles how a release that changes it gets installed.
- **No portable copy, as in Sweepling (2026-10-25).** Velopack's per-user Setup is the only download. A copy that can't update itself would miss fixes. This gives up a zip for people who won't run an installer.
- **The mute key is Win+Shift+Q (2026-10-25).** It was PowerToys Video Conference Mute's default for "both", so people who kept 0.87.1 already know it, and it's in neither Microsoft's Windows shortcut list nor Teams' (Teams uses Ctrl+Shift+M and Ctrl+Shift+O). If another app holds it, the tray menu says so. This gives up picking your own key, for now (Later).
- **A mic unmuted behind Shybun's back shows live (2026-10-25).** The badge and the tray say a mic is live, and Shybun never mutes it again on its own; the next press mutes everything. A laptop's mic key or Windows' Sound settings may unmute on purpose, and fighting them would trap you. This gives up protection from an accidental unmute.
- **The badge shows while muted, and whenever a mic is live during Mute or can't be read (2026-10-25).** It hides when everything is simply live, so no badge means live. If Shybun dies, the badge goes too, and you assume you're live, which is the safe guess; the tray icon tells the two apart. It sits in the primary screen's top right, as PowerToys' toolbar did by default. This gives up a badge that also confirms you're live.
- **The bunny mark is the logo (2026-10-26).** The placeholder stays, with its lockups and the app icon, so there's no design project for a new one. Shiori's dusty rose stays apart from the mark's blush. The trademark search moves to Later. This gives up a logo drawn for the brand, perhaps from Shiori's bunny clip, and a pink that matches hers.
- **The camera half covers laptops with and without admin (2026-10-26).** The owner can approve an admin prompt at work, and work uses Teams, so M2 builds the Shybun camera. Without admin, the badge shows when an app has the real camera on and says Shybun can't turn it off, and the key leaves the camera alone. Teams' local API is dropped: Elgato pulled its Teams plugin in November 2025 over "Microsoft's decision to discontinue the API", a plugin author reports it was retired on 30 June 2026, and it's a socket, which the trust contract forbids. This gives up a camera mute without admin.

Claude's calls:

- **Public, under Apache-2.0,** like Staypaw. A tool that sits between you and your camera is easier to trust when people can read it.
- **.NET 10, Avalonia 12 and NativeAOT,** laid out like Staypaw: `Shybun.Core` (the mute state and its rules, with no Win32), `Shybun.Desktop` (the tray app and all the Windows code) and `Shybun.Tests`. Win32 and COM calls go through CsWin32. Because Core calls no Win32, its rules test without devices. This gives up a smaller exe on a runtime Windows already ships.
- **The mic is muted in Windows,** with `IAudioEndpointVolume::SetMute` on every capture device, as PowerToys' Video Conference Mute did. It works in every app with no admin, where Win+Alt+K reaches only apps that support it. This gives up the meeting app's own mute icon: Teams can show you unmuted while Windows has muted the mic, so the badge is the truth.
- **Every mic, including one that appears while you're muted.** Otherwise a headset plugged in mid-call is live. PowerToys also offered every mic, as its default. This gives up leaving a mic you never use alone.
- **The camera is a Windows 11 virtual camera** (`MFCreateVirtualCamera`) that wraps the real camera and sends a camera-off image while muted. It needs no driver: PowerToys removed its driver-based camera in v0.88.0 over install trouble and webcams it broke. This gives up:
  - Windows 10;
  - an app that opens the real camera directly, which Shybun can't mute (M2 tries to warn about it);
  - picking "Shybun camera" once in each app;
  - admin once at install. Windows loads the camera's media source in the Frame Server, the camera monitor and every app that opens it, so it must be registered for the whole machine (HKLM) and sit in a folder those services can read, never under the user's profile ([VCamNetSample](https://github.com/smourier/VCamNetSample)). A bug in it breaks the camera for every app that loads it.
- **Not Windows' camera privacy switch.** In a test on 2026-10-24 (Windows 11 25H2, the owner's built-in webcam), setting "Let desktop apps access your camera" to Deny didn't stop a running stream: Media Foundation kept 15 fps at normal brightness for 8 s, and DirectShow lost no frames. A stream opened while it's Deny starts with no error and gets no frames. Windows checks the setting only when a stream starts, so it can't be a mute. This gives up the one camera mute that needs no install.
- **If the work laptop allows no admin,** the camera half falls back to Teams' local API for third-party devices. It's unverified, and on 2026-10-24 MuteDeck's docs said its Teams support "works without the old third-party API", so the API may be on its way out. M2 checks it before anything is built on it. This gives up a camera mute for apps other than Teams on that route. *(Superseded on 2026-10-26: without admin, the badge shows the camera honestly instead, above.)*
- **No network code, so no in-app updater.** Updates come through winget and GitHub Releases, as in Staypaw. *(Superseded on 2026-10-25: Shybun updates itself, above.)*
- **Velopack Setup and a portable zip, signed through the Artifact Signing setup Staypaw uses.** *(The portable zip is superseded on 2026-10-25: no portable copy, above.)* Setup installs per user and never asks for admin; the camera's one-time admin step is Shybun's own, run when you turn the camera on. This gives up a single install that does everything.
- **The brand is a placeholder:** a shy bunny with its eyes closed and its cheeks blushing, in `docs/brand/`. The wordmark is Fredoka SemiBold, as in Staypaw and Sweepling. The real logo and the mascot are M0 owner items. *(Superseded: Shiori is the mascot, and the placeholder mark is the logo, both above.)*
- **The key toggles what the badge shows (2026-10-25).** When it shows Muted, a press unmutes every mic; otherwise, a mic live during Mute or one Shybun can't read included, a press mutes every mic. A press always goes toward quiet first. This gives up a plain on/off toggle that ignores what Windows reports.
- **Shybun never changes a mic at start or at quit (2026-10-25).** It starts from what Windows reports, so it starts muted only if every mic already is, and quitting leaves every mic as it was. This gives up unmuting for you when Shybun closes.
- **CsWin32 runs as a build task (2026-10-25),** so its COM interfaces use .NET's COM source generators, which NativeAOT supports, with runtime marshaling turned off. Windows' device and mute callbacks are `[GeneratedComClass]` classes; one reached the native build in the live test. This gives up nothing found yet; Staypaw and Sweepling use CsWin32's older, marshaled mode.
- **Silence means below one step of 16-bit audio, 1/32768 (2026-10-25).** A muted mic measured 2e-10, float residue from Windows' audio processing. The live test asks for sound from at least one live mic, not every one: the owner's Logitech PRO X Wireless headset read live but sent pure silence, because it was switched off or muted on the headset. This gives up proving sound on every mic.
- **The tray's state icons are the app icon with a dot (2026-10-25):** blush while muted, ink when a mic is live during Mute or can't be read. `scripts/make-icons.cs` draws them, so the mark itself is unchanged.
- **The Shybun camera wraps the real camera as Microsoft's sample does (2026-10-26).** The virtual camera names the real camera with `AddDeviceSourceInfo`, and the Frame Server hands it to the media source. `MediaSource` and `MediaStream` are ported from Microsoft's `HWMediaSource` (MIT), and the COM hosting follows VCamNetSample (MIT). All interop goes through CsWin32, so VCamNetSample's DirectN isn't a dependency. This gives up DirectN's ready-made helpers.
- **Mute reaches the camera through the registry (2026-10-26).** The tray app writes `Mute` to `HKCU\Software\Shybun\Camera` and lets Local Service, which the Frame Server runs as, read it; the virtual camera tells the media source whose key that is. The media source reads it for every frame and writes nothing. This gives up hearing back from the camera: the badge item settles how the camera confirms, since a write-back would widen the camera's "does nothing else".
- **The camera fails closed (2026-10-26).** When Mute can't be read, the camera-off image goes out, and a format it can't draw sends nothing rather than the real frame. This gives up a working camera when the tray app's key is missing or broken.
- **The camera-off image stands Shiori on the frame's bottom edge (2026-10-26),** at 90% of its height, centred on heather grey `#BCB8B9`, like someone sitting at a webcam. It's drawn in the stream's own format: NV12, YUY2, RGB32 or MJPG. This gives up centring her vertically.

## M0: Placeholder (as soon as possible)

- [x] Add `LICENSE` (Apache-2.0), `README.md`, `ROADMAP.md` and `AGENTS.md`.
- [x] Add the placeholder mark and its lockups to `docs/brand/`.
- [x] The bunny mascot: Shiori Burrows, from anime-pipeline, in `docs/brand/shiori/` and the README.
- [x] The real logo: the placeholder bunny mark stays. *(owner, see Decisions)*
- [x] Scaffold `Shybun.Core`, `Shybun.Desktop` and `Shybun.Tests` on .NET 10 and Avalonia 12, with central package versions and warnings as errors.
- [x] A tray icon made from the mark, with Quit. No window yet. Load Avalonia's Fluent theme, or the tray menu opens empty (Staypaw's fix).
- [x] Design tokens: a spacing scale, a type scale, a palette with shades and a few shadow levels.
- [x] NativeAOT publish for `win-x64` with no trim or AOT warnings.
- [x] Guardrail tests, each with a positive control: `Shybun.Core` references neither Avalonia nor any Win32 interop (P/Invoke or COM), and no Shybun assembly uses `System.Net`. A deliberate `HttpClient` and a deliberate `DllImport` in Core failed them.
- [x] CI on Windows: build, test, and secret scanning with the gitleaks release binary, checked against its published checksum and run with `--log-opts="--full-history HEAD"`.

**Done when:** CI is green on `main`, and `dotnet publish -r win-x64` gives a native exe that starts in the tray.

**Done 2026-10-26.** The build items were done on 2026-10-24; on 2026-10-26 the owner kept the bunny mark as the logo and moved the trademark search to Later. [CI run 40157132474](https://github.com/hazeliscoding/shybun/actions/runs/40157132474) passed on `main`: build, tests, format, NativeAOT publish with warnings as errors, and the secret scan. A throwaway pull request that added an `HttpClient` to Core failed `No_Shybun_assembly_uses_the_network` in CI ([run 40157298279](https://github.com/hazeliscoding/shybun/actions/runs/40157298279)) and was closed without merging. The native exe is 17,563,136 bytes, and it started in the tray with no window, using a 39.6 MB working set after 5 s.

## M1: Mic and badge

- [x] A global key through `RegisterHotKey`, which never moves focus. Pick a default that clashes with neither Windows (Win+Alt+K) nor Teams. *(Win+Shift+Q, see Decisions.)*
- [x] Mute and unmute every capture device through `IAudioEndpointVolume::SetMute`, with no admin.
- [x] Mute a mic that appears while muted, as it arrives.
- [x] Read each mic's state back after every change and on Windows' own notifications. The badge and the tray show what was read, never what Shybun asked for.
- [x] Decide what happens when something unmutes a mic behind Shybun's back: mute it again, or show it live. *(Show it live, see Decisions.)*
- [x] The badge: always on top, never takes focus, and clicks pass through it.
- [x] The tray icon shows the state too.
- [x] Log every mute and unmute with the state read back from each mic, to `%LOCALAPPDATA%\Shybun\mute-log.csv`, for the dogfooding count. Times and states only, never sound.

**Done when:** tests on `Shybun.Core` cover a mic that appears while muted and a mic unmuted behind Shybun's back; an explicit test opens a capture stream on each mic and gets only silence while muted, and sound otherwise; and pressing the key from another app leaves that app in front.

**Done 2026-10-25.** `MuteTests` cover a mic that appears while muted and one unmuted behind Shybun's back. On the owner's laptop, with two mics (the built-in Realtek one and a Logitech PRO X Wireless headset), the explicit `LiveTests` passed against the native exe:
- Win+Shift+Q, pressed with `SendInput`, muted both mics and left the app in front where it was, and pressed again it unmuted both.
- While muted, a second of each mic peaked at 2e-10 at most. Unmuted, the built-in mic gave sound; the headset was switched off or muted on the headset itself.
- Unmuting one mic behind Shybun's back reached Shybun as Windows' notification. The mic stayed live, and the log gained a `windows,on,2,1,1,0` row.
- The badge showed "Muted" in the top right and hid on unmute.

Not yet seen live: a mic plugged in or switched on while muted (Core-tested only), a click passing through the badge, and the tray icons in the real tray. The native exe is 17,804,800 bytes, up from 17,563,136.

## M2: Camera

- [x] Settle the route: can the owner's work laptop install as admin once, and which meeting apps does work use? *(owner: yes, and Teams. Build the Shybun camera, and show the camera honestly without admin; see Decisions.)*
- [ ] `Shybun.Camera`: a NativeAOT media source that passes the real camera's frames through and sends the camera-off image while muted. Start from VCamNetSample (MIT), keeping its notice in `THIRD-PARTY-NOTICES.md`.
- [ ] Hand the mute state to the media source across processes: it runs in the Frame Server and the apps that open it, never in Shybun.
- [ ] Give the Shybun camera the real camera while it streams (`IMFVirtualCamera::AddDeviceSourceInfo`).
- [ ] A one-time elevated step copies the media source to `%ProgramFiles%\Shybun` and registers it. The tray app stays unelevated, and uninstalling undoes the step.
- [ ] Decide whether to release the real camera while muted, so its light goes off. eitchtee/VCM does, and documents webcams that fail to reopen afterwards.
- [ ] Warn when an app holds the real camera instead of the Shybun camera. Check first whether the per-app `LastUsedTimeStart` and `LastUsedTimeStop` values under `ConsentStore\webcam` show that.
- [ ] Without the Shybun camera installed, the badge shows when an app has the real camera on and says Shybun can't turn it off. The key leaves the camera alone.
- [ ] The badge shows the camera.
- [ ] The camera-off image: Shiori's hidden-and-quiet sticker, waist-up and centred on a flat heather-grey 16:9 frame, composed in Shybun from `docs/brand/shiori/hidden-and-quiet.png`.

**Done when:** a frame probe reading the Shybun camera gets real frames unmuted and only camera-off frames muted; a probe that opens the real camera directly makes the badge show the camera as unconfirmed, or, without the Shybun camera installed, say Shybun can't turn it off; and Windows' Camera app, Teams, Discord and Google Meet in a browser each list the Shybun camera and show the camera-off image while muted.

**Built 2026-10-26, not yet run on the webcam.** `Shybun.Camera` wraps the real camera, reads Mute and sends the camera-off image while it's on. Unit tests cover the image and the Mute switch, and CI publishes the DLL with no AOT warnings. `CameraLiveTests` waits for the owner to approve the install's UAC prompt. The tray app doesn't create the Shybun camera or write Mute yet.

## M3: 1.0

- [ ] A Velopack per-user Setup, built by a release workflow.
- [ ] Shybun updates itself through Velopack, as Sweepling does: the first check a minute after start, then every 4 hours, with an "Update automatically" tick in the tray menu and no check in a build from source. A downloaded update waits until no app is using a mic or a camera; check first that Windows' `ConsentStore` shows that (see M2).
- [ ] Decide how a release that changes the Shybun camera reaches `%ProgramFiles%\Shybun`, which needs admin.
- [ ] Sign releases with Artifact Signing through OIDC, from a `release` environment that only `v*` tags can deploy to, as in Staypaw.
- [ ] Scan the published exe and the media source with Microsoft Defender.
- [ ] Submit a winget manifest.
- [ ] A screenshot and a short demo in the README.

**Done when:** `winget install hazeliscoding.Shybun` installs a signed build that starts in the tray, turning the camera on installs the Shybun camera, and an installed release updates itself to a later test release with no Shybun process left behind.

## Later

- Separate keys for the mic and the camera, and push-to-talk. PowerToys' Video Conference Mute had both.
- A camera-off image you pick.
- Mute when the screen locks.
- Pick your own mute key, for when another app holds Win+Shift+Q.
- Search trademarks for Shybun: USPTO. *(owner, moved from M0 on 2026-10-26)*
- Shiori's gone-quiet (mic) and face-hidden (camera) stickers were drawn in anime-pipeline on 2026-10-25. Their 512px exports are `docs/brand/shiori/gone-quiet.png` and `face-hidden.png`, not yet used in the README or the app. gone-quiet covers her mouth with both hands, and face-hidden covers her eyes with one eye peeking. They fit separate keys for the mic and the camera. face-hidden's peeking hand has three fingers, which can't be seen at sticker size; anime-pipeline's notes give a masked fix for it. Her walked-in sticker is specced there but not drawn, and needs a new budget.
- Shiori as the badge. She covers her mouth while the mic is muted and hides her face while the camera is off: a mute state worth showing in a ten-second clip, the way ThatPet shows an agent's. Her gone-quiet and face-hidden stickers above are the poses. Truth still rules: each pose shows what Windows reports, and unconfirmed must look nothing like muted. There's no reaction to talking while muted, since Shybun never opens an audio stream. Extra characters to buy would reopen the decision to build Shybun free.

## Not planned

- **Linux.** See Decisions.
- **macOS.** Both halves are built on Windows APIs. MuteDeck covers macOS.
- **Any kernel or camera driver,** DirectShow or AVStream. Work laptops block them, and PowerToys removed its own over install trouble and broken webcams.
- **Pressing meeting apps' own buttons for you,** as MuteDeck does. It breaks whenever an app moves a button, and MuteDeck already does it well.
- **Network code besides the update check:** telemetry or crash reporting.
- **Recording or previewing sound or video.** See the trust contract.

## How we'll know it works

- The owner's dogfooding at work: the mute log counts mutes where the badge and a device disagreed. It must stay at zero.
- Public signals after release: issues, downloads and winget installs. No interviews or outreach.
