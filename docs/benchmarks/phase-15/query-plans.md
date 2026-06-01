# Phase 15 Smart-Shelf Query Plans

Date: 2026-06-01

Corpus: 2,000 synthetic books in a migrated SQLite catalogue, with shelf
memberships and metadata fields for category, tag, and language.

## Findings

| Query | SQLite query plan | Result |
| --- | --- | --- |
| Active books since 2010 | `SEARCH Books USING COVERING INDEX IX_Books_Status_Year (Status=? AND Year>?)` | Uses the Phase 15 status/year index |
| Shelf members with high rating | `SEARCH sb USING COVERING INDEX IX_ShelfBooks_ShelfId_BookId (ShelfId=?)`; `SEARCH b USING INDEX sqlite_autoindex_Books_1 (BookId=?)` | Starts from the shelf covering index, then resolves books by primary key |
| Science category | `SEARCH BookMetadataFields USING COVERING INDEX IX_BookMetadataFields_FieldName_Value (FieldName=? AND Value=?)` | Uses the Phase 15 metadata field/value index |
| Active reference-tagged books | `SEARCH f USING INDEX IX_BookMetadataFields_FieldName_Value (FieldName=? AND Value=?)`; `SEARCH b USING INDEX sqlite_autoindex_Books_1 (BookId=?)` | Uses the metadata index before joining books |
| Active science books since 2000 | `SEARCH b USING INDEX IX_Books_Status_Year (Status=? AND Year>?)`; `SEARCH f USING INDEX IX_BookMetadataFields_FieldName_Value (FieldName=? AND Value=?)` | Uses both Phase 15 smart-shelf indexes |

`COUNT(DISTINCT ...)` query shapes can require a temporary B-tree, but the table
access itself still uses the intended Phase 15 indexes. On the local 2,000-book
corpus, the measured P95 remained below the NFR-OGMA-002 2,000 ms budget.

## Verification

Command:

```powershell
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase15SmartShelfPerformanceTests --logger "console;verbosity=detailed"
```

Result: 3 passed.
