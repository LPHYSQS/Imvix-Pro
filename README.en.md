<p align="center">
  <img src="Assets/logo.png" alt="Imvix Pro logo" width="128" />
</p>

<h1 align="center">Imvix Pro</h1>

<p align="center">
  A professional desktop conversion tool for batch workflows, local AI assistance, mixed document/image inputs, and Windows integration.
</p>

<p align="center">
  <a href="README.md">简体中文</a> | English
</p>

<p align="center">
  <a href="https://get.microsoft.com/installer/download/9n3ztwz2f3z9?referrer=appbadge" target="_self">
    <img src="https://get.microsoft.com/images/zh-cn%20dark.svg" width="200" alt="Get Imvix Pro from Microsoft Store" />
  </a>
</p>

<p align="center">
  .NET 10 | Avalonia 11 | MVVM | Local AI Runtime | PDF / PSD / GIF / OCR / QR / Barcode
</p>

> Imvix Pro is no longer just a simple “change the file extension” image converter. The current Pro build is closer to a desktop workstation for images, documents, icon assets, and automation workflows, with batch conversion, PDF and GIF expansion, PSD preview, offline AI features, intelligent preview tools, folder watch automation, and Windows integration.

## Overview

Imvix Pro is a Windows-first desktop conversion tool, and the current repository version is `1.3.3`.  
It combines format conversion, batch compression, resizing, intelligent analysis, PDF/PSD handling, offline AI tools, recent history, failure logs, and folder watch workflows in one desktop application.

Compared with the older standard-edition README, the current Pro build has clearly expanded into:

- mixed image, vector, PDF, PSD, EXE icon, and desktop shortcut icon intake
- first-class `PDF` output with page, range, and split strategies
- local offline AI batch enhancement plus preview-window AI enhancement, AI matting, and AI erasing
- OCR, QR recognition, barcode recognition, file-detail inspection, and Windows integration features
- reusable workflows through presets and saved folder-watch profiles

## Highlights

| Area | What the current Pro build provides |
| --- | --- |
| Mixed intake | Multi-file import, folder import, drag-and-drop, with support for PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, TIF, ICO, SVG, PDF, PSD, EXE, and LNK |
| Conversion output | PNG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, and PDF output with source-folder or custom-folder routing |
| Batch rules | Compression quality, resize strategies, rename rules, overwrite control, transparency handling, and ICO/SVG background settings |
| PDF / GIF workflows | PDF current-page, all-pages, page-range, and split-single-page export; GIF first-frame, specific-frame, and all-frame export |
| AI batch enhancement | Local offline AI image enhancement that runs before the regular conversion pipeline continues |
| Intelligent preview tools | Preview-window AI enhancement compare, AI matting, AI erasing, OCR text recognition, QR scanning, and barcode scanning |
| Workflow tools | Presets, pause/resume/cancel, recent history, failure logs, auto-open output folder, and saved folder-watch profiles |
| Windows integration | System tray support, run on startup, desktop shortcuts, and Windows “Open with Imvix Pro” context menu integration |
| UX and localization | 10 UI languages, light/dark/system theme modes, window placement restore, and PDF lock / unlock flow |

## Supported Scope

| Type | Scope |
| --- | --- |
| Batch import / open | PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, TIF, ICO, SVG, PDF, PSD, EXE, LNK |
| Batch output | PNG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF |
| AI batch enhancement input | PNG, JPG, JPEG, WEBP, BMP, static TIFF, and single-frame GIF |
| Preview AI matting / erasing | Supported static raster images |
| Preview OCR / QR / barcode | Previewable image content, including rendered previews of PDF and PSD sources |

Notes:

- When a batch contains `PDF`, `PSD`, `SVG`, animated `GIF`, or other inputs that are not suitable for AI batch enhancement, those files skip AI enhancement and continue through the standard conversion path.
- `PDF` input can be exported as images or exported back out as `PDF`, with all-page, current-page, page-range, and split-single-page strategies.
- `GIF` input supports first-frame, specific-frame, and all-frame strategies. When the output format is not `GIF`, the app can expand frames into separate outputs.
- `EXE` and `LNK` inputs are primarily used as icon-extraction sources.

## AI and Intelligent Tools

### 1. AI batch image enhancement

- Fully local and offline
- Based on Real-ESRGAN and Upscayl model families
- Supports requested upscale targets from `2x` to `16x`
- Supports `Auto` and `Force CPU` execution modes
- Enhanced results continue through the existing compression, resize, output, and naming rules
- The UI explicitly marks third-party models that carry extra non-commercial notices

### 2. Preview-window AI tools

- `AI enhancement preview`: generate an enhanced preview for the current item, compare original/result/split view, and save the result
- `AI matting`: local ONNX background removal with `U2Net`, `ISNet`, `MODNet`, `AnimeSeg`, transparent output, or solid-color background output
- `AI eraser`: local `LaMa`-based erase-and-repair tool with brush size, mask expansion, and edge blend controls

