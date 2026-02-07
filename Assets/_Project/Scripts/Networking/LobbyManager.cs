using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Manages Steam Lobby creation, discovery, and membership.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        public Lobby? CurrentLobby { get; private set; }
        public bool IsInLobby => CurrentLobby.HasValue;
        public bool IsHost => CurrentLobby.HasValue && CurrentLobby.Value.Owner.Id == SteamClient.SteamId;

        /// <summary>Fired when a player joins the current lobby.</summary>
        public event Action<Friend> OnPlayerJoined;

        /// <summary>Fired when a player leaves the current lobby.</summary>
        public event Action<Friend> OnPlayerLeft;

        /// <summary>Fired when lobby creation succeeds.</summary>
        public event Action OnLobbyCreated;

        /// <summary>Fired when we successfully join a lobby.</summary>
        public event Action OnLobbyJoined;

        private void OnEnable()
        {
            SteamMatchmaking.OnLobbyMemberJoined += HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberDisconnected += HandleMemberDisconnected;
            SteamMatchmaking.OnLobbyMemberLeave += HandleMemberLeave;
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyMemberJoined -= HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberDisconnected -= HandleMemberDisconnected;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleMemberLeave;
        }

        /// <summary>
        /// Creates a new public lobby with the given max player count.
        /// </summary>
        public async Task<bool> CreateLobby(int maxPlayers = 4)
        {
            try
            {
                var result = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
                if (!result.HasValue)
                {
                    Debug.LogError("[LobbyManager] Failed to create lobby.");
                    return false;
                }

                CurrentLobby = result.Value;
                CurrentLobby.Value.SetPublic();
                CurrentLobby.Value.SetJoinable(true);
                CurrentLobby.Value.SetData("game_name", "VoxelEngine");
                CurrentLobby.Value.SetData("version", Application.version);

                Debug.Log($"[LobbyManager] Lobby created. ID: {CurrentLobby.Value.Id}");
                OnLobbyCreated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] CreateLobby exception: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Searches for available public lobbies.
        /// </summary>
        public async Task<Lobby[]> FindLobbies()
        {
            try
            {
                var list = await SteamMatchmaking.LobbyList
                    .WithMaxResults(20)
                    .WithKeyValue("game_name", "VoxelEngine")
                    .RequestAsync();

                if (list == null)
                {
                    Debug.Log("[LobbyManager] No lobbies found.");
                    return Array.Empty<Lobby>();
                }

                Debug.Log($"[LobbyManager] Found {list.Length} lobbies.");
                return list;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] FindLobbies exception: {e.Message}");
                return Array.Empty<Lobby>();
            }
        }

        /// <summary>
        /// Joins an existing lobby.
        /// </summary>
        public async Task<bool> JoinLobby(Lobby lobby)
        {
            try
            {
                var result = await lobby.Join();
                if (result != RoomEnter.Success)
                {
                    Debug.LogError($"[LobbyManager] Failed to join lobby: {result}");
                    return false;
                }

                CurrentLobby = lobby;
                Debug.Log($"[LobbyManager] Joined lobby. Host: {lobby.Owner.Name} ({lobby.Owner.Id})");
                OnLobbyJoined?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] JoinLobby exception: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Leaves the current lobby.
        /// </summary>
        public void LeaveLobby()
        {
            if (CurrentLobby.HasValue)
            {
                CurrentLobby.Value.Leave();
                Debug.Log("[LobbyManager] Left lobby.");
                CurrentLobby = null;
            }
        }

        private void HandleMemberJoined(Lobby lobby, Friend friend)
        {
            if (CurrentLobby.HasValue && lobby.Id == CurrentLobby.Value.Id)
            {
                Debug.Log($"[LobbyManager] Player joined: {friend.Name} ({friend.Id})");
                OnPlayerJoined?.Invoke(friend);
            }
        }

        private void HandleMemberDisconnected(Lobby lobby, Friend friend)
        {
            if (CurrentLobby.HasValue && lobby.Id == CurrentLobby.Value.Id)
            {
                Debug.Log($"[LobbyManager] Player disconnected: {friend.Name} ({friend.Id})");
                OnPlayerLeft?.Invoke(friend);
            }
        }

        private void HandleMemberLeave(Lobby lobby, Friend friend)
        {
            if (CurrentLobby.HasValue && lobby.Id == CurrentLobby.Value.Id)
            {
                Debug.Log($"[LobbyManager] Player left: {friend.Name} ({friend.Id})");
                OnPlayerLeft?.Invoke(friend);
            }
        }
    }
}
