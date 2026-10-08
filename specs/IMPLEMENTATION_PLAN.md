# Implementation Plan - DocumentMDConverter

This document tracks the roadmap, execution phases, and acceptance criteria for the **DocumentMDConverter** application.

---

## Phase 1: Unified Conversion Core in C# (.NET 10) ✅ Complete
**Objective:** Create the application and infrastructure services capable of executing `anydoc` with graceful fallback to `Google Cloud Vision API`.

1. **NuGet Package Management:**
   - Central Package Management (CPM) via `Directory.Packages.props`.
   - `Google.Cloud.Vision.V1`: Official Google SDK for high-precision document OCR.
   - `Markdig`: High-performance CommonMark / GFM Markdown processor for Blazor HTML rendering.
2. **Domain Models & Interfaces:**
   - `ConversionRequest` (Stream, FileName, OcrEnabled, Options).
   - `ConversionProgress` (StepName, Percentage, Detail).
   - `ConversionResult` (JobId, MarkdownText, EngineUsed, DurationMs, WordCount, TokenEstimate, ErrorMessage, IsSuccess, StorageUri).
   - `IDocumentConverterService`: Orchestrator interface with `IProgress<ConversionProgress>` support.
3. **`AnydocEngineService` Implementation:**
   - Subprocess invocation of `anydoc` CLI with cross-platform OS resolution.
   - Warmup background service (`AnydocWarmupBackgroundService`) eliminating first-hit cold start latency.
4. **`GoogleVisionOcrService` Implementation:**
   - Invocation of `ImageAnnotatorClient.DetectDocumentTextAsync`.
   - Structural mapping of blocks, paragraphs, and break types into Markdown.
5. **Orchestrator `DocumentConverterService`:**
   - Unified execution pipeline: executes anydoc -> upon scanned document detection, triggers Google Cloud Vision OCR -> saves metadata to GCS.

---

## Phase 2: Modern Blazor User Interface ✅ Complete
**Objective:** Deliver a high-end visual experience (Dark & Light mode, glassmorphism, responsive navigation).

1. **Design System & CSS Tokens (`modern-converter.css`):**
   - Dual theme system with CSS variables (`--bg-app`, `--bg-surface`, `--text-primary`, `--accent-soft`).
   - Persistent theme toggle button with instant swap.
2. **Upload Component (`DocumentDropzone.razor`):**
   - Drag & drop zone with Blazor `InputFile` support.
   - Real-time progress bar powered by SignalR (`IProgress<ConversionProgress>`).
   - File size validation and allowed extensions enforcement.
3. **Dual Markdown Viewer (`MarkdownViewer.razor`):**
   - Segmented tab switch: "Preview" (rendered HTML via `Markdig`), "Split" (side-by-side), and "Raw" (syntax code).
   - 1-click clipboard copy via JavaScript Interop.
   - Direct `.md` file download.
   - Real-time word count and estimated LLM tokens (~3.8 chars/token).

---

## Phase 3: REST API & Cloud Endpoints ✅ Complete
**Objective:** Enable headless consumption for scripts, automated pipelines, and health monitoring.

1. **Segregated Minimal APIs:**
   - `POST /api/documents/convert`: Direct synchronous conversion (multipart form `file`, optional `enableOcrFallback`, `format`; returns JSON).
   - `GET /api/history`: List recent conversions for the authenticated user.
   - `GET /api/history/download`: Direct redirect (`302 Found`) to GCS V4 Signed Download URL.
   - `GET /api/history/markdown-content`: Returns stored Markdown as JSON.
   - `POST /api/batch/zip`: Builds a ZIP of selected Markdown outputs.
   - `GET /api/health`: Health probe endpoint for Cloud Run container lifecycle.

---

## Phase 4: Modular Terraform Infrastructure in GCP ✅ Complete
**Objective:** Multi-environment deployment with security, auto-cleanup, and SignalR WebSocket affinity.

1. **Reusable Modules (`infra/modules/`):**
   - `iam`: Dedicated Service Account with minimal permissions (`roles/serviceusage.serviceUsageConsumer`, `roles/bigquery.jobUser`, `roles/iam.serviceAccountTokenCreator`).
   - `storage`: Temporary GCS bucket with 7-day auto-purge lifecycle, CORS, and disabled soft-delete.
   - `artifact_registry`: Private Docker repository.
   - `compute`: Cloud Run v2 with `session_affinity = true` for persistent Blazor circuits and IAP access bindings.
