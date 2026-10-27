# AGENTS.md

These are the working rules for agents in this repo. Shybun is a Windows tray app (.NET 10, Avalonia 12, NativeAOT, Apache-2.0) that mutes your mic and camera with one key, in every app, and shows you that you're muted.

## Sources of truth

- `README.md`: the pitch and the trust contract.
- `ROADMAP.md`: decisions already made, the milestones, and what is out of scope. Check it before proposing features. Respect those decisions unless the owner reopens them.
- Work from the next unchecked item in `ROADMAP.md`. Don't build past the current milestone without asking.

## The trust contract (hard rules)

Shybun sits between people and their mic and camera. They run it only if they can trust it, so never break these, not even in dev tooling that ships.

- **No code in other processes, except the Shybun camera.** No DLL injection, no `CreateRemoteThread`, no hook DLLs. Work laptops ban injection. The Shybun camera's media source is the one exception, and Windows loads it, not Shybun: into the Frame Server, the camera monitor and every app that opens the camera. So it passes frames through and swaps in the camera-off image, and does nothing else: it writes no files and opens no network. A bug there breaks the camera for every app that loads it.
- **No network call except the update check.** Velopack's check of GitHub Releases, made by the tray app, is the only one. No `HttpClient`, sockets or telemetry in Shybun's own code, no other package that brings network code in, and nothing network in the Shybun camera. A test will fail on any `System.Net` use in Shybun's assemblies. A tool between you and your camera that talks to the network can't be trusted, so its one call fetches its own updates, and a tick in the tray menu turns that off.
- **Never store or send a frame or a sound.** The mic half only flips Windows' mute switch and never opens an audio stream, except in explicit tests that read silence. The camera passes frames through in memory. No snapshots, no debug dumps of frames, no log line with pixel or sample data. A mute tool that records is spyware.
- **Run unelevated.** Don't set `requireAdministrator` in the manifest. The one exception is the camera's one-time install step: a separate elevated process that copies the media source and registers it, and nothing more. The mic half must work with no admin at all, because work laptops often grant none.
- **The badge never claims a mute it can't confirm.** It shows what Windows reports, read back after each change. A device Shybun can't read shows as unconfirmed, never as muted. A false "muted" is worse than no badge: you'd talk freely on a live mic.
- **Never move focus.** The key goes through `RegisterHotKey`, and the badge never activates. Pressing mute mid-sentence must not pull you out of the app you're in.
- **Never restart into an update mid-call.** The key does nothing while Shybun restarts, so a downloaded update waits until no app is using a mic or a camera.
- **Don't change Windows' camera or mic privacy settings** (`CapabilityAccessManager\ConsentStore`) without the owner's OK, even in a test, and restore them afterwards. A test that forgets leaves the camera blocked for every app.
- **Code ported from VCamNetSample keeps its MIT notice** in `THIRD-PARTY-NOTICES.md`. The license requires it.

## Code

- .NET 10 with nullable on and warnings as errors. Package versions live in `Directory.Packages.props`.
- `Shybun.Core` holds the mute state and its rules. It never references Avalonia or any Win32 interop; a test will enforce it. Rules take device events and the time as inputs, so tests run without real devices.
- Win32 and COM calls go through CsWin32 (`NativeMethods.txt`), not hand-written `DllImport`. The generated code is typed and NativeAOT-safe.
- NativeAOT must stay free of warnings: no reflection-based serialization. Use source generators.
- `CameraContract`'s values never change. A Shybun camera installed by an older release keeps running after the tray app updates, until the owner clicks "Update the Shybun camera...", and it reads those values. A test pins them.

## Brand

- The logo is the bunny mark, and the app icon is the mark on a paper tile (`app-icon.svg`). It began as a placeholder, and the owner kept it as the logo, so don't redraw it here.
- Shiori, in `docs/brand/shiori/`, is the mascot. Never edit, recolour or redraw her here: her images come from one written spec and one reference image in the owner's anime-pipeline, and an image changed by hand drifts from the rest of the set. New stickers are drawn there.
- The wordmark is outlined Fredoka SemiBold. Use the lockup SVGs; don't re-typeset it with a web font. `scripts/make_lockup.py` rebuilds them from the mark, so rerun it when the mark changes.

## Dependencies

- **A special-purpose library** (a parser, an installer framework): add it yourself, with its reason in the commit message.
- **A general-purpose helper:** only after checking that nothing in the project already does the job.
- **A framework:** never without asking the owner.
- **Anything that brings in network code:** never, except Velopack for the update check. See the trust contract.

