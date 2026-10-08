# UI/UX Specification - DocumentMDConverter

## 1. Visual Direction & Aesthetics
DocumentMDConverter provides a clean, responsive, high-end developer and enterprise tool experience:
* **Theming:** Dual Theme System (Dark Mode First + Clean Light Mode) with instantaneous CSS variable toggling and local storage persistence.
* **Color Palette (Dark Theme):**
  - App Background: Deep Obsidian (`#0b0f19`).
  - Elevated Surfaces: Translucent slate/zinc (`#131c2e` / `#1e293b`) with 1px border highlights (`rgba(255, 255, 255, 0.08)`).
  - Primary Accent: Indigo-violet to electric cyan (`#6366f1` / `#818cf8`).
  - State Indicators: Emerald Green (`#22c55e`) for success, Amber (`#f59e0b`) for OCR / warnings, Red (`#ef4444`) for errors.
* **Color Palette (Light Theme):**
  - App Background: Soft porcelain (`#f8fafc`).
  - Elevated Surfaces: Pure white (`#ffffff`) with subtle drop shadow and borders (`#e2e8f0`).
  - Text: Deep Charcoal (`#0f172a`).
* **Typography:**
  - Interface: `Inter`, system sans-serif.
  - Code & Raw Markdown: Modern monospace font family.

---

## 2. Global Layout & Header

```text
+-----------------------------------------------------------------------------------------+
| [Logo] DocumentMDConverter           [ user@company.com • ]  [ ☀️/🌙 Theme ] [ Docs ]    |
+-----------------------------------------------------------------------------------------+
|                                                                                         |
|                    ( [Convert] )   ( [Recent Conversions (7d)] )   ( [Markdown Preview] )       |
|                                                                                         |
+-----------------------------------------------------------------------------------------+
```

* **Header Controls:**
  - **Authenticated User Badge:** Displays the current Google IAP authenticated identity with an emerald green pulse indicator.
  - **Theme Toggle:** Sun/Moon SVG button swapping CSS variables instantaneously.
* **Pill Navigation Tabs:**
  - **Convert Tab:** Ingestion dropzone and immediate conversion output.
  - **Recent Conversions Tab:** 7-day retention history with serverless GCS persistence.
  - **Markdown Preview Tab:** standalone live editor and renderer (third tab).

---

## 3. Screen Layouts

### 3.1 Tab 1: Converter Screen
```text
+-----------------------------------------------------------------------------------------+
|                                                                                         |
|    +-------------------------------------------------------------------------------+    |
|    |                                                                               |    |
|    |        [ Animated Cloud Upload Icon ]                                         |    |
|    |        Drag & drop your files here or click to browse                         |    |
|    |        Formats: docx pptx xlsx csv pdf png jpg (30MB, 20 files max)         |    |
|    |                                                                               |    |
|    +-------------------------------------------------------------------------------+    |
|                                                                                         |
|  +---------------------------------------+  +----------------------------------------+  |
|  | File Metadata                         |  | Markdown Viewer                        |  |
|  | - File: Annual_Report.pdf             |  | [ Preview ] [ Split ] [ Raw ]          |  |
|  | - Engine: anydoc ⚡ (0.04s)            |  |----------------------------------------|  |
|  | - Words: 3,420 • Tokens: ~4,500       |  | # Executive Summary                    |  |
|  |                                       |  |                                        |  |
|  | [ Copy Markdown ]  [ Download .md ]   |  | Table of Contents...                   |  |
|  +---------------------------------------+  +----------------------------------------+  |
+-----------------------------------------------------------------------------------------+
```

### 3.2 Tab 2: Recent Conversions (Default Widescreen Grid)
When no document is open for inspection, history cards arrange in a **responsive 2-column grid** on widescreen monitors (`minmax(540px, 1fr)`) to optimize horizontal space:

```text
+-----------------------------------------------------------------------------------------+
|  Recent Conversions (7-Day Cloud Storage Retention)                         [ Refresh ] |
|  [!] Files and metadata are retained in Google Cloud Storage for 7 days.                |
|                                                                                         |
|  +------------------------------------+    +------------------------------------+       |
|  | [DOCX] Contract_v2.docx • 2h ago   |    | [PDF] Financial_Q3.pdf • 5h ago    |       |
|  | Expires in 6 days                  |    | Expires in 6 days                  |       |
|  |------------------------------------|    |------------------------------------|       |
|  | Input: 1.2 MB [Download Original]  |    | Input: 4.8 MB [Download Original]  |       |
|  | Output: anydoc • 2.4k words        |    | Output: Cloud Vision • 5.1k words  |       |
|  | [Open in Viewer] [Copy] [Download] |    | [Open in Viewer] [Copy] [Download] |       |
|  +------------------------------------+    +------------------------------------+       |
+-----------------------------------------------------------------------------------------+
```

### 3.3 Tab 2: Recent Conversions (Widescreen Split View $\ge$ 992px)
When the user clicks **"Open in Viewer"**, the view smoothly transforms into a **Widescreen Split View**:

