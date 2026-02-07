namespace VoxelEngine.Networking
{
    /// <summary>
    /// Interface for components that need to sync state across the network.
    /// Implement this on any MonoBehaviour that has multiplayer-relevant state.
    /// The network layer will call SerializeState/DeserializeState for dirty components.
    /// </summary>
    public interface ISyncableComponent
    {
        /// <summary>Unique identifier for this syncable component type (0-255).</summary>
        byte SyncId { get; }

        /// <summary>Serializes the current state to a byte array for network transfer.</summary>
        byte[] SerializeState();

        /// <summary>Applies received state from the network.</summary>
        void DeserializeState(byte[] data);

        /// <summary>Whether the state has changed since last sync.</summary>
        bool IsDirty { get; }

        /// <summary>Clears the dirty flag after state has been sent.</summary>
        void ClearDirty();
    }
}
