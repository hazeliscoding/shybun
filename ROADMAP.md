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
- **Shiori Burrows is the mascot, beside the placeholder mark.** She is a shy homebody who would rather stay unseen and unheard, drawn in the owner's anime-pipeline from the app's line: hiding her face is the camera, and going quiet is the mic. She appears in the README, in `docs/brand/shiori/` and in launch posts, and M2 plans the camera-off image from her hidden-and-quiet sticker. The bunny mark stays the logo and the app icon, and the real logo is still an owner item. Her art was drawn with an AI image model, and it is Apache-2.0 like the code; the README says both. Her dusty rose (`#C08182`) stays apart from the mark's blush until the real logo is picked, rather than retinting her or moving the brand. This gives up a single mark, a matched pink for now, and exclusive rights to her art: anyone may reuse or change it.
- **Shybun updates itself, as Sweepling does (2026-10-25).** Velopack checks GitHub Releases a minute after start and every 4 hours, downloads in the background, and restarts into the update. An "Update automatically" tick in the tray menu turns it off, and a build from source never checks. The check is the only network call: the tray app makes it, the Shybun camera never does, and offline it fails quietly. *Claude's call within it:* a downloaded update waits until no app is using a mic or a camera, because the key does nothing while Shybun restarts. This gives up "no network at all", about 6 MB of exe (Velopack's HTTP stack grew Sweepling's from 19.9 to 25.6 MB), and updates for the Shybun camera without admin: it sits in `%ProgramFiles%\Shybun`, so M3 settles how a release that changes it gets installed.

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
- **If the work laptop allows no admin,** the camera half falls back to Teams' local API for third-party devices. It's unverified, and on 2026-10-24 MuteDeck's docs said its Teams support "works without the old third-party API", so the API may be on its way out. M2 checks it before anything is built on it. This gives up a camera mute for apps other than Teams on that route.
- **No network code, so no in-app updater.** Updates come through winget and GitHub Releases, as in Staypaw. *(Superseded on 2026-10-25: Shybun updates itself, above.)*
- **Velopack Setup and a portable zip, signed through the Artifact Signing setup Staypaw uses.** Setup installs per user and never asks for admin; the camera's one-time admin step is Shybun's own, run when you turn the camera on. This gives up a single install that does everything.
- **The brand is a placeholder:** a shy bunny with its eyes closed and its cheeks blushing, in `docs/brand/`. The wordmark is Fredoka SemiBold, as in Staypaw and Sweepling. The real logo and the mascot are M0 owner items. *(The mascot half is superseded: Shiori is the mascot, above.)*

## M0: Placeholder (as soon as possible)

- [x] Add `LICENSE` (Apache-2.0), `README.md`, `ROADMAP.md` and `AGENTS.md`.
- [x] Add the placeholder mark and its lockups to `docs/brand/`.
- [ ] Search trademarks for Shybun: USPTO. *(owner)*
- [x] The bunny mascot: Shiori Burrows, from anime-pipeline, in `docs/brand/shiori/` and the README.
- [ ] Pick the real logo in a claude.ai design project, perhaps drawn from Shiori's bunny clip, and export `mark.svg` with a `-dark` variant. `scripts/make_lockup.py` rebuilds the lockups from it. *(owner)*
- [x] Scaffold `Shybun.Core`, `Shybun.Desktop` and `Shybun.Tests` on .NET 10 and Avalonia 12, with central package versions and warnings as errors.
- [x] A tray icon made from the mark, with Quit. No window yet. Load Avalonia's Fluent theme, or the tray menu opens empty (Staypaw's fix).
- [x] Design tokens: a spacing scale, a type scale, a palette with shades and a few shadow levels.
- [x] NativeAOT publish for `win-x64` with no trim or AOT warnings.
- [x] Guardrail tests, each with a positive control: `Shybun.Core` references neither Avalonia nor any Win32 interop (P/Invoke or COM), and no Shybun assembly uses `System.Net`. A deliberate `HttpClient` and a deliberate `DllImport` in Core failed them.
- [x] CI on Windows: build, test, and secret scanning with the gitleaks release binary, checked against its published checksum and run with `--log-opts="--full-history HEAD"`.

