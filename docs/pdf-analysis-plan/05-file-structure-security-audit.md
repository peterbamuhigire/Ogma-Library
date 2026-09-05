# File structure, security and containment audit

## Positive foundations

The current plan correctly treats PDFs as untrusted input. The broker checks
root containment, extension, existence, size and `%PDF-` magic before parser
entry. The worker copies input into a private sandbox, uses one-shot stdin for
passwords, verifies output paths/size, and applies Windows Job Object limits.
The progress ledger records these as implemented slices.

## Material gaps

### Structural validation

Header and magic bytes do not establish a valid PDF. The opener still needs
bounded resolution of the trailer/xref chain, classic and stream xrefs, object
streams, incremental updates, filters, page tree and encryption dictionary.
Strict and lenient outcomes must be distinct. The current broad catches that
return page count zero, empty text or fallback dimensions risk hiding the cause
and make quality telemetry unreliable.

### Containment

The worker boundary is not the same as a real OS sandbox. Environment flags
that say network or child processes are disabled are not proof of denial. The
remaining gate explicitly requires Windows/macOS filesystem/network/child
process escape evidence and independent security approval.

### File identity and TOCTOU

The broker validates a path, then the worker copies it. A source can change
between those events. Open must establish a stable snapshot/content hash and
all derived artifacts must bind to that hash. A changed source must fail or
restart deterministically, never mix pages from two versions.

### Passwords and active content

Password handling must distinguish absent, wrong, empty and correct passwords;
permissions must not be treated as authorization to execute content. JavaScript,
launch actions, external URLs, embedded files, multimedia and 3D require a
deny-by-default policy and visible safe recovery. Signatures require a separate
validation story; rewriting a signed file must be blocked or clearly invalidated.

## Required controls

- strict structural inspection plus a separate lenient recovery mode;
- bounded object/stream decompression, page count, image dimensions, output,
  CPU, memory, wall-clock and concurrency limits;
- real per-platform sandbox profiles with deny-by-default network/filesystem;
- worker crash isolation and typed document/page/resource error codes;
- stable source snapshot hash and parser/renderer/config version on every
  artifact and cache entry;
- no password in command line, environment, logs, crash dumps, DB or telemetry;
- safe action broker: never invoke a process or unrestricted URL from PDF data;
- mutation transaction for write-back: backup, diff, confirm, write temp,
  re-open/validate, hash, atomic replace, restore and audit.

## Security acceptance corpus

Include malformed xrefs, cyclic objects, enormous values, decompression bombs,
huge images/pages, encryption/password permutations, incremental updates,
embedded files, launch/JavaScript actions, external links, annotations/forms,
symlink/reparse/traversal, source replacement during copy, worker crash and
output path escape attempts. Each fixture needs lawful provenance and expected
behavior.
