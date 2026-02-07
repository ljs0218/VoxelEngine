using Steamworks;
using UnityEngine;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Manages SteamClient lifecycle. Singleton, DontDestroyOnLoad.
    /// Must be the first Steam-related component to initialize.
    /// </summary>
    public class SteamManager : MonoBehaviour
    {
        private static SteamManager instance;
        public static SteamManager Instance => instance;

        /// <summary>Whether Steam is successfully initialized.</summary>
        public bool Initialized { get; private set; }

        /// <summary>Current user's Steam display name.</summary>
        public string PlayerName => Initialized ? SteamClient.Name : "Unknown";

        /// <summary>Current user's SteamId.</summary>
        public SteamId MySteamId => SteamClient.SteamId;

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                SteamClient.Init(480, asyncCallbacks: false);
                Initialized = true;
                Debug.Log($"[SteamManager] Steam initialized. Player: {SteamClient.Name} ({SteamClient.SteamId})");

                // Initialize relay network access early for faster first connection
                SteamNetworkingUtils.InitRelayNetworkAccess();
            }
            catch (System.Exception e)
            {
                Initialized = false;
                Debug.LogError($"[SteamManager] Failed to initialize Steam: {e.Message}");
                enabled = false;
            }
        }

        private void Update()
        {
            if (Initialized)
            {
                SteamClient.RunCallbacks();
            }
        }

        private void OnApplicationQuit()
        {
            if (Initialized)
            {
                SteamClient.Shutdown();
                Initialized = false;
                Debug.Log("[SteamManager] Steam shut down.");
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
