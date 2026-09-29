# LLMTranslation — Lightweight AI Selection Translator

<p align="center">
  English &nbsp;|&nbsp; <a href="README-zh.md"><b>简体中文</b></a>
</p>

This repository was entirely generated with AI coding tools (vibe coding; this document is excluded — after the AI drafted most of it, it was manually reviewed).

Models: Qwen 3.8 flash + Qwen 3.8 27B<br>Agents: Pi + QoderCLI

Inspired by Cherry Studio's selection-translation flow, but keeping only that single feature and pushing it to be as lightweight as possible (< 15 MB):

- **The executable is only ~750KB**
- No bundled runtime: built on the .NET Framework 4.8 that ships with Windows (preinstalled on Win10/11)
- Works with any **OpenAI-compatible API** (endpoint / port / key / model / system prompt all configurable)
- Math rendering: translation results are rendered in WebView2 with marked + KaTeX for Markdown and LaTeX formulas ($…$ inline, $$…$$ display, common symbols like \boldsymbol supported); all scripts and fonts are bundled locally — works fully offline; automatically falls back to plain text when WebView2 is unavailable
- **Cherry Studio–style translation window**: rounded corners; draggable title bar (always-on-top / minimize / close);
  toolbar: source→target language selection (Auto / 简体中文 / English), show/hide source text, copy (with "Copied" toast)
- Window interaction: **resize by dragging any edge or corner** via transparent hit zones, with the matching bidirectional arrow cursor on hover (minimum 320×240)
- **Streaming live rendering**: SSE chunks are pushed and displayed incrementally; KaTeX typesetting is debounced by 400ms so it doesn't disrupt the streaming feel
- Translation window shortcuts: **C to copy / R to re-translate / Esc to close**
- Sends `"thinking":{"type":"disabled"` by default to disable DeepSeek thinking (faster and cheaper); can be re-enabled in settings
- **Translated-text font size** adjustable in settings (10–28, default 15)

## Usage

1. Double-click `dist\LLMTranslation.exe` — a blue "译" (translate) icon appears in the system tray and stays resident.
2. On first launch the settings window opens automatically. Fill in:
   - **API endpoint**: default `https://api.deepseek.com/v1` (model `deepseek-v4-flash`); any OpenAI-compatible endpoint works, e.g. a local proxy `http://127.0.0.1:7890/v1`
   - **Port (optional)**: overrides the port in the endpoint (e.g. a local forwarding port)
   - **API key / model / system prompt**
3. **Select** (drag) or **double-click** text in any application → a floating `文A 翻译` capsule button appears next to the cursor (it auto-hides ~1 second after the mouse moves away).
4. Click the button → the translation window opens and streams (SSE) the translation in real time. Supports always-on-top / copy / edge-drag resize / minimize to taskbar / `Esc to close`.
5. Global hotkey (default **Alt+Q**, changeable in settings by clicking the field and pressing a key combo): skips the floating button and opens the translation window directly for the currently selected text.
6. Left-click or right-click the tray icon: toggle selection translation, open settings / usage guide, quit; double-click the tray icon to open settings directly.

Configuration is stored at `%LOCALAPPDATA%\LLMTranslation\config.json` (plain-text JSON, hand-editable).

## Directory layout

