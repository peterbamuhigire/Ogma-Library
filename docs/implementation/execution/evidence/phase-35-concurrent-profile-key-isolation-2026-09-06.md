# Phase 35 Concurrent Profile-Key Isolation

Date: 2026-09-06

## Defect closed

Concurrent first use of one student profile could observe no private-state key
in multiple operations, generate more than one candidate key, and race while
persisting it. Data encrypted with a losing candidate could then become
undecryptable after the repository was reopened.

The test-only/default in-memory credential implementation also used an ordinary
dictionary despite being reachable from concurrent Client operations.

## Corrected invariant

- Private-state key initialization is single-flight per profile within the
  singleton repository.
- The credential store is checked again after entering the profile gate, so a
  waiting operation uses the winner's persisted key.
- Generated key bytes are zeroed after persistence.
- Existing keys retain the lock-free read path.
- The in-memory credential implementation now uses a concurrent dictionary.
- Profiles retain separate database paths and key names.

## Executable proof

The new regression concurrently writes eight encrypted annotations during the
same profile's first use, reopens the repository, decrypts every body, verifies
that another profile sees no rows, and confirms distinct profile keys.

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StudentPrivateRepositoryTests" --logger "console;verbosity=minimal" -m:1
Passed: 8, Failed: 0, Skipped: 0

dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OgmaLibrary.Tests.ClassroomClient" --logger "console;verbosity=minimal" -m:1
Passed: 112, Failed: 0, Skipped: 0
```

## Residual gate

This closes the deterministic in-process first-use key race. Physical hostile
two-user sessions, OS credential-store behavior, filesystem permissions, and
cross-machine sync isolation remain `NOT ASSESSED`.
