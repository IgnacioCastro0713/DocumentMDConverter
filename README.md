<div align="center">

# 📄 DocumentMDConverter

**Turn Word, PowerPoint, Excel/CSV, PDF and images into clean, structured Markdown (GFM), optimized for LLMs (Gemini, Claude, GPT) and human review.**

A hybrid engine: [**anydoc**](https://github.com/firecrawl/anydoc) (native Rust CLI, $0 compute) with automatic fallback to **Google Cloud Vision OCR** for scanned PDFs and images.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Blazor Server](https://img.shields.io/badge/Blazor-Server-5C2D91)
![Cloud Run](https://img.shields.io/badge/GCP-Cloud%20Run-4285F4?logo=googlecloud&logoColor=white)
![Terraform](https://img.shields.io/badge/IaC-Terraform-7B42BC?logo=terraform&logoColor=white)
![Powered by anydoc](https://img.shields.io/badge/powered%20by-anydoc%20(Rust)-DEA584?logo=rust&logoColor=white)

<img src="docs/screenshot.png" alt="DocumentMDConverter UI" width="900">

</div>

---

## ✨ 1. Highlights

- ⚡ **Fast and cheap:** conversion runs on [`anydoc`](https://github.com/firecrawl/anydoc), a native Rust engine, so there is no per-document API cost.
- 🔍 **Smart OCR fallback:** scanned PDFs and images go to Google Cloud Vision automatically, only when needed.
- 📥 **Easy input:** drag & drop, file picker, `Ctrl+V` paste, batches of up to 20 files (30 MB each).
- 🕘 **History:** search, filter, multi-select and ZIP download. Items expire after 7 days.
- 📝 **Live Markdown editor** with split view and Mermaid diagrams.
- 🌗 **Light and dark themes.**
- ☁️ **Serverless:** one container on Cloud Run, no database, Terraform included.

---

## 🖼️ Screenshots

<table>
  <tr>
    <td width="50%"><img src="docs/history.png" alt="Recent Conversions: history, engine and metrics"><br><sub><b>Recent Conversions</b>: search, filters, engine and metrics</sub></td>
    <td width="50%"><img src="docs/markdown-preview.png" alt="Markdown Preview: live editor and rendered view"><br><sub><b>Markdown Preview</b>: live editor with Mermaid support</sub></td>
  </tr>
</table>

---

## 🦀 2. Powered by anydoc

[**anydoc**](https://github.com/firecrawl/anydoc) by Firecrawl is the core of this project. It is a Rust CLI that converts office documents and PDFs to Markdown without any external service.

- Handles `.docx`, `.pptx`, `.xlsx`, `.csv`, `.pdf` and more, keeping headings, lists and tables (GFM).
- Runs as a local subprocess: no network calls and no cost per page.
- Detects scanned PDFs (exit code `3`) so the app can hand them to Cloud Vision OCR.
- Distributed on npm (`@firecrawl/anydoc`). The Docker image installs it globally. Locally it is fetched with `npx` if no binary is configured.

Many thanks to the Firecrawl team for building it. 🙌

---

## 🧭 3. Supported Formats

| Input | Engine | Output |
| :--- | :--- | :--- |
| `.docx` `.doc` `.odt` `.rtf` | **anydoc** | Headings, lists, emphasis |
| `.xlsx` `.xls` `.csv` `.ods` | **anydoc** | GFM tables |
| `.pptx` `.ppt` | **anydoc** | Slide Markdown |
| `.pdf` with text | **anydoc** | Paragraphs, headings, tables |
| `.pdf` scanned | anydoc detects it → **Cloud Vision OCR** | One `## Page N` section per page |
| `.png` `.jpg` `.jpeg` `.webp` | **Cloud Vision OCR** | OCR text |

`.epub` is rejected.

---

## 🏛️ 4. System Architecture

One .NET 10 monolith (Blazor Server + Minimal APIs) in a single Cloud Run container. Google Cloud Storage holds files, outputs and history metadata; a lifecycle rule purges everything after 7 days.

```mermaid
flowchart LR
    User["Browser"] -->|"HTTPS"| IAP["Identity-Aware Proxy"]
    IAP --> Run

    subgraph GCP["Google Cloud Platform"]
        subgraph Run["Cloud Run"]
            UI["Blazor Server UI"]
            API["Minimal API /api/*"]
            App["DocumentConverterService"]
            Anydoc["anydoc (Rust)"]
            UI --> App
            API --> App
            App -->|"subprocess"| Anydoc
        end
        Vision["Cloud Vision OCR"]
        GCS[("Cloud Storage, 7-day lifecycle")]
        App -->|"scanned PDFs / images"| Vision
        App -->|"input, output, metadata"| GCS
    end
```

Clean Architecture layers: `Domain` → `Application` → `Infrastructure` → `Web`, under `src/`.

### Conversion flow

Every upload, from the UI or from `POST /api/documents/convert`, goes through the same orchestrator (`DocumentConverterService`):

```mermaid
flowchart TD
    A["1. Upload"] --> V{"2. Valid? not empty, not .epub, ≤ 30 MB"}
    V -->|"no"| E["Typed error (400)"]
    V -->|"yes"| T["3. Buffer to temp file + save original to GCS"]
    T --> I{"4. Image?"}
    I -->|"yes"| OCR["Cloud Vision OCR"]
    I -->|"no"| AD["anydoc (60 s timeout)"]
    AD -->|"ok"| MD["5. Markdown"]
    AD -->|"needs OCR + fallback enabled"| OCR
    AD -->|"other error"| E2["Typed error (422 / 504 / 500)"]
    OCR --> MD
    MD --> M["6. Words, characters, ~tokens"]
    M --> S["7. Save .md + metadata.json to GCS (expires in 7 days)"]
    S --> R["8. Result: Markdown, engine used, metrics"]
```

| Step | What happens |
| :--- | :--- |
| 1-2. **Validate** | Rejects empty files, `.epub` and anything over 30 MB before any work is done. |
| 3. **Stage** | The file is copied to a temp file (deleted at the end, even on failure). If Cloud Storage is configured, the original is also stored under `users/<email>/<jobId>/input/` so it can be downloaded later. |
| 4. **Pick the engine** | Images go straight to Cloud Vision. Everything else goes to **anydoc**, which is free and local. |
| 5. **Fallback** | If anydoc reports a PDF with no text layer (exit code `3`) and `enableOcrFallback` is on, the same file is sent to Cloud Vision. If it is off, the caller gets a `422`. |
| 6. **Metrics** | Words, characters and estimated tokens (characters ÷ 3.8), shown as pills in the viewer. |
| 7. **Persist** | The Markdown and a `metadata.json` are saved for the history. A storage failure is **non-fatal**: the Markdown is still returned, it just will not appear in history. |
| 8. **Result** | The response says which engine produced the text (`anydoc` or `Google Cloud Vision OCR`). |
### 💡 Architectural Decision: Local Engine First, Paid OCR Only When Needed

- **The naive approach:** send every document to a cloud OCR/AI API. Simple, but it costs money per page and adds latency and a hard dependency on the network, even for documents that already contain selectable text.
- **Our approach:** `anydoc` converts text-based files locally in-process-tree. Only when it reports that a PDF has no text layer (exit code `3`) does the app call Cloud Vision, which gives 1,000 free pages per month.
- **Conclusion:** most documents cost **$0**, and the paid path is limited to files that really need it.

### 💡 Architectural Decision: Serverless Persistence and Zero-RAM Downloads

- **No database:** history is just `metadata.json` objects under a per-user prefix in Cloud Storage. A bucket lifecycle rule deletes everything after 7 days, so there is nothing to clean up or migrate.
- **No streaming through the app:** downloads are a `302` redirect to a short-lived V4 signed URL, so file bytes never pass through Cloud Run memory.
- **Session affinity** keeps each Blazor circuit pinned to one Cloud Run instance.

---

## 🚀 5. Quick Start

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/) and [Node.js 20+](https://nodejs.org/) (for `anydoc` via `npx`). Cloud Vision and Cloud Storage need `gcloud auth application-default login`, but plain conversions work without them.

```bash
dotnet run --project src/DocumentMDConverter.Web/DocumentMDConverter.Web.csproj
```

Open `http://localhost:5070`. To enable history locally, copy `appsettings.Example.json` to `appsettings.Local.json` (git-ignored) and set your bucket.

---

## 🔌 6. REST API

| Method | Route | Description |
| :--- | :--- | :--- |
| `POST` | `/api/documents/convert` | Multipart `file`, optional `enableOcrFallback`. Returns the conversion result. |
| `GET` | `/api/history` | The current user's history. |
| `GET` | `/api/history/download?path=` | `302` to a 1-hour signed URL. |
| `GET` | `/api/history/markdown-content?path=` | Markdown of a stored result. |
| `POST` | `/api/batch/zip` | ZIP of several results. |
| `GET` | `/api/health` | Health probe. |

```bash
curl -X POST http://localhost:5070/api/documents/convert -F "file=@report.docx" -F "enableOcrFallback=true"
```

---

## ⚙️ 7. Configuration

| Setting | Purpose |
| :--- | :--- |
| `GCS_TEMP_BUCKET` | Enables Cloud Storage and history. Without it, conversions work but nothing is stored. |
| `ANYDOC_BIN_PATH` | Path to the `anydoc` binary. Falls back to `/usr/bin/anydoc`, then `npx @firecrawl/anydoc`. |
| `Iap:DefaultDevUserEmail` | Identity used locally when there is no IAP header. |

---

## 🐳 8. Docker & Deployment

```bash
docker build -f src/DocumentMDConverter.Web/Dockerfile -t document-md-converter .
```

The image installs Node.js and `@firecrawl/anydoc`. Infrastructure (Cloud Run, Storage, IAM, Artifact Registry) is Terraform in [`infra/`](infra/README.md), which also covers the release procedure.

---

## ⚠️ 9. Known Limitations

- Limits: 30 MB per upload, 20 files per batch (processed sequentially). Vision OCR for PDFs: ≤ 10 MB and ≤ 5 pages.
- ZIP files are built in memory.
- The history download endpoints do not check that `path` belongs to the caller. They rely on IAP and unguessable job IDs, so add a `users/<caller>/` prefix check before wider exposure.
- Mermaid loads from a CDN, so offline environments show the diagram source.

---

## 📚 10. Further Documentation

- [`specs/ARCHITECTURE.md`](specs/ARCHITECTURE.md): technical architecture, storage schema, sequence diagrams
- [`specs/UI_UX_SPEC.md`](specs/UI_UX_SPEC.md): design system and component behavior
- [`specs/IMPLEMENTATION_PLAN.md`](specs/IMPLEMENTATION_PLAN.md): phases and acceptance criteria
- [`infra/README.md`](infra/README.md): Terraform, IAM and release procedure