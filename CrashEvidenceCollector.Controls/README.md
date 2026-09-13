# CrashEvidenceCollector.Controls

CEC's own copy of the custom WinForms controls it uses: the vStack / adaptive-row
DPI layout system, the rounded inputs, the tab header and content controls, and the
report viewer panel.

## Where it came from

Copied from `F:\GithubMasterCopys\EliteBrowserShell` at commit `bad6179`
("Controls refresh for CEC"). The shell itself was only read, never changed.

Only the files CEC needs came across — the controls it references, plus everything
those controls genuinely use in code. Browser, bookmark, history, download and
SafeCopy code stayed in the shell.

## What differs from the shell

- **`CustomControls/ControlSettings.cs` is new.** Five layout controls and
  `ModernButton` read app-wide settings from the browser app's `SettingsManager`,
  which dragged the whole browser settings stack into any consumer. They now read
  `ControlSettings` instead, with the shell's defaults:
  - `StepDownHighDpiFonts` — `true`
  - `AppIconOnlyMode` — `false`, plus its `AppIconOnlyModeChanged` event
- Nothing else was edited in the move. Theming changes made after the move are
  CEC's own and are not expected to flow back to the shell.

## Conventions kept

- Namespace is still `ProfessorSnowsVideoDownloader.*`, so existing designer files
  resolve unchanged. Renaming is a separate, later step.
- Targets `net8.0-windows`, as the shell does, so the controls compile exactly as
  they did there.
- The layout engine's rules still apply: `AutoScaleMode.None`, fonts come from
  designer values or vStack rules, and rule↔control pairing is by `Tag` ↔ rule `Id`.
