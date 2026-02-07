namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by host after all initial chunks have been streamed to a new client.
    /// No payload — presence of this message signals sync completion.
    /// </summary>
    public struct SyncCompleteMessage
    {
        public byte[] Serialize()
        {
            return System.Array.Empty<byte>();
        }

        public static SyncCompleteMessage Deserialize(byte[] data)
        {
            return new SyncCompleteMessage();
        }
    }
}