```text
+-----------------------------------------------------------------------------------------+
|  Recent Conversions                                                         [ Refresh ] |
|                                                                                         |
|  Sidebar (380px, Sticky & Scrollable)       Markdown Viewer (1fr)                       |
|  +------------------------------------+    +-----------------------------------------+  |
|  | [DOCX] Contract_v2.docx [Viewing]  |    | Contract_v2.docx    [Preview][Split][Raw] |  |
|  +------------------------------------+    | 2.4k words • 3.1k tokens   [X Close]    |  |
|  | [PDF] Financial_Q3.pdf  [Open]     |    |-----------------------------------------|  |
|  +------------------------------------+    | # Non-Disclosure Agreement              |  |
|  | [XLSX] Budget2026.xlsx  [Open]     |    |                                         |  |
|  +------------------------------------+    | Section 1: Confidentiality...           |  |
|  | [PPTX] PitchDeck.pptx   [Open]     |    |                                         |  |
|  +------------------------------------+    +-----------------------------------------+  |
+-----------------------------------------------------------------------------------------+
```
* **Seamless Document Switching:** Clicking any other document card in the sidebar immediately loads it in the viewer.
* **Close Button:** The `[X Close]` button dismisses the viewer and returns to the 2-column grid.
* **No Tab Switching:** The user remains within the Recent Conversions tab at all times.

### 3.4 Tab 2: Recent Conversions (Mobile Slide-Over Drawer $<$ 992px)
On mobile and tablet viewports, clicking "Open in Viewer" slides a panel over the screen from the right with a backdrop blur overlay (`min(850px, 94vw)`), preserving mobile readability.

---

## 4. Key Interactive Components

### 4.1 `DocumentDropzone.razor`
* Animated SVG illustration.
* File size validation (30 MB per file) and batch limit (20 files); `.epub` rejected.
* Global drag & drop overlay, file picker and clipboard paste of images/files (`converterUtils.initGlobalDropzone`).
* Batch mode: sequential processing, per-item status (Pending / Processing / Completed / Failed), per-item view and download, ZIP download.
* SignalR real-time conversion progress tracker.

### 4.2 `MarkdownViewer.razor`
* Triple-mode segmented view: **Preview** (rich HTML), **Split** (rendered + raw side-by-side with a keyboard-accessible resizer), **Raw** (syntax code).
* Word and LLM token counter (`~3.8 characters per token`).
* Copy to clipboard with instant visual confirmation.
* Direct `.md` file download.
* Optional contextual `Close` button for drawer and split layouts.

### 4.3 Markdown Preview tab
* 99vw Markdown editor and live rendered preview side-by-side, with a resizable divider, synchronized line numbers, and near-viewport pane height.
* Mermaid diagrams render client-side with strict security and high-contrast colors for both app themes; raw HTML is disabled in pasted Markdown previews.
* The preview HTML is written from JavaScript (`converterUtils.setHtml`) so Blazor never diffs nodes mutated by Mermaid (avoids "unexpected error, reload" crashes while typing).
* In dark mode, sequence-diagram message and loop text is forced to a light color (`#e2e8f0`); node and actor labels stay dark because Mermaid fills them with light colors.
* On narrow screens, the editor and preview stack vertically.

### 4.4 `HistoryItemCard.razor`
* Modular card component rendering document format badges (`DOCX`, `PPTX`, `XLSX`, `PDF`).
* Direct V4 Signed URL download buttons for both original files and converted `.md` files.
* Relative time formatting (`"Just now"`, `"5m ago"`, `"2h ago"`).
* Dynamic "Viewing Now" active state.

---

## 5. Accessibility & Performance
* **WCAG 2.1 AA:** Minimum 4.5:1 text contrast ratios across both Dark and Light themes.
* **Direct Cloud Storage Downloads:** High-speed downloads without server memory streaming.
* **Zero Layout Shift:** Responsive CSS grids with smooth animations.

---

## 6. Layout Rules & Sizing

* Content container: `app-layout-container`; top padding is `1rem` below the navbar, bottom `1.5rem` (`3rem` on md+).
* Viewer panes (Split, Raw, Markdown Preview, history viewer/sidebar) use tall heights: `clamp(700px, 120vh, 1800px)` (history panes `calc(120vh - 15rem)`, min `700px`); the page scrolls vertically, each pane scrolls internally. Mobile stacks panes at `minmax(500px, 90vh)` each.
* The Convert result (`.home-result-container`) has no max-width, so it matches the Recent Conversions width (`app-layout-container`, up to 1680px); the rendered preview canvas (`.document-canvas`) is 99% of its pane.
* The history sidebar is **not** sticky (it is taller than the viewport).
* Line-number gutters (`.editor-line-numbers`) use `line-height: 1.344rem` (= textarea `0.84rem × 1.6`) and extra bottom padding so rows stay aligned with the textarea while scrolling.
* Hover on history sidebar items must not translate the element: the list scrolls and clips overflow, which cut the first item's border.
* Split panes are resizable (25-75 %) by pointer or keyboard (`←` `→`, `Shift` for larger steps, `Home`/`End`).

## 7. Theming Implementation

* `data-theme="dark|light"` on `<html>`; set before first paint from `localStorage["mdai-theme"]` (fallback to `prefers-color-scheme`).
* Tokens live in `wwwroot/css/modern-converter.css` (`--bg-app`, `--bg-surface`, `--text-primary`, `--editor-bg`, `--editor-line-numbers-*`, ...).
* `converterUtils.toggleTheme()` flips the attribute and stores the choice; Mermaid reads the current theme when it renders (`converterUtils.getCurrentTheme`).