namespace MasterServerToolkit.MasterServer
{
    public enum MstMessageCodes
    {
        // Standard error code
        Error = 31000,

        // Ping request code
        Ping,

        MstStart,

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
        FindGamesRequest,

        // Auth
        SignIn,
        SignUp,
        SignOutRequest,
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
        UpdateDisplayNameRequest
    }
}