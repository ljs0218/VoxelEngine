namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Identifies the type of a network message.
    /// First byte of every message envelope.
    /// </summary>
    public enum NetworkMessageType : byte
    {
        WorldMetadata = 0,
        PlayerJoinRequest = 1,
        PlayerJoinAccepted = 2,
        PlayerLeft = 3,
        SessionEnd = 4,

        ChunkData = 10,
        ChunkRequest = 11,

        BlockEdit = 20,
        BlockEditConfirm = 21,
        BlockEditReject = 22,

        PlayerPosition = 30,

        SyncComplete = 40,

        ComponentSync = 50
    }
}
