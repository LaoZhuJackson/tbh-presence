# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENTS.md

## Building and running

There is **no test suite** in this repo. Verification is a build plus a runtime check against a live TaskBarHero process, so state clearly in a handoff when a check could not be run (no game, no BepInEx interop assemblies).

```powershell
.\build.ps1                     # both editions -> TbhCompanion.exe, TbhCompanion-Presence.exe
.\build.ps1 -Version 3.01.05    # stamp a release version (writes src/Version.g.cs, deleted after)
dotnet build autosynth/TbhAutoSynth.csproj -c Release   # the BepInEx plugin
```

`build.ps1` uses the `csc.exe` bundled with .NET Framework 4.x — no SDK needed — and compiles `src/*.cs` **twice**, once per edition. It embeds the plugin DLL as a resource in the full edition, preferring `autosynth\bin\Release\TbhAutoSynth.dll` over the committed `autosynth\prebuilt\TbhAutoSynth.dll` by timestamp. CI cannot build the plugin (no game to generate interop assemblies), so it ships the prebuilt copy: **after editing `autosynth/Plugin.cs`, rebuild and refresh `autosynth/prebuilt/TbhAutoSynth.dll` before tagging a release.**

The plugin build needs a game folder with BepInEx already initialized; `GameDir` in `autosynth/TbhAutoSynth.csproj` is an absolute path that must point at the local install and may need editing.

Runtime checks (game running, `--once` is the fastest reader sanity check):

```
TbhCompanion.exe --once          # print one state reading as JSON and exit
TbhCompanion.exe --console       # tray-less run with live logging
  --interval <sec>  --client-id <id>  --no-cache
TbhCompanion.exe --shot <png>    # dev: render the settings window to a PNG and exit
```

Console modes `AttachConsole` on demand because the exe is linked `/target:winexe` (no console window in tray mode). The presence-only edition is selected by `/define:PRESENCE_ONLY` and gates behavior on `Build.Synth`, not on separate sources.

Legacy PowerShell prototype (mirrors the reader, handy for poking at the game without rebuilding): `.\legacy\Get-TbhStage.ps1 -Once` (Windows PowerShell 5.1+).

## Architecture

Three pieces built from one repo; the interesting coupling is in the contracts between them.

**1. Reader + presence exe (`src/`).** `Memory.cs` opens the `TaskBarHero` process with `PROCESS_VM_READ` **only — it must never write** — and locates IL2CPP classes by scanning for class-name strings then qwords pointing at them. `Game.cs` holds every struct offset and the `GameReader`, plus the on-disk address cache (`%LOCALAPPDATA%\tbh-companion\cache.txt`, keyed on `CACHE_VERSION`, validated by PID + process start time + class pointer). `PresenceEngine.cs` is the UI-agnostic poll loop; `Discord.cs` speaks the IPC named pipe with no libraries; `Tray.cs` / `StatusForm.cs` / `UiControls.cs` are the WinForms shell; `Program.cs` parses the CLI and picks a run mode.

Because everything resolves by class name, offsets are the only thing a game patch breaks. The live-stage class is an obfuscated short name the game re-randomizes each update, so it is found structurally (name string → static-field block `+0xB8` → slot `+0xA8` must point at a real `StageCache`), not by a fixed name.

**2. Auto-synthesis plugin (`autosynth/`).** A BepInEx plugin that runs *inside* the game and clicks the game's own UI. `Plugin.cs` is the cycle orchestrator (Soulstone → Chest → Offering → Alchemy → Synthesis → Rune, each optional) plus config and hotkeys; one `*Runner.cs` per phase; `GameInterop.cs` is the signature-based bridge to obfuscated members. It re-reads its config every ~10s, so edits apply without a game restart.

**3. Contracts that span files:**

- **Reader ↔ exe:** offsets in `src/Game.cs` must stay aligned with `legacy/Get-TbhStage.ps1` (`$OFF`), the offsets table in `CONTRIBUTING.md`, and `CACHE_VERSION` must be bumped so stale caches are discarded. The drift-check workflow opens an issue quoting exactly this checklist after a game patch.
- **Plugin ↔ exe:** the plugin writes live state to `%LOCALAPPDATA%\tbh-companion\autosynth-status.json`, which `StatusForm.cs` reads and displays. The exe deploys the embedded DLL into the game's `BepInEx\plugins` at startup (`AutoSynthDeploy.cs`); plugin changes only take effect after that redeploy and a game relaunch.
- **Version ↔ game version:** the tool's release version tracks the game's, offset by `20000` (`v3.00.28` ↔ game `1.00.28`). `.github/workflows/game-version-drift.yml` asserts this invariant three times a day against the Steam news feed and files a de-duplicated issue on drift; releasing the matching tag restores it. `SelfUpdate.cs` / `GameVersion.cs` use the same mapping to decide whether a matching release exists, reading the version from `AssemblyInformationalVersion` stamped by `build.ps1`.
- **UI text ↔ `Lang`:** all display strings go through `src/Lang.cs`, keyed by the English text itself (miss → English). Do not translate BepInEx cfg tokens, JSON keys, registry values, or file names — `StatusForm`'s `SynthesisTypes` / `Tiers` arrays are both cfg tokens and tile captions, and only the painted caption is translated. `GameState.Details`/`PartyLine`/`Label` stay English on purpose: Discord consumes them and `UpdateStatus` regex-matches them. `SelfUpdate.Status.Message` is rendered on demand so a language switch re-texts the version row without re-running the network check.

## Conventions and gotchas

- Do not add `AssemblyVersion` to `src/AssemblyInfo.cs` — `build.ps1` generates `src/Version.g.cs` and csc fails on the duplicate.
- Never drop `/codepage:65001` from `build.ps1`'s csc args. Sources are BOM-less UTF-8; without it csc decodes them as ANSI and the Chinese strings in `src/Lang.cs` become mojibake on a non-UTF-8 host.
- Obfuscated member names differ between Il2CppDumper output and BepInEx's Cpp2IL interop (e.g. dump `bsfb` → interop `bsfm`); verify names against `BepInEx\interop\Assembly-CSharp.dll`.
- Clicking a `ButtonBase` needs both `OnPointerClick(...)` and the wrapped `Button.onClick.Invoke()` — the former only plays hover/click effects.
- Phase runners that touch inventory use the game's own `SlotInteractionManager.MoveToCube` action rather than synthetic pointer events, which leave the player holding the item. Destructive phases (Alchemy, Soulstone) have `*DryRun` flags — prefer logging over acting when validating.
- The exe and the plugin are separate deploys: relaunching the game is required for plugin changes, not for exe changes.

See `CONTRIBUTING.md` for the full offset table, re-dumping procedure, `--once` JSON field reference, and BepInEx install details; `autosynth/README.md` for the plugin's config keys.
