# Phase 29 Progress - Grounded Explanations and Answer Mode

Date: 2026-09-04

## Delivered in this increment

- Expanded recommendation provenance and answer citations with source labels,
  evidence contract versions, and explicit uncertainty labels.
- Added field-level recommendation provenance validation: a provider claim must
  match the cited local title, author, tag, description or note value before it
  is retained; unsupported claims degrade to a clearly marked local fallback.
- Hardened local answer mode as extractive evidence presentation with bounded
  control-character-safe excerpts, duplicate citation suppression, source labels,
  page/chunk anchors, and an explicit “evidence excerpts” limitation statement.
- Enforced the metadata-only/content-aware boundary: page, note and TOC passages
  require `AllowContentAwareTier`; metadata tags and descriptions remain usable
  without content-aware opt-in.
- Preserved useful grounded results when semantic relevance is unavailable by
  labeling exact-text fallback uncertainty instead of presenting it as semantic
  certainty.
- Added an untrusted-evidence boundary to provider payloads: metadata and
  content passages are explicitly labeled as data, structural delimiters are
  escaped, and prompt-injection fixture coverage proves embedded markup cannot
  create a second provider message structure.
- Added a versioned source-label map to recommendation payloads and prompt
  instructions requiring provider provenance to echo the local evidence source;
  the existing field-level validator remains the final authority before any
  claim reaches the user.
- Added a durable, privacy-safe answer-evidence trace. It records a hashed
  question identity, bounded search/citation counts, outcome, and citation
  provenance without storing the question, answer text, or evidence excerpts.
- Added a desktop local-answer action to the Advisor surface. It sends only a
  metadata-only `AnswerRequest`, renders the bounded local answer and citation
  excerpts, and preserves explicit no-evidence output.
- Made displayed local citations actionable: the desktop Advisor opens the
  cited book in the reader and converts the validated one-based citation page
  to the reader's zero-based page hint, with a detail fallback for reduced
  compositions.
- Added an explicit unchecked-by-default consent control for page and note
  evidence; the user's choice is passed to the existing content-aware tier
  boundary and is covered by a default-deny regression.
- Added a deterministic unsupported-claim/abstention benchmark covering 24
  fabricated provenance fixtures plus a no-local-evidence case; every fixture
  is marked uncertain or abstains without citations.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed with
  0 warnings and 0 errors before the final test-only adjustment; the same code
  path is covered by the focused test build.
- Phase 29 grounded evidence and answer slice: 6 passed.
- Existing local answer regression slice: 6 passed.
- Source-labeled recommendation payload slice: 10 passed across recommendation
  pipeline and grounded-evidence tests.
- Durable answer-evidence trace and grounded citation slice: 6 passed.
- Advisor view-model answer slice: 48 passed; headless answer-surface render:
  1 passed; Release build: 0 warnings and 0 errors.
- Fresh citation-navigation regression: 6 core advisor tests and 1 headless
  Advisor render test passed.
- Content-aware consent regression: 7 advisor tests passed.

## Remaining phase gate

The bounded unsupported-claim/abstention benchmark is closed. The desktop
answer display remains local/evidence-only; provider-generated explanations
still receive assembled, versioned source-label evidence. Physical UI evidence
remains unassessed.
