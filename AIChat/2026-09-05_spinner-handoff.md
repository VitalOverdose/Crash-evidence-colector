# Handoff: SpinnerBox — a procedural WinForms spinner you can use in BSODGuru

**From:** the Claude session working in `F:\GithubMasterCopys\EliteBowserVDS`
**To:** the Codex session working in `F:\GithubMasterCopys\BSODGuru`
**Date:** 2026-09-05
**Author of the control:** David (art-directed) + Claude, in the EliteBowserVDS repo.

---

## 1. What it is

`SpinnerBox` is a fully procedural loading spinner: a `Control` subclass that draws
itself as geometry every frame. No image assets, no sprite sheets, no GIF. That means
it stays crisp at any size and any DPI, and it recolours from properties instead of
needing new artwork.

It ships **19 different looks**, selected by one enum property. They are not skins over
one animation — each is a separately drawn pattern.

The animation runs on a 16ms `System.Windows.Forms.Timer` (~60fps). The timer only runs
while the control is **visible AND `Spinning`** — a hidden or stopped spinner costs
nothing, so you can leave them parked in collapsed panes without paying for them.

## 2. IMPORTANT: it is not in the DLL you reference

This is the thing to get right before you plan anything.

BSODGuru references a vendored `libs\EliteBrowserShell.dll`. **`SpinnerBox` is not in
it**, and it is not in the EliteBrowserShell source repo either. It lives only in
EliteBowserVDS (`ProfessorSnowsVideoDownloader`), which BSODGuru does not reference.

So you cannot `using` your way to it. It has to be **copied as source**.

The good news: it is completely self-contained. The whole file's only dependencies are
`System`, `System.ComponentModel`, `System.Drawing`, `System.Drawing.Drawing2D` and
`System.Windows.Forms`. It touches no project type, no settings manager, no resources.
The single line tying it to its home repo is its namespace declaration.

## 3. How to bring it in

A verbatim copy of the source sits next to this file as **`SpinnerBox.reference.cs`**
(33KB, 684 lines). To use it:

1. Copy `SpinnerBox.reference.cs` into `CrashEvidenceCollector.App\` (or a `Controls\`
   subfolder if you prefer — it does not care), naming it `SpinnerBox.cs`.
2. Change the namespace. The file currently declares the old block-scoped form:

   ```csharp
   namespace ProfessorSnowsVideoDownloader.CustomControls
   {
       ...
   }
   ```

   BSODGuru uses **file-scoped namespaces** (see `UiTheme.cs`), so convert it to:

   ```csharp
   namespace CrashEvidenceCollector.App;
   ```

   and drop the wrapping braces + one level of indentation. Both the `SpinnerLook`
   enum and the `SpinnerBox` class live in that one namespace.
3. That is the entire port. It compiles on `net10.0-windows` unchanged — it is plain
   WinForms/GDI+ with nothing version-sensitive in it.

Do NOT try to refresh `libs\EliteBrowserShell.dll` to get this. The spinner is not in
that library's source; rebuilding the donor would not produce it, and the donor repo is
mid-edit anyway.

## 4. The API surface

Everything is designer-visible (`[Category]`/`[Description]` attributed), so it works
declared in a `.Designer.cs` as well as code-created.

| Member | Type | Default | What it does |
|---|---|---|---|
| `Look` | `SpinnerLook` | `Arc` | Which of the 20 patterns to draw. |
| `AccentColor` | `Color` | `#0078D7` | Main stroke — the colour that reads as "the spinner". |
| `SoftColor` | `Color` | `#C8D2DC` | Secondary — inner rings, faded tails, echo trails. |
| `Thickness` | `float` | `4f` | Stroke width in px at 96dpi. **Scales with control size**, so it stays proportional; you rarely need to touch it. |
| `Speed` | `double` | `1.0` | Pace multiplier. `1` is the tuned default. |
| `Spinning` | `bool` | `true` | `false` freezes the drawing AND stops the timer. |

Sizing: default is 64x64. It always draws into the **largest centred square** that fits
the client area, so a non-square control just letterboxes rather than distorting. Below
8px it draws nothing.

`BackColor` is `Color.Transparent` and it sets `SupportsTransparentBackColor`, so it
picks up whatever pane it sits on — it will sit on `UiTheme.Surface` or `UiTheme.Canvas`
without a visible box around it.

## 5. The 19 looks

`Arc`, `DualRing`, `Dots`, `Segments`, `TripleArc`, `SquareChase`, `OrbitEcho`,
`PolygonPulse`, `EqualizerRing`, `InfinityChase`, `FlowingDash`, `ConcentricRipples`,
`PieWipe`, `Pendulum`, `NewtonCradle`, `MorphCycle`, `DnaHelix`, `IrisShutter`,
`TripleOrbit`.

Short descriptions are on each enum member in the source. A few that suit an
evidence-collection tool: `Arc` (the Material/Chrome classic, safest default),
`Segments` (flat ticks with a chasing tail — reads as "working through a list"),
`EqualizerRing` (radial bars, reads as "sampling"), `TripleOrbit` (David's own
commission, the showiest — three nested orbit trails).

## 6. Suggested usage in BSODGuru

```csharp
var spinner = new SpinnerBox
{
    Look        = SpinnerLook.Segments,
    AccentColor = UiTheme.Accent,
    SoftColor   = UiTheme.Border,
    Size        = new Size(48, 48),
    Spinning    = false,      // start parked
};
```

Then flip `Spinning` around the long operations — dump parsing, evidence collection,
the elevated helper round-trip — rather than creating and disposing spinners.

Two rules worth honouring, both from the donor codebase:

- **Do not fight the timer.** Toggle `Spinning` or `Visible`; the control starts and
  stops its own timer from those. Never poke it on a background thread — it is a plain
  WinForms control and `Invalidate` must come from the UI thread.
- **Do not animate a spinner the user cannot see.** It is cheap, but a spinner running
  behind a collapsed pane is 60 wasted invalidations a second for nothing.

## 7. Optional: the gallery

EliteBowserVDS also has `frmSpinnerGallery` — a dev-only form that renders every look
side by side, generated from the enum, click a tile to zoom it solo. It is 85 lines and
code-created on purpose (test tooling, never seen by users). I have NOT copied it here
because it is a judging room, not a feature. If David wants to pick a look by eye rather
than from the list above, ask and I will send it over too.

## 8. Provenance / questions

The control is at `F:\GithubMasterCopys\EliteBowserVDS\CustomControls\SpinnerBox.cs`,
last touched by commit `8565634` ("TripleOrbit: double the rotation speed (art
director's note)"). If the copy here drifts from that, the EliteBowserVDS one is the
original — but there is no sync mechanism, so once you copy it into BSODGuru it is
yours to change.

Anything unclear, leave a reply file in this `AIChat\` folder and David will carry it
back.
