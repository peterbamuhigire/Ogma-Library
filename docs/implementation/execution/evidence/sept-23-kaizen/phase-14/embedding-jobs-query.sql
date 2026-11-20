-- Phase 14 (Sept-23 Kaizen), K25 embedding part: failed and retried embedding jobs.
-- Run read-only against the session catalogue.db of a journey (fresh OGMA_LIBRARY_DATA_DIR).
-- Source: tests/OgmaLibrary.Tests.E2E/Journeys/SemanticSearchTests.cs (ReadEmbeddingJobs).
-- Status values: 2 Completed; 3, 5 Failed; 0, 6, 7 Pending, Paused, WaitingForCapability ("parked").
SELECT COUNT(*)                                    AS Total,
       COALESCE(SUM(Status = 2), 0)                AS Completed,
       COALESCE(SUM(Status IN (3, 5)), 0)          AS Failed,
       COALESCE(SUM(Status IN (0, 6, 7)), 0)       AS Parked,
       COALESCE(SUM(RetryCount), 0)                AS Attempts,
       COALESCE(SUM(StartedUtc IS NOT NULL), 0)    AS Started
FROM Jobs
WHERE JobType IN ('EmbeddingJob', 'EmbeddingGeneration');

-- MEASURED results, 2026-11-20, final head of kaizen/phase-14 (run ids in real-window-journeys-final.json):
-- Before (Sept-25 audit, K25/K42): 16 embedding jobs, 16 failed, retrying (provider absent).
-- J-SEM-N1 1280x800, no model, 10 min real idle:  Total 14, Completed 0,  Failed 0, Parked 14, Attempts 0,  Started 0
-- J-SEM-N2 1280x800, damaged model files:         Total 14, Completed 0,  Failed 0, Parked 14, Attempts 0,  Started 0
-- J-SEM-N2 1920x1080, damaged model files:        Total 12, Completed 0,  Failed 0, Parked 12, Attempts 0,  Started 0
-- J-SEM-1 1280x800, built-in model, index built:  Total 14, Completed 14, Failed 0, Parked 0,  Attempts 14, Started 14
