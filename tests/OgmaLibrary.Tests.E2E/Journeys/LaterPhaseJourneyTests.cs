using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// G11 and G12 are registered by Phase 01 (T01.12) and implemented by their owning phases. Until
/// then they report NOT ASSESSED — the test fails in the raw runner and is never a pass. G10 is real
/// since Sept-23 Phase 10 (<see cref="CatalogueOrganiseTests"/>) and G9 since Sept-23 Phase 11
/// (<see cref="DetailTests"/>).
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class LaterPhaseJourneyTests
{
    /// <summary>G11: classroom host, client join, browse, read, search, revoke.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G11")]
    [Trait("Journey", "HostStartStop")]
    [Trait("Tag", "Classroom")]
    public void G11_Classroom_Placeholder() => Journey.Placeholder("G11", "Phases 19-20");

    /// <summary>G12: 3D shelf with real covers, keyboard alternative, WebGL2 fallback.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G12")]
    [Trait("Tag", "Shelf3D")]
    public void G12_Shelf3D_Placeholder() => Journey.Placeholder("G12", "Phase 18");
}