## Glossary

One meaning per term. Use these words in code, docs and commits.

- **Mute:** Shybun's one on/off state. On means every mic is muted in Windows and the Shybun camera sends the camera-off image.
- **Mic:** any audio capture device Windows lists, not only the default one.
- **Badge:** the small always-on-top window that shows the mic's and the camera's state as Windows reports it.
- **Unconfirmed:** the badge's state for a device Shybun can't read back, or a camera an app holds directly. Never shown as muted.
- **Shybun camera:** the virtual camera people pick in their meeting apps. It wraps the real camera.
- **Real camera:** the physical webcam.
- **Camera-off image:** what the Shybun camera sends while muted.

## Commands

- `dotnet build` builds everything. `dotnet test` runs the tests.
- `dotnet test tests/Shybun.Tests -- --filter-class "Shybun.Tests.LiveTests" --explicit only` runs the live checks on real mics and the real keyboard. Ask the owner first, every time: nobody on a call, and nobody typing until it finishes, Orca's prompt box included. Set `SHYBUN_EXE` to a published `Shybun.exe` to check the native build.
- `dotnet test tests/Shybun.Tests -- --filter-class "Shybun.Tests.CameraLiveTests" --explicit only` runs the live checks on the real camera, with the same ask first. One loads the media source in-process and needs no install; the others go through the Frame Server and need the Shybun camera installed. The install-step check needs `SHYBUN_EXE` and two UAC prompts, which the owner approves. `dotnet test` doesn't print the checks' readings; run `tests/Shybun.Tests/bin/Release/net10.0-windows/Shybun.Tests.exe -method <name> -explicit only -diagnostics` to see them.
- `dotnet publish src/Shybun.Desktop -c Release -r win-x64` builds the native exe, with the camera's native DLL beside it. With Visual Studio 18, put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on `PATH` first: its `VsDevCmd.bat` calls `vswhere.exe` without a path, and the error text breaks the NativeAOT link step ("'vswhere.exe' is not recognized").
- A pushed `v*` tag releases: `.github/workflows/release.yml` tests, publishes, packs with Velopack (`vpk`, pinned in `dotnet-tools.json`), signs through Azure Artifact Signing in the `release` environment, which only `v*` tags can deploy to, and drafts a GitHub Release. Run it by hand to build the same files unsigned.
- An installed copy checks GitHub Releases a minute after it starts. To try an update locally, `vpk pack` two versions into one folder, install the older one with a Setup packed on its own (each pack overwrites the folder's Setup), and start the installed `Shybun.exe` with `SHYBUN_UPDATE_SOURCE` set to that folder. It restarts into the newer one once no app is using a mic or a camera.
- In a published `Shybun.exe`, "Install the Shybun camera..." in the tray menu installs the media source beside it, through one UAC prompt, and "Remove the Shybun camera..." undoes it. Neither stops the Frame Server, which keeps a media source it has loaded until it restarts. After installing a new build, restart Windows, or the Frame Server service elevated; that ends any camera stream, so ask the owner first.
- `dotnet run scripts/make-icons.cs` regenerates `docs/brand/shybun.ico` and `app-icon-256.png` from `app-icon.svg`.
- `uv run --no-project --with fonttools --with uharfbuzz python -I scripts/make_lockup.py <Fredoka-SemiBold.ttf> docs/brand` rebuilds the lockups. Its header says where to get the font.

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `ci:`, `build:`, `refactor:`). Keep each commit atomic, and use a scope when it adds clarity (`feat(mic): …`).
- **No AI attribution** in commits or PRs. That means no `Co-Authored-By` trailers, no "Generated with" lines and no session links.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. Give every check that tests for an absence a positive control. For example, the network guard must fail on a deliberate `HttpClient`.
- **Structural rules get tests.** When a decision is a rule about the code's shape, add a test that fails when it's broken.
- **Real devices:** explicit tests that open the mic or the camera run only when the owner isn't on a call, and never save what they read.
- **Validation:** evidence comes from dogfooding and public signals (issues, downloads). Don't plan interviews, recruiting or outreach.
- **Docs:** short and concise. Prefer editing `ROADMAP.md` over creating new planning documents.
- **Code comments:** explain why, not what. Comment only what the code can't say for itself: a non-obvious constraint, a Win32 or Media Foundation quirk and its cause, or a line that keeps the trust contract. Don't restate names or types, and don't leave commented-out code.
- Local Playwright output goes to `.playwright-mcp/`, which git ignores.
