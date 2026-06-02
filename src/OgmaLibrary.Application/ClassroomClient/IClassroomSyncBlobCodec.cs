namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Serializes and encrypts opt-in private classroom sync snapshots.</summary>
public interface IClassroomSyncBlobCodec
{
    /// <summary>Serializes, compresses, and encrypts a private-state snapshot.</summary>
    EncryptedClassroomSyncBlob Encode(
        ClassroomSyncSnapshot snapshot,
        string sessionToken);

    /// <summary>Decrypts, decompresses, and deserializes a private-state snapshot.</summary>
    ClassroomSyncSnapshot Decode(
        EncryptedClassroomSyncBlob blob,
        string sessionToken);
}