```
build.bat            build script (system csc, zero dependencies; bundles web/ and libs/ as embedded resources)
src\                 all source code (auto-split from a single Program.cs into modules, compiled together with csc)
  Program.cs         entry point: single-instance mutex + AppContext bootstrap
  AppContext.cs      main control: tray/hotkey wiring, global mouse hook, selection dispatch, resource extraction, log toggle
  TrayHotkey.cs      tray icon + global hotkeys (hidden message window)
  TranslateForm.cs   translation window: WebView2 rendering, streaming increments, edge/corner resize, DWM rounded corners
  SettingsForms.cs   full settings / usage-guide windows (WebDialogForm, WebView2)
  FloatingButtonForm.cs  selection capsule floating button (custom-drawn; includes RoundRect rounded-rect helper)
  Translator.cs      OpenAI-compatible API calls + SSE stream parsing
  Config.cs          config read/write (%LOCALAPPDATA%\LLMTranslation\config.json)
  Models.cs          data models (WebMsg / CfgDto and other frontend/backend messages)
  Dpi.cs             DPI awareness and scale factor (Dpi.Scale)
  NativeMethods.cs   Win32 P/Invoke + EdgePanel/EdgeBodyPanel/DWM rounded-corner helpers
  WinLog.cs          diagnostic logging (log.txt, debug mode only)
  TestTarget.cs      target window for automated tests (provides selectable text)
  app.manifest       DPI awareness manifest    app.ico  app/taskbar icon
web\                 frontend pages (packaged as embedded webassets at build time, extracted to %LOCALAPPDATA% on first run)
  index.html         translation window page: translated Markdown/formula rendering + toolbar + "quick settings" dialog
  settings.html      full settings / usage-guide page
  marked.min.js      Markdown parser (offline)
  katex.min.js/css   KaTeX formula rendering (offline, inline $...$ / display $$...$$)
  fonts\             KaTeX fonts (woff2)
libs\                WebView2 interop DLLs (build reference + embedded in rundeps.zip for single-file distribution)
dist\                final artifacts: LLMTranslation.exe (single file) + TestTarget.exe (testing only)
tools\makeicon.*     small tool to generate app.ico
test-e2e.ps1         e2e test: select/double-click → capsule → translation window
test-api.ps1         e2e test: local mock SSE server to verify streaming translation + formula rendering
test-ui.ps1          e2e test: translation window dragging + edge/corner resize (reads the configured hotkey)
mock-server.ps1      mock OpenAI SSE server for testing
AutoTest.cs/.exe     real-mouse selection automation verification (paired with autotest.manifest)
tmpcheck\            temporary dev verification scripts (not in version control)
```

## Rebuilding

```
build.bat
```

Only invokes `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` — zero dependencies.

## Text extraction (dual channel)

1. **UI Automation**: reads the TextPattern selection of the focused control (works in most apps — browsers, Office, Notepad, etc.; never touches the clipboard);
2. **Clipboard fallback**: when UIA can't read the selection, it simulates Ctrl+C to read it, then **immediately restores the original clipboard contents**.
   - For console windows (cmd/PowerShell/terminal, etc.) the Ctrl+C fallback is **not** used, to avoid accidentally triggering an interrupt signal.

## Capsule appearance spec (pinned, decoupled from system scaling)

The capsule window is fixed at 98×40 physical pixels and does not scale with DPI; fonts use fixed pixel sizes (`GraphicsUnit.Pixel`):
"A" 17.5px Bold, "翻译" 14.17px, "R" 18.33px; draw positions (8,4)/(18,17)/(42,9), corner radius 9.
It looks identical on any machine (100%/125%/150%). A/B pixel reference values (125% display): white capsule 96×38,
text bbox 70×24, 441 stroke pixels. After modifying the capsule drawing in `src/FloatingButtonForm.cs`, do a pixel-level A/B verification.

## Known limitations

- Target windows running **as administrator** are restricted by the Windows UIPI security mechanism and may not be readable (this app runs with normal permissions).
- For some old or fully custom-drawn controls that support neither UIA nor Ctrl+C, extraction is not possible (use Alt+Q or manual copy with a clipboard tool instead).
- Double-click selection detection relies on a self-contained mouse-event interval check of ≤500ms (matching the default system double-click speed).

## Why not just use existing software

| Software | Size | Status |
|---|---|---|
| Cherry Studio | ~100MB+ | Feature-complete but bloated |
| pot-desktop | 31.6MB installer | Exceeds the 15MB limit, and no longer maintained |
| STranslate | ~100MB | Exceeds the limit |
| **This tool** | **0.75MB** | Does exactly one thing: selection translation |

## Packaging & deployment (single file)

`build.bat` produces two artifacts in one build:

| Artifact | Size | Notes |
|---|---|---|
| `dist\LLMTranslation.exe` | ~741 KB | **Single-file edition**: copy it anywhere and run (desktop/USB stick, etc.). On first launch it automatically extracts the embedded web frontend and WebView2 dependencies to `%LOCALAPPDATA%\LLMTranslation\` |
| `dist\TestTarget.exe` | - | Testing only, not for distribution |

Uninstall: just delete the program directory (settings and the WebView2 cache in `%LOCALAPPDATA%\LLMTranslation` are kept and reused on reinstall).

Data locations (independent of the exe location, follow the user):

- Settings: `%LOCALAPPDATA%\LLMTranslation\config.json`
- Debug log (enabled by setting the `LLMTRANSLATION_DEBUG=1` environment variable): `%LOCALAPPDATA%\LLMTranslation\log.txt`
- WebView2 cache: `%LOCALAPPDATA%\LLMTranslation\WebView2\`

Note: only one instance can run at a time (single-instance lock).
