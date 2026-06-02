using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 private-state sync blob encryption tests.</summary>
public sealed class ClassroomSyncBlobCodecTests
{
    private static readonly Guid ProfileId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SyncBlob_SerializesCompressesEncryptsAndRoundTripsPrivateState()
    {
        var codec = new ClassroomSyncBlobCodec();
        ClassroomSyncSnapshot snapshot = CreateSnapshot();

        EncryptedClassroomSyncBlob blob = codec.Encode(snapshot, "session-token");
        ClassroomSyncSnapshot decoded = codec.Decode(blob, "session-token");

        Assert.Equal(ClassroomSyncBlobCodec.CurrentVersion, blob.Version);
        Assert.Equal(ClassroomSyncBlobCodec.BlobContentType, blob.ContentType);
        Assert.DoesNotContain("Important idea", Encoding.UTF8.GetString(blob.Content), StringComparison.Ordinal);
        AssertSnapshotEqual(snapshot, decoded);
    }

    [Fact]
    public void SyncBlob_DecryptFailure_OnWrongSessionToken()
    {
        var codec = new ClassroomSyncBlobCodec();
        EncryptedClassroomSyncBlob blob = codec.Encode(CreateSnapshot(), "session-token");

        Assert.ThrowsAny<CryptographicException>(() => codec.Decode(blob, "different-session-token"));
    }

    [Fact]
    public void SyncBlob_Codec_IsRegisteredInClassroomClientServices()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddClassroomClientServices(
                Path.Combine(Path.GetTempPath(), $"ogma-classroom-sync-codec-{Guid.NewGuid():N}"),
                new InMemoryClassroomCredentialStore())
            .BuildServiceProvider();

        Assert.IsType<ClassroomSyncBlobCodec>(provider.GetRequiredService<IClassroomSyncBlobCodec>());
    }

    private static ClassroomSyncSnapshot CreateSnapshot() =>
        new(
            ProfileId,
            "host-1",
            Now,
            [new StudentReadingProgress("host-1", "book-1", 12, 18.5, Now)],
            [new StudentAnnotation(
                "annotation-1",
                "host-1",
                "book-1",
                7,
                "Highlight",
                "#ffd166",
                "Important idea",
                Now,
                Now,
                IsDeleted: true)],
            [new StudentBookmark("bookmark-1", "host-1", "book-1", 9, "Exam quote", Now, Now)],
            [new StudentAiHistoryEntry(
                "ai-1",
                "host-1",
                "What does this chapter argue?",
                "The chapter argues for local-first reading.",
                "student",
                Now)],
            new StudentSyncState("host-1", Now, "abc123", 2));

    private static void AssertSnapshotEqual(
        ClassroomSyncSnapshot expected,
        ClassroomSyncSnapshot actual)
    {
        Assert.Equal(expected.ProfileId, actual.ProfileId);
        Assert.Equal(expected.HostId, actual.HostId);
        Assert.Equal(expected.ExportedUtc, actual.ExportedUtc);
        Assert.Equal(expected.ReadingProgress, actual.ReadingProgress);
        Assert.Equal(expected.Annotations, actual.Annotations);
        Assert.Equal(expected.Bookmarks, actual.Bookmarks);
        Assert.Equal(expected.AiHistory, actual.AiHistory);
        Assert.Equal(expected.SyncState, actual.SyncState);
    }
}
