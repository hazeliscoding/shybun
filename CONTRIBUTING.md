# Contributing to Shybun

Thanks for helping. Shybun mutes your mic and camera with one key, in every Windows app, and shows you that you're muted. It sits between people and their mic and camera, so it stays small on purpose, and this page is short.

## Where to start

- [README.md](README.md) says what Shybun does and what it promises. [ROADMAP.md](ROADMAP.md) holds the decisions already made, the milestones, and what's planned for later or not at all. Check it before proposing a feature: decisions stand unless the owner reopens them.
- [AGENTS.md](AGENTS.md) lists the rules every change keeps, each with its reason, and the words the code and docs use: mute, mic, badge, unconfirmed, Shybun camera and real camera mean one thing each.
- The most useful issue is a badge that disagreed with a device: what it said, what the mic or camera really did, and which meeting app was open.
- For anything bigger than a fix, open an issue first, so nobody builds something that's already decided against.

## Build, run and test

You need Windows 10 or 11 on x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` asks for 10.0.300 or a later feature band. The Shybun camera needs Windows 11.

```powershell
dotnet build Shybun.slnx
dotnet test --solution Shybun.slnx
dotnet format Shybun.slnx --verify-no-changes
```

The ordinary tests touch no device. A few write a scratch folder or a scratch key under `HKCU`, and delete them.

**The live tests use your real mics, camera and keyboard.** They're explicit, so `dotnet test` skips them. Run them only when nobody is on a call, and keep off the keyboard while they run: they press the mute key and mute every mic. AGENTS.md has the commands.

To try the native build, publish it. The publish is NativeAOT and needs Visual Studio's C++ workload. If linking fails with "'vswhere.exe' is not recognized", put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on `PATH`.

```powershell
dotnet publish src/Shybun.Desktop -c Release -r win-x64 -o artifacts/publish
```

`artifacts\publish\Shybun.exe` then runs in the tray, with the Shybun camera's media source beside it, and "Install the Shybun camera..." in its tray menu installs that through one UAC prompt.

## Project layout

| Folder | What it holds |
|---|---|
| `src/Shybun.Core` | The mute state and its rules, and what the tray app and the camera share. No UI and no Windows calls. |
| `src/Shybun.Desktop` | The tray app: the key, the mics, the badge, the camera's install step and updates. Win32 and COM through CsWin32. |
| `src/Shybun.Camera` | The Shybun camera: a NativeAOT media source that Windows loads, ported in part from MIT samples (see THIRD-PARTY-NOTICES.md). |
| `tests/Shybun.Tests` | xUnit v3 tests, including the explicit live tests. |
| `docs/brand` | The mark, the lockups, the icons and Shiori's art. |
| `scripts/` | The icon and lockup generators. |

Package versions live in `Directory.Packages.props`.

## Hard rules

These come from the trust contract in [AGENTS.md](AGENTS.md), which gives the reason for each, and reviews hold every change to them.

- No code in other processes, except the Shybun camera, which Windows loads. It passes frames through and swaps in the camera-off image, and does nothing else.
- No network call except Velopack's update check. A test fails on any `System.Net` use in Shybun's assemblies.
- Never store or send a frame or a sound, not even in a debug log.
- The tray app runs unelevated. Only the camera's install step runs elevated, and it copies and registers the media source and nothing more.
- The badge never claims a mute it can't confirm. A device Shybun can't read shows as unconfirmed, never as muted.
- Never move focus. The key goes through `RegisterHotKey`, and the badge never activates.
- Never restart into an update while an app is using a mic or a camera.
- `CameraContract`'s values never change: a Shybun camera installed by an older release reads them.

## Code style

Match the code around you. `.editorconfig` sets the style, `.gitattributes` keeps LF line endings, and CI runs `dotnet format --verify-no-changes`. Comments explain why, not what.

Shiori's art comes from one written spec in the owner's art pipeline; don't edit or redraw her here.

## Commits and pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/), with a scope when it helps, such as `fix(camera): …`. Keep each commit to one logical change.
- Commit messages read like a person wrote them: no `Co-Authored-By` trailers or "Generated with" lines for AI tools.
- Work on a branch and open a pull request into `main`. [CI](.github/workflows/ci.yml) builds, tests, checks formatting, publishes with NativeAOT and scans for secrets, and it has to pass.
- Bugs and requests go in [GitHub Issues](https://github.com/hazeliscoding/shybun/issues). Security problems don't: see [SECURITY.md](SECURITY.md).

## How releases are cut

The owner cuts releases. A `v*` tag starts [the release workflow](.github/workflows/release.yml): it tests, publishes, signs the app, the media source and Setup, and opens a draft GitHub Release with the update packages and `SHA256SUMS`, which the owner checks and publishes. Installed copies pick the release up through their update check.