Note: `AI matting` and `AI eraser` are currently preview tools only. They do not participate in batch conversion or folder-watch tasks.

### 3. Recognition and analysis

- `OCR text recognition`: offline Paddle OCR v5 runtime
- `QR scanning`: detect QR content and extract links
- `Barcode scanning`: detect common 1D and 2D barcodes
- `Content analysis`: format suggestions, transparency-risk warnings, compression-risk warnings, and output size estimation

## File and Document Handling

- `PDF`
  - first-page preview, page navigation, and page-range selection
  - export to images or export to new PDF files
  - password unlock flow and locked-file skip behavior
- `PSD`
  - import and rendered composite preview
  - PSD canvas, layer, channel, and color detail inspection
- `EXE / LNK`
  - extract application icons or shortcut icons as conversion sources
- `File detail viewer`
  - inspect image, PDF, PSD, EXE, and LNK metadata and derived information

## Workflow and Integration

- Presets: save, apply, overwrite, and delete conversion presets
- History: record recent conversions, trigger source, duration, estimated size, and output summary
- Logging: generate failure logs only when a task contains failed items
- Folder watch: save the current rules as a watch profile and process new files automatically
- System tray: keep the app available after closing the main window
- Run on startup: use a Windows startup shortcut
- Explorer context menu: show “Open with Imvix Pro” for supported image, PDF, PSD, EXE, and LNK files

## Processing Flow

```mermaid
flowchart LR
    A["Import files / folders / drag and drop"] --> B["Analyze content, size, and risk"]
    B --> C["Optional AI batch enhancement"]
    C --> D["Apply format, page/frame handling, resize, compression, rename, and output rules"]
    D --> E["Write output files"]
    E --> F["Store history, failure logs, and watch status"]
```

## Current Architecture

```text
Imvix Pro/
|-- Assets/Localization/         # 10 language resources
|-- RuntimeAssets/AI/            # AI enhancement, matting, erasing models and runtimes
|-- RuntimeAssets/Ocr/           # OCR runtime assets
|-- RuntimeAssets/Qr/            # QR runtime configuration
|-- RuntimeAssets/Barcode/       # barcode runtime configuration
|-- Services/AI/                 # AI enhancement, matting, and erasing services
|-- Services/ImageConversion/    # core conversion, encoding, saving, and format handling
|-- Services/PdfModule/          # PDF import, render, security, and export
|-- Services/PsdModule/          # PSD import, render, and detail analysis
|-- ViewModels/Main/             # main window state, AI, PDF, preview, and watch logic
|-- Views/                       # main UI, preview window, detail window, dialogs, and summaries
`-- Imvix Pro.csproj             # main desktop project
```

## Build and Run

### Requirements

- Windows is the primary validated and published target for this repository
- `.NET 10 SDK`
- A desktop environment supported by Avalonia

### Run locally

```bash
dotnet restore
dotnet build "Imvix Pro.csproj"
dotnet run --project "Imvix Pro.csproj"
```

### Publish a Windows single-file build

```bash
dotnet publish "Imvix Pro.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Additional notes:

- The UI uses Avalonia, but the published target and integration layer are clearly Windows-focused in the current repository.
- OCR, EXE/LNK icon handling, run-on-startup, Explorer context menus, some codec paths, and some AI acceleration paths are Windows-dependent.

## Configuration and Data

On Windows, Imvix Pro stores app data under `%AppData%\Imvix Pro`.

| File or folder | Purpose |
| --- | --- |
| `settings.json` | Language, theme, default output rules, presets, watch configuration, preview-tool settings, and window state |
| `history.json` | Recent conversion history |
| `Logs/conversion-*.log` | Batch failure logs |

If an older `%AppData%\Imvix` folder exists, the application attempts to migrate it to `%AppData%\Imvix Pro`.

## Localization

Built-in UI resources are available for:

- `zh-CN`
- `zh-TW`
- `en-US`
- `ja-JP`
- `ko-KR`
- `fr-FR`
- `de-DE`
- `it-IT`
- `ru-RU`
- `ar-SA`

`ar-SA` uses right-to-left layout support.

## Tech Stack

- `.NET 10`
- `Avalonia UI 11`
- `CommunityToolkit.Mvvm`
- `SkiaSharp`
- `Docnet.Core`
- `Magick.NET`
- `Microsoft.ML.OnnxRuntime` / `DirectML`
- `RapidOCR.Net`
- `ZXing.Net`

## License and Commercial Use

This repository ships with a custom source-available license in [`LICENSE`](LICENSE).

- The author / copyright holder retains the right to use, license, sell, distribute, and operate Imvix Pro commercially
- Any other individual or organization must contact the author and obtain prior written permission before using the project in a commercial product, paid service, revenue-generating workflow, internal business operation, or any other commercial scenario
- Current commercial licensing contact email: `3261296352@qq.com`
- Because commercial use is restricted, this project is `source-available`, not an OSI-approved open source project
- Bundled third-party runtimes, models, or assets may have their own license terms, and you must still comply with them
