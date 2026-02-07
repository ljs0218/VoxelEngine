using System.Collections.Generic;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Central registry for ISyncableComponent instances.
    /// Used by the network layer to discover and sync components.
    /// </summary>
    public static class SyncRegistry
    {
        private static readonly Dictionary<byte, ISyncableComponent> components = new Dictionary<byte, ISyncableComponent>();

        /// <summary>
        /// Registers a syncable component. Overwrites existing registration with same SyncId.
        /// </summary>
        public static void Register(ISyncableComponent component)
        {
            if (component == null) return;
            components[component.SyncId] = component;
        }

        /// <summary>
        /// Unregisters a syncable component by its SyncId.
        /// </summary>
        public static void Unregister(byte syncId)
        {
            components.Remove(syncId);
        }

        /// <summary>
        /// Returns a component by its SyncId, or null if not registered.
        /// </summary>
        public static ISyncableComponent GetById(byte syncId)
        {
            components.TryGetValue(syncId, out var component);
            return component;
        }

        /// <summary>
        /// Returns all components that have changed since last sync.
        /// </summary>
        public static List<ISyncableComponent> GetDirtyComponents()
        {
            var dirty = new List<ISyncableComponent>();
            foreach (var kvp in components)
            {
                if (kvp.Value.IsDirty)
                {
                    dirty.Add(kvp.Value);
                }
            }
            return dirty;
        }

        /// <summary>
        /// Clears all registrations. Call on session end.
        /// </summary>
        public static void Clear()
        {
            components.Clear();
        }
    }
}
