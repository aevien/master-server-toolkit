using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This is an instance of the room in master server
    /// </summary>
    public class RegisteredRoom
    {
        public delegate void GetAccessCallback(RoomAccessPacket access, string error);

        /// <summary>
        /// List of used accesses
        /// </summary>
        private readonly Dictionary<int, RoomAccessPacket> accessesInUse;

        /// <summary>
        /// List of unconfirmed access
        /// </summary>
        private readonly Dictionary<string, RoomAccessData> unconfirmedAccesses;

        /// <summary>
        /// The number of requests that are waiting for access token from room the player wants to be connected
        /// </summary>
        private HashSet<int> requestsInProgress;

        /// <summary>
        /// Options this room has
        /// </summary>
        public RoomOptions Options { get; private set; }

        /// <summary>
        /// Current room ID
        /// </summary>
        public int RoomId { get; private set; }

        /// <summary>
        /// Peer of this room owner
        /// </summary>
        public IPeer Peer { get; private set; }

        /// <summary>
        /// Connected users list
        /// </summary>
        public Dictionary<int, IPeer> Players { get; protected set; }

        /// <summary>
        /// Number of the connected users
        /// </summary>
        public int OnlineCount { get { return accessesInUse.Count; } }

        /// <summary>
        /// Fires when player joined room 
        /// </summary>
        public event Action<IPeer> OnPlayerJoinedEvent;

        /// <summary>
        /// Fireswhen player left room
        /// </summary>
        public event Action<IPeer> OnPlayerLeftEvent;

        /// <summary>
        /// Fires when room destroyed
        /// </summary>
        public event Action<RegisteredRoom> OnDestroyedEvent;

        public RegisteredRoom(int roomId, IPeer peer, RoomOptions options)
        {
            RoomId = roomId;
            Peer = peer;
            Options = options;

            requestsInProgress = new HashSet<int>();
            unconfirmedAccesses = new Dictionary<string, RoomAccessData>();
            accessesInUse = new Dictionary<int, RoomAccessPacket>();

            Players = new Dictionary<int, IPeer>();
        }

        /// <summary>
        /// Replace options of the room with another
        /// </summary>
        /// <param name="options"></param>
        public void ChangeOptions(RoomOptions options)
        {
            Options = options;
        }

        /// <summary>
        /// Sends a request to room, to retrieve an access to it for a specified peer
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="callback"></param>
        public void GetAccess(IPeer peer, GetAccessCallback callback)
        {
            GetAccess(peer, new MstProperties(), callback);
        }

        /// <summary>
        /// Sends a request to room, to retrieve an access to it for a specified peer, 
        /// with some extra options
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="customOptions"></param>
        /// <param name="callback"></param>
        public void GetAccess(IPeer peer, MstProperties customOptions, GetAccessCallback callback)
        {
            // If request is already pending
            if (requestsInProgress.Contains(peer.Id))
            {
                callback.Invoke(null, "You've already requested an access to this room");
                return;
            }

            // If player is already in the game
            if (Players.ContainsKey(peer.Id))
            {
                callback.Invoke(null, "You are already in this room");
                return;
            }

            // If player has already received an access and didn't claim it
            // but is requesting again - send him the old one
            var currentAccess = unconfirmedAccesses.Values.FirstOrDefault(v => v.Peer == peer);

            if (currentAccess != null)
            {
                // Restore the timeout
                currentAccess.Timeout = DateTime.Now.AddSeconds(Options.AccessTimeoutPeriod);

                callback.Invoke(currentAccess.Access, null);
                return;
            }

            // If there's a player limit
            if (Options.MaxConnections > 0)
            {
                var playerSlotsTaken = requestsInProgress.Count + accessesInUse.Count + unconfirmedAccesses.Count;

                if (playerSlotsTaken >= Options.MaxConnections)
                {
                    callback.Invoke(null, "Room is already full");
                    return;
                }
            }

            // Create packet to request checking of access
            var provideRoomAccessCheckPacket = new ProvideRoomAccessCheckPacket()
            {
                PeerId = peer.Id,
                RoomId = RoomId,
                CustomOptions = customOptions
            };

            // Try to find out if requester is logged in add the username if available
            // Simetimes we want to check if user is banned
            var userPeerExtension = peer.GetExtension<IUserPeerExtension>();

            if (userPeerExtension != null && !string.IsNullOrEmpty(userPeerExtension.Username))
            {
                provideRoomAccessCheckPacket.Username = userPeerExtension.Username;
            }

            // Add requester peer id to pending list to prevent new access request to this room
            requestsInProgress.Add(peer.Id);

            // Send request to owner of the room to get access token
            Peer.SendMessage(MstOpCodes.ProvideRoomAccessCheck, provideRoomAccessCheckPacket, (status, response) =>
            {
                // Remove requester peer id from pending list
                requestsInProgress.Remove(peer.Id);

                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                // Parse access data from message
                var accessData = response.AsPacket<RoomAccessPacket>();

                // Create new access info
                var access = new RoomAccessData()
                {
                    Access = accessData,
                    Peer = peer,
                    Timeout = DateTime.Now.AddSeconds(Options.AccessTimeoutPeriod)
                };

                // Save the access info to list and wait for confirmation
                unconfirmedAccesses[access.Access.Token] = access;

                callback?.Invoke(access.Access, null);
            });
        }

        /// <summary>
        /// Checks if access token is valid
        /// </summary>
        /// <param name="token"></param>
        /// <param name="peer"></param>
        /// <returns></returns>
        public bool ValidateAccess(string token, out IPeer peer)
        {
            unconfirmedAccesses.TryGetValue(token, out RoomAccessData data);

            peer = null;

            // If there's no data
            if (data == null)
            {
                return false;
            }

            // Remove unconfirmed
            unconfirmedAccesses.Remove(token);

            // If player is no longer connected
            if (!data.Peer.IsConnected)
            {
                return false;
            }

            // Set access as used
            accessesInUse.Add(data.Peer.Id, data.Access);

            peer = data.Peer;
            peer.GetExtension<IUserPeerExtension>().JoinedRoomID = RoomId;

            // Save player in room
            Players[peer.Id] = peer;

            // Invoke the event
            OnPlayerJoinedEvent?.Invoke(peer);

            return true;
        }

        /// <summary>
        /// Removes player from room by his peer id
        /// </summary>
        /// <param name="peerId"></param>
        public void RemovePlayer(int peerId)
        {
            accessesInUse.Remove(peerId);

            if (!Players.TryGetValue(peerId, out IPeer playerPeer))
                return;

            playerPeer.GetExtension<IUserPeerExtension>().JoinedRoomID = -1;
            Players.Remove(peerId);
            OnPlayerLeftEvent?.Invoke(playerPeer);
        }

        /// <summary>
        /// Clears all of the accesses that have not been confirmed in time
        /// </summary>
        public void ClearTimedOutAccesses()
        {
            var timedOut = unconfirmedAccesses.Values.Where(u => u.Timeout < DateTime.Now).ToList();

            foreach (var access in timedOut)
            {
                unconfirmedAccesses.Remove(access.Access.Token);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Destroy()
        {
            OnDestroyedEvent?.Invoke(this);

            unconfirmedAccesses.Clear();

            // Clear listeners
            OnPlayerJoinedEvent = null;
            OnPlayerLeftEvent = null;
            OnDestroyedEvent = null;
        }
    }
}