# Filesystem identity scenario specification

These are Phase 3 expected outcomes. Phases 4 and 7-9 must turn them into
persistence, scanner and reconciliation integration tests.

| Scenario | Evidence | Required identity outcome | Forbidden outcome |
| --- | --- | --- | --- |
| New PDF | New locator and verified hash | New occurrence and asset; provisional edition/work until identified | Infer edition from filename alone |
| Rename in one root | Old locator absent, new locator present, same hash | Preserve asset/edition/work; update occurrence locator through reconciliation | Create another catalogue item |
| Move between approved roots | Same hash at new root | Preserve content and bibliographic identity; occurrence/root transition follows Phase 8 policy | Treat missing old path as user deletion |
| File contents replaced | Same locator, different verified hash | Preserve occurrence history; create/link new asset and reprocess | Keep stale asset/index identity |
| Exact copy | Different locator, equal verified hash | Two occurrences, one exact content asset | Collapse occurrence records |
| Different encoding of one edition | Different hashes, shared strong edition identifier | Reviewable same-edition proposal | Automatic merge |
| Different edition of one work | Shared work ID, conflicting edition IDs | Reviewable same-work/different-edition proposal | Collapse editions |
| Similar title only | Title/author similarity | Possible match requiring review | Merge work or edition |
| Root disconnected | Root health failure; observations incomplete | Mark unavailable/unknown observation state; retain curated catalogue | Delete records or infer intentional removal |
| Explicit delete | User-authorized delete workflow (later phase) | Apply defined file/catalogue deletion policy with audit/recovery | Equate scan absence with authorization |
