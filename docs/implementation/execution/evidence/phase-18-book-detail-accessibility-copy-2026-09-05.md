# Phase 18 Book Detail Accessibility Copy

Date: 2026-09-05
Reviewer: Peter Bamuhigire, Lead Consultant

## Scope and user outcome

Book Detail and startup users relying on keyboard focus, voice control, or a
screen reader need stable names for closing the panel, opening a book,
enriching metadata, choosing a rating, and understanding migration progress.
The visible numeric rating controls remain compact; their spoken names now
communicate the action and selected value in the active locale.

## Decision

The existing Ogma type pairing and tokenized spacing are retained: the change
is semantic and localization-only, so no new typeface or visual treatment is
introduced. Existing display/body roles remain the deliberate choice for the
editorial catalogue surface; no font embedding or licence change is involved.

`BookDetailViewModel` owns the localized strings and raises their bindings on
culture change. `BookDetailView` binds accessibility names to those properties
instead of embedding English literals. The rating actions use one localized
parameterized resource so translators control sentence order. The startup
migration progress bar now binds to its localized progress text rather than a
separate English-only name. The catalogue shell's sidebar toggle and sort
control use the existing localized shell properties as their spoken names.

## Verification

| Gate | Evidence | Result | Residual risk |
|---|---|---|---|
| Implementation | `BookDetailViewModel`, `BookDetailView.axaml`, and `InMemoryLocalizationService` | PASS | None identified locally |
| Localisation | `Phase18DesignSystemTests` checks English, French, and pseudo-locale resources | PASS — 3/3 | RTL/CJK expansion not assessed |
| Build | `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | PASS — 0 warnings/errors | None locally |
| Accessibility | Resource-backed names and culture-change notifications | CONDITIONAL | Physical Narrator/VoiceOver/voice-control walkthrough remains NOT ASSESSED |
| Visual QA | No layout, colour, font, or asset change; existing design tokens retained | CONDITIONAL | Reference-device rendering remains NOT ASSESSED |

## Handoff

Changed surfaces: Book Detail close action, reader/enrichment actions, and
one-to-five-star rating actions. The next owner should run the installed
Windows Narrator and macOS VoiceOver journeys, including French and a
pseudo-locale, before closing Phase 18.