**Done when:** CI is green on `main`, and `dotnet publish -r win-x64` gives a native exe that starts in the tray.

**Build items done 2026-10-24; the two owner items are still open.** [CI run 40157132474](https://github.com/hazeliscoding/shybun/actions/runs/40157132474) passed on `main`: build, tests, format, NativeAOT publish with warnings as errors, and the secret scan. A throwaway pull request that added an `HttpClient` to Core failed `No_Shybun_assembly_uses_the_network` in CI ([run 40157298279](https://github.com/hazeliscoding/shybun/actions/runs/40157298279)) and was closed without merging. The native exe is 17,563,136 bytes, and it started in the tray with no window, using a 39.6 MB working set after 5 s.

## M1: Mic and badge

- [ ] A global key through `RegisterHotKey`, which never moves focus. Pick a default that clashes with neither Windows (Win+Alt+K) nor Teams.
- [ ] Mute and unmute every capture device through `IAudioEndpointVolume::SetMute`, with no admin.
- [ ] Mute a mic that appears while muted, as it arrives.
- [ ] Read each mic's state back after every change and on Windows' own notifications. The badge and the tray show what was read, never what Shybun asked for.
- [ ] Decide what happens when something unmutes a mic behind Shybun's back: mute it again, or show it live.
- [ ] The badge: always on top, never takes focus, and clicks pass through it.
- [ ] The tray icon shows the state too.
- [ ] Log every mute and unmute with the state read back from each mic, to `%LOCALAPPDATA%\Shybun\mute-log.csv`, for the dogfooding count. Times and states only, never sound.

**Done when:** tests on `Shybun.Core` cover a mic that appears while muted and a mic unmuted behind Shybun's back; an explicit test opens a capture stream on each mic and gets only silence while muted, and sound otherwise; and pressing the key from another app leaves that app in front.

## M2: Camera

- [ ] Settle the route: can the owner's work laptop install as admin once, and which meeting apps does work use? *(owner)* With admin, build the virtual camera. Without it, first check that Teams' local API still exists.
- [ ] `Shybun.Camera`: a NativeAOT media source that passes the real camera's frames through and sends the camera-off image while muted. Start from VCamNetSample (MIT), keeping its notice in `THIRD-PARTY-NOTICES.md`.
- [ ] Hand the mute state to the media source across processes: it runs in the Frame Server and the apps that open it, never in Shybun.
- [ ] Give the Shybun camera the real camera while it streams (`IMFVirtualCamera::AddDeviceSourceInfo`).
- [ ] A one-time elevated step copies the media source to `%ProgramFiles%\Shybun` and registers it. The tray app stays unelevated, and uninstalling undoes the step.
- [ ] Decide whether to release the real camera while muted, so its light goes off. eitchtee/VCM does, and documents webcams that fail to reopen afterwards.
- [ ] Warn when an app holds the real camera instead of the Shybun camera. Check first whether the per-app `LastUsedTimeStart` and `LastUsedTimeStop` values under `ConsentStore\webcam` show that.
- [ ] The badge shows the camera.
- [ ] The camera-off image: Shiori's hidden-and-quiet sticker, waist-up and centred on a flat heather-grey 16:9 frame, composed in Shybun from `docs/brand/shiori/hidden-and-quiet.png`.

**Done when:** a frame probe reading the Shybun camera gets real frames unmuted and only camera-off frames muted; a probe that opens the real camera directly makes the badge show the camera as unconfirmed; and Windows' Camera app shows the camera-off image while muted.

## M3: 1.0

- [ ] A Velopack per-user Setup and a portable zip, built by a release workflow.
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
- Shiori's gone-quiet (mic), face-hidden (camera) and walked-in stickers, drawn in anime-pipeline with a new budget. Their presets are ready in her spec there. gone-quiet and face-hidden fit separate keys for the mic and the camera.

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
