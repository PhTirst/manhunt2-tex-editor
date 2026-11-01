# Manhunt 2 TEX Editor

English | [简体中文](README.zh-CN.md)

A Windows editor for **Manhunt 2 PC `.tex` files only**, using WinForms and a native C++ core. No Qt. Manhunt 1 TXD and other games' TEX formats are not supported.

`0.1.0-rc.1` is a release candidate. Synthetic tests exercise structure and codecs; they do not certify every level or mod in-game.

## Run

Extract the entire `manhunt2-tex-editor-*-windows-x64.zip` and launch `Mh2TexEditor.exe`. Windows 10/11 x64; no separate .NET or Visual C++ Runtime installation is required. Keep `mh2core.dll` and all other DLLs with the executable.

Use any writable tools folder. The application does not need to be installed in the game and does not deploy changes automatically. No game assets, fonts or translated textures are included. Work on copies and keep original backups.

## Workflow

1. Open a copy of a TEX file.
2. Filter and select a texture from the left list, which retains the original record order.
3. Export PNG, edit externally without changing dimensions, and replace it.
4. Compare original/current previews and RGBA, alpha or individual channels. Inspect each mip level.
5. Save As to a new TEX, reopen and inspect it before manual deployment.

Supports batch export/replacement, undo/redo, per-texture revert, checker/dark/light backgrounds, wheel zoom and drag panning. Preview refreshes after background work; closing is blocked while an operation is in progress.

## Duplicate Names

Some original TEX files contain multiple physical records with the same name. They are displayed as `name [1/2]` and `name [2/2]`, not merged.

- Export names are `name__1.png`, `name__2.png`.
- Batch replacement prioritizes numbered filenames. A shared `name.png` can replace every same-name record.
- Save applies replacements by physical record offset, preserving unrelated same-name entries.

## Deliberate Limits

- Existing records can be replaced; adding, deleting or renaming records is not supported.
- PNG dimensions must match the original.
- Original DDS encoding, mip count, TEX slot capacity and record order are preserved. Oversized replacements are rejected.
- No automatic game deployment or backup service is provided. Save As uses a temporary file and a replacement operation, but always retain your own original copies.
- The current GUI is Simplified Chinese. Texture names are left unchanged. Bilingual documentation does not imply runtime UI-language switching.

Shortcuts: Ctrl+O open, Ctrl+Shift+S save as, Ctrl+E export PNG, Ctrl+R replace PNG, Ctrl+Z/Y undo/redo, Ctrl+0/1 fit/actual size.

## Build and Test

Requires Windows x64, .NET 10 SDK, CMake 3.24+, Visual Studio 2022 C++ Build Tools, a Windows SDK and clang-cl. Scripts use the ClangCL platform toolset from VS, not a hardcoded compiler path.

The first build downloads fixed upstream DirectXTex and zlib-ng archives, verifies SHA-256 hashes, and caches them only under `out/native/_deps`. The source release does not include build caches or require the old workspace.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

Tests generate all fixtures themselves: raw/Z2HM TEX, BGRA/BC3 DDS, mipmaps, PNG replacement, history, duplicate isolation, and save/reopen. No game files are needed.

`out/release/` contains self-contained Windows x64 and resource-free source ZIPs with SHA-256 sidecars. The runtime contains a per-file manifest. Packaging audits archives and validates the extracted native dependency via a headless self-test.

`src/Mh2TexEditor` is the GUI; `src/native` is the native format/codec layer and C ABI; `tests`, `scripts`, and `cmake` contain regression, reproducible commands and pinned dependencies. GitHub Actions configuration is provided for Windows Debug/Release builds; it runs only after upload.

See [release checklist](docs/RELEASING.md), [third-party notices](NOTICE.md), and [MIT license](LICENSE).
