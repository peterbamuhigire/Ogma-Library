# Phase 13 Provider Attribution Link Evidence

Date: 2026-09-05

## Delivered

The desktop book-detail enrichment tab now exposes provider attribution links
when provider-sourced fields are present and the catalogue has a valid ISBN.
The links are generated only for the two approved providers:

| Provider | Link form | Boundary |
| --- | --- | --- |
| Google Books | `https://books.google.com/books?q=isbn:{normalized-isbn}` | Fixed HTTPS host; ISBN is normalized to digits/`X` and bounded to 10–13 characters |
| Open Library | `https://openlibrary.org/isbn/{normalized-isbn}` | Fixed HTTPS host; ISBN is normalized to digits/`X` and bounded to 10–13 characters |

The UI uses localized headings and accessible action names. The click handler
revalidates the scheme and host before passing the URI to Avalonia's platform
launcher. No provider-supplied URL, raw response, credential, or local path is
used by this attribution path.

## Verification

- `BookDetailViewModelTests`: 11 passed, 0 failed, 0 skipped.
- The attribution proof verifies both allowlisted provider hosts and normalized
  ISBN construction.
- `dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Release --no-restore`:
  0 warnings, 0 errors.
- Full Release solution regression after the implementation: 1,097 passed
  (901 core, 41 architecture, 155 UI), 0 failed, 0 skipped.

## Gate disposition

Closed locally: provider attribution and link presentation now have an explicit
desktop consumer path and automated boundary proof.

Still open: stale-result labeling is not claimed closed because the current
persisted `MetadataLookup` projection does not carry the gateway's transient
`IsStale` flag end-to-end. Legal/privacy owner review, archived terms evidence,
live provider/network evidence, and physical UI acceptance remain open.