2. **Environments (`infra/environments/`):**
   - `dev`: Scale-to-0 serverless setup (`min_instances = 0`, `max_instances = 2`), release by pushing a new image tag and bumping `container_image` in `terraform.tfvars` (see `infra/README.md`).
   - `prod`: High-availability configuration (`max_instances = 10`).

---

## Phase 5: GCS Persistence, Widescreen Split Layout & Strict Quality ✅ Complete
**Objective:** Zero-database serverless history, direct cloud downloads, responsive multi-pane layout, and zero warnings.

1. **Serverless GCS History & Direct Downloads:**
   - Saves `metadata.json` alongside converted files in `gs://<bucket>/users/<email>/<jobId>/`.
   - Downloads original files and `.md` outputs directly from Cloud Storage via V4 Signed URLs (zero server RAM streaming).
   - 7-day automatic lifecycle expiration.
2. **Identity-Aware Proxy (IAP) Integration:**
   - Authenticated user identification via `X-Goog-Authenticated-User-Email`.
   - Top-bar user badge with live connection status.
3. **Responsive Master-Detail Split View & Mobile Drill-Down Navigation:**
   - Default history view: Master-Detail split layout with 360px compact sidebar playlist (`HistoryListItem.razor`) and full [`MarkdownViewer.razor`](file:///C:/personal.projects/MDConverter/MDAIConverter/src/DocumentMDConverter.Web/Components/Shared/MarkdownViewer.razor).
   - Auto-selection of most recent document on load for desktop (zero wasted screen space).
   - Mobile & tablet ($<$ 992px): Clean drill-down navigation (full-width playlist $\rightarrow$ full-width viewer with `[ ← Back to conversions ]`).
   - Compact word count notation starting at 100,000 words (`~100k`, `~110k`, `~1M`) via `FormatUtils.FormatWordCount`.
   - Dual download buttons in viewer header (`[Original (.ext)]` and `[Download .md]`) with clutter-free sidebar items.
   - Strict CSS Flexbox constraints (`min-w-0`, `overflow-x: hidden`, `text-truncate`) preventing horizontal overflow.
   - Full brand favicon replacement (`favicon.png`, `favicon.ico`, `favicon.svg`) with cache-busting.
4. **Strict Code Quality & Dead Code Cleanup:**
   - `<AnalysisMode>All</AnalysisMode>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` with **0 Warnings, 0 Errors**.
   - Pure functional Result pattern with native `.Match()` methods.
   - Centralized formatting in `FormatUtils`.

---

## Phase 6: Batch, Markdown Playground & UI Polish ✅ Complete
**Objective:** Productivity features and visual hardening.

1. **Batch conversion:** multi-file staging (max 20), sequential processing, per-item status, ZIP download (`POST /api/batch/zip`), multi-select ZIP from history.
2. **Markdown Preview tab:** live editor/preview with resizable split, line numbers, Mermaid (strict security, theme aware), HTML disabled; preview written via JS interop to avoid Blazor/Mermaid DOM conflicts.
3. **Global ingestion:** page-wide drag & drop and clipboard paste of images/files.
4. **Layout & dark-mode fixes:** taller panes, aligned line-number gutters, readable sequence diagrams in dark mode, history hover border, tighter navbar-to-tabs spacing.
5. **Open items / ideas:** per-user path validation on download endpoints, streaming ZIP, tabs inside the navbar, prod IAP variables in Terraform.

---

## Acceptance Criteria
- [x] Supports `.docx`, `.pptx`, `.xlsx`, `.csv`, `.pdf` conversion.
- [x] anydoc executes at sub-second speeds with background warmup.
- [x] Scanned documents automatically fall back to Google Cloud Vision OCR.
- [x] Clean Architecture with segregated interfaces and Dependency Injection files.
- [x] Central Package Management and strict .editorconfig rules.
- [x] Real-time interactive UI with SignalR and dual theme toggle (Dark / Light).
- [x] 7-day serverless history in Google Cloud Storage with direct V4 Signed URL downloads.
- [x] Widescreen Master-Detail Split View with Auto-Select and Mobile Drawer.
- [x] Google Cloud IAP user authentication and badge.
- [x] Modular Terraform validated and live in Cloud Run.
- [x] Batch conversion (up to 20 files) with ZIP export.
- [x] Markdown Preview tab with Mermaid diagrams (light and dark).
- [x] Standalone images routed straight to Cloud Vision OCR.
