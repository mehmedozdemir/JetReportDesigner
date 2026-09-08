# PDF Engine Decision (Phase 0 Spike)

> Status: **Decided (provisional)** — PdfSharp/MigraDoc is primary; QuestPDF stays
> referenced as a fallback and the choice is re-confirmed at the end of Phase 2,
> once banded pagination stress is known. The `IPdfRenderer` abstraction keeps the
> swap cheap.

## Why a spike

The plan does not fix the PDF engine (QuestPDF's Community licence is revenue-gated
and the user does not want a licence constraint). `.Rendering` defines
`IPdfRenderer`; two implementations are built side by side and compared before one
is kept.

## Candidates

| Engine | Package | Licence | Notes |
|--------|---------|---------|-------|
| QuestPDF | `QuestPDF` | Community (free below revenue threshold) / paid above | Modern API, native pagination, **bundles fonts via SkiaSharp** (no OS font provisioning). |
| PdfSharp / MigraDoc | `PdfSharp`, `PDFsharp-MigraDoc` | MIT (no threshold) | `XGraphics` for absolute placement. **No bundled fonts** — needs a custom `IFontResolver` + font files on non-Windows. |
| PuppeteerSharp (fallback) | `PuppeteerSharp` | MIT | HTML→PDF via headless Chromium. Adds a ~300 MB Chromium dependency and higher per-render cost; not wired yet. |

## Method

1. Render `SampleDocuments.HelloWorld()` (text alignment, a rule, a bordered box)
   with each engine.
2. Compare on:
   - **Fidelity** — element positions within ±2px of the model.
   - **Fonts / cross-platform** — does it run on the Linux CI image with no extra setup?
   - **Performance** — cold and warm render time for the sample; time for a
     500-row synthetic banded report.
   - **Deployment cost** — image size delta, native dependencies.
   - **Licence** — constraint at expected company revenue.
3. Record numbers in the table below; pick one; delete the other implementation
   and its package reference.

## Results

`RUN_PDF_BENCHMARK=1 dotnet test tests/JetReportDesigner.Rendering.Tests --filter Compare_Engines`
on Windows 11, .NET 10, `SampleDocuments.HelloWorld()`:

| Criterion | QuestPDF 2026.8 | PdfSharp/MigraDoc 6.2.4 |
|-----------|-----------------|--------------------------|
| Sample renders (Windows) | yes | yes (Windows core fonts) |
| Sample renders (Linux CI) | **expected yes, unverified** — SkiaSharp bundles a fallback font; may still need `libfontconfig1` in the image | **no as-is** — `WindowsCoreFontResolver` throws `PlatformNotSupportedException`; needs bundled font files + a resolver that reads them |
| Output size (hello world) | 17 KB | 65 KB (embeds a full font face) |
| Cold render | ~2.5 s (SkiaSharp init + JIT) | ~1.0 s |
| Warm render | ~18 ms | ~24 ms |
| Native dependencies | SkiaSharp (~10–15 MB in image) | none |
| Licence | Community, free only below a revenue threshold | MIT, no threshold |
| Pagination for banded reports | strong, native | manual — we write the vertical flow/pager |

## Decision

**Primary: PdfSharp/MigraDoc.** Reasons: MIT licence with no revenue constraint
(the user's stated requirement), no native dependencies, smaller runtime image,
faster cold start. It draws via `XGraphics`, which suits our absolute-positioned
render model directly.

**Cost we accept, scheduled into Phase 1:**
- Bundle open fonts (Liberation Sans / Serif / Mono, ~1 MB) as embedded resources
  in `.Rendering` and replace `WindowsCoreFontResolver` with a resolver that reads
  them — makes rendering identical on Windows and Linux and removes the CI gap.
- Larger PDFs: mitigated by font subsetting (PdfSharp 6.2 supports it) once real
  fonts are wired.

**QuestPDF stays referenced** behind `IPdfRenderer` through Phase 2 as a fallback.
Trigger to switch: if manual banded pagination (Phase 2) proves too costly or
low-fidelity versus QuestPDF's native paging. Final call — and dropping the unused
package — at the end of Phase 2.

**Not pursued:** PuppeteerSharp — the Chromium dependency and per-render cost are
disproportionate for V1.

## Follow-ups

- [ ] Phase 1: embed Liberation fonts + cross-platform `IFontResolver`; delete `WindowsCoreFontResolver`.
- [ ] Verify QuestPDF renders on the `mcr.microsoft.com/dotnet/aspnet:10.0` image (add `libfontconfig1` if needed) so the fallback stays real.
- [ ] Phase 2 end: confirm or switch; remove the losing package + its renderer.
