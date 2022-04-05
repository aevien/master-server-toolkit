namespace MasterServerToolkit.MasterServer
{
    public enum MstOpCodes
    {
        // Standard error code
        Error = 31000,

        // Ping request code
        Ping,

        // Security
        AesKeyRequest,
        PermissionLevelRequest,
        PeerGuidRequest,

        // Rooms
        RegisterRoomRequest,
        DestroyRoomRequest,
        SaveRoomOptionsRequest,
        GetRoomAccessRequest,
        ProvideRoomAccessCheck,
        ValidateRoomAccessRequest,
        PlayerLeftRoomRequest,

        // Spawner
        RegisterSpawner,
        SpawnProcessRequest,
        ClientsSpawnRequest,
        SpawnRequestStatusChange,
        RegisterSpawnedProcess,
        CompleteSpawnProcess,
        KillProcessRequest,
        ProcessStarted,
        ProcessKilled,
        AbortSpawnRequest,
        GetSpawnFinalizationData,
        UpdateSpawnerProcessesCount,

        // Matchmaker
        GetGameRequest,
        FindGamesRequest,
        GetRegionsRequest,

        // Auth
        SignIn,
        SignUp,
        SignOut,
        GetPasswordResetCode,
        GetEmailConfirmationCode,
        ConfirmEmail,
        GetLoggedInUsersCount,
        ChangePassword,
        GetPeerAccountInfo,
        UpdateAccountInfo,

        // Chat
        PickUsername,
        JoinChannel,
        LeaveChannel,
        GetCurrentChannels,
        ChatMessage,
        GetUsersInChannel,
        UserJoinedChannel,
        UserLeftChannel,
        SetDefaultChannel,

        // TODO cleanup
        // Lobbies
        JoinLobby,
        LeaveLobby,
        CreateLobby,
        LobbyInfo,
        SetLobbyProperties,
        SetMyProperties,
        SetLobbyAsReady,
        StartLobbyGame,
        LobbyChatMessage,
        SendMessageToLobbyChat,
        JoinLobbyTeam,
        LobbyGameAccessRequest,
        LobbyIsInLobby,
        LobbyMasterChange,
        LobbyStateChange,
        LobbyStatusTextChange,
        LobbyMemberPropertySet,
        LeftLobby,
        LobbyPropertyChanged,
        LobbyMemberJoined,
        LobbyMemberLeft,
        LobbyMemberChangedTeam,
        LobbyMemberReadyStatusChange,
        LobbyMemberPropertyChanged,
        GetLobbyRoomAccess,
        GetLobbyMemberData,
        GetLobbyInfo,

        // Profiles
        ClientProfileRequest,
        ServerProfileRequest,
        UpdateServerProfile,
        UpdateClientProfile,
        UpdateDisplayNameRequest,

        // Notifications
        SubscribeToNotifications,
        UnsubscribeFromNotifications,
        Notification,

        // Friends
        FriendAdded,
        GetFriends,
        RemoveFriends,
        InspectFriend,
        BlockFriends,
        RequestFriendship,
        AcceptFriendship,
        IgnoreFriendship,
        GetDeclinedFriendships,
        DeclineFriendship,

        // Guilds

    }
}