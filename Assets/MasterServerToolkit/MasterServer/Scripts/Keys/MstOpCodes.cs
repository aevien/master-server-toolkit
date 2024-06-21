using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.MasterServer
{
    public struct MstOpCodes
    {
        public static ushort Error = "mst.error".ToUint16Hash();

        public static ushort Ping = nameof(Ping).ToUint16Hash();

        public static ushort ServerAccessRequest = nameof(ServerAccessRequest).ToUint16Hash();
        public static ushort AesKeyRequest = nameof(AesKeyRequest).ToUint16Hash();
        public static ushort PermissionLevelRequest = nameof(PermissionLevelRequest).ToUint16Hash();
        public static ushort PeerGuidRequest = nameof(PeerGuidRequest).ToUint16Hash();

        public static ushort RegisterRoomRequest = nameof(RegisterRoomRequest).ToUint16Hash();
        public static ushort DestroyRoomRequest = nameof(DestroyRoomRequest).ToUint16Hash();
        public static ushort SaveRoomOptionsRequest = nameof(SaveRoomOptionsRequest).ToUint16Hash();
        public static ushort GetRoomAccessRequest = nameof(GetRoomAccessRequest).ToUint16Hash();
        public static ushort ProvideRoomAccessCheck = nameof(ProvideRoomAccessCheck).ToUint16Hash();
        public static ushort ValidateRoomAccessRequest = nameof(ValidateRoomAccessRequest).ToUint16Hash();
        public static ushort PlayerLeftRoomRequest = nameof(PlayerLeftRoomRequest).ToUint16Hash();

        public static ushort RegisterSpawner = nameof(RegisterSpawner).ToUint16Hash();
        public static ushort SpawnProcessRequest = nameof(SpawnProcessRequest).ToUint16Hash();
        public static ushort ClientsSpawnRequest = nameof(ClientsSpawnRequest).ToUint16Hash();
        public static ushort SpawnRequestStatusChange = nameof(SpawnRequestStatusChange).ToUint16Hash();
        public static ushort RegisterSpawnedProcess = nameof(RegisterSpawnedProcess).ToUint16Hash();
        public static ushort CompleteSpawnProcess = nameof(CompleteSpawnProcess).ToUint16Hash();
        public static ushort KillProcessRequest = nameof(KillProcessRequest).ToUint16Hash();
        public static ushort ProcessStarted = nameof(ProcessStarted).ToUint16Hash();
        public static ushort ProcessKilled = nameof(ProcessKilled).ToUint16Hash();
        public static ushort AbortSpawnRequest = nameof(AbortSpawnRequest).ToUint16Hash();
        public static ushort GetSpawnFinalizationData = nameof(GetSpawnFinalizationData).ToUint16Hash();
        public static ushort UpdateSpawnerProcessesCount = nameof(UpdateSpawnerProcessesCount).ToUint16Hash();

        public static ushort GetGameRequest = nameof(GetGameRequest).ToUint16Hash();
        public static ushort FindGamesRequest = nameof(FindGamesRequest).ToUint16Hash();
        public static ushort GetRegionsRequest = nameof(GetRegionsRequest).ToUint16Hash();

        public static ushort SignIn = nameof(SignIn).ToUint16Hash();
        public static ushort SignUp = nameof(SignUp).ToUint16Hash();
        public static ushort SignOut = nameof(SignOut).ToUint16Hash();
        public static ushort GetPasswordResetCode = nameof(GetPasswordResetCode).ToUint16Hash();
        public static ushort GetEmailConfirmationCode = nameof(GetEmailConfirmationCode).ToUint16Hash();
        public static ushort ConfirmEmail = nameof(ConfirmEmail).ToUint16Hash();
        public static ushort GetLoggedInUsersCount = nameof(GetLoggedInUsersCount).ToUint16Hash();
        public static ushort ChangePassword = nameof(ChangePassword).ToUint16Hash();
        public static ushort GetAccountInfoByPeer = nameof(GetAccountInfoByPeer).ToUint16Hash();
        public static ushort GetAccountInfoByUsername = nameof(GetAccountInfoByUsername).ToUint16Hash();
        public static ushort BindExtraProperties = nameof(BindExtraProperties).ToUint16Hash();

        public static ushort PickUsername = nameof(PickUsername).ToUint16Hash();
        public static ushort JoinChannel = nameof(JoinChannel).ToUint16Hash();
        public static ushort LeaveChannel = nameof(LeaveChannel).ToUint16Hash();
        public static ushort GetCurrentChannels = nameof(GetCurrentChannels).ToUint16Hash();
        public static ushort ChatMessage = nameof(ChatMessage).ToUint16Hash();
        public static ushort GetUsersInChannel = nameof(GetUsersInChannel).ToUint16Hash();
        public static ushort UserJoinedChannel = nameof(UserJoinedChannel).ToUint16Hash();
        public static ushort UserLeftChannel = nameof(UserLeftChannel).ToUint16Hash();
        public static ushort SetDefaultChannel = nameof(SetDefaultChannel).ToUint16Hash();

        public static ushort JoinLobby = nameof(JoinLobby).ToUint16Hash();
        public static ushort LeaveLobby = nameof(LeaveLobby).ToUint16Hash();
        public static ushort CreateLobby = nameof(CreateLobby).ToUint16Hash();
        public static ushort LobbyInfo = nameof(LobbyInfo).ToUint16Hash();
        public static ushort SetLobbyProperties = nameof(SetLobbyProperties).ToUint16Hash();
        public static ushort SetMyProperties = nameof(SetMyProperties).ToUint16Hash();
        public static ushort SetLobbyAsReady = nameof(SetLobbyAsReady).ToUint16Hash();
        public static ushort StartLobbyGame = nameof(StartLobbyGame).ToUint16Hash();
        public static ushort LobbyChatMessage = nameof(LobbyChatMessage).ToUint16Hash();
        public static ushort SendMessageToLobbyChat = nameof(SendMessageToLobbyChat).ToUint16Hash();
        public static ushort JoinLobbyTeam = nameof(JoinLobbyTeam).ToUint16Hash();
        public static ushort LobbyGameAccessRequest = nameof(LobbyGameAccessRequest).ToUint16Hash();
        public static ushort LobbyIsInLobby = nameof(LobbyIsInLobby).ToUint16Hash();
        public static ushort LobbyMasterChange = nameof(LobbyMasterChange).ToUint16Hash();
        public static ushort LobbyStateChange = nameof(LobbyStateChange).ToUint16Hash();
        public static ushort LobbyStatusTextChange = nameof(LobbyStatusTextChange).ToUint16Hash();
        public static ushort LobbyMemberPropertySet = nameof(LobbyMemberPropertySet).ToUint16Hash();
        public static ushort LeftLobby = nameof(LeftLobby).ToUint16Hash();
        public static ushort LobbyPropertyChanged = nameof(LobbyPropertyChanged).ToUint16Hash();
        public static ushort LobbyMemberJoined = nameof(LobbyMemberJoined).ToUint16Hash();
        public static ushort LobbyMemberLeft = nameof(LobbyMemberLeft).ToUint16Hash();
        public static ushort LobbyMemberChangedTeam = nameof(LobbyMemberChangedTeam).ToUint16Hash();
        public static ushort LobbyMemberReadyStatusChange = nameof(LobbyMemberReadyStatusChange).ToUint16Hash();
        public static ushort LobbyMemberPropertyChanged = nameof(LobbyMemberPropertyChanged).ToUint16Hash();
        public static ushort GetLobbyRoomAccess = nameof(GetLobbyRoomAccess).ToUint16Hash();
        public static ushort GetLobbyMemberData = nameof(GetLobbyMemberData).ToUint16Hash();
        public static ushort GetLobbyInfo = nameof(GetLobbyInfo).ToUint16Hash();

        public static ushort GetZoneRoomInfo = nameof(GetZoneRoomInfo).ToUint16Hash();
        public static ushort GetZoneRoomAccess = nameof(GetZoneRoomAccess).ToUint16Hash();

        public static ushort ClientFillInProfileValues = nameof(ClientFillInProfileValues).ToUint16Hash();
        public static ushort ServerFillInProfileValues = nameof(ServerFillInProfileValues).ToUint16Hash();
        public static ushort ServerUpdateProfileValues = nameof(ServerUpdateProfileValues).ToUint16Hash();
        public static ushort UpdateClientProfile = nameof(UpdateClientProfile).ToUint16Hash();
        public static ushort UpdateDisplayNameRequest = nameof(UpdateDisplayNameRequest).ToUint16Hash();
        public static ushort UpdateAvatarRequest = nameof(UpdateAvatarRequest).ToUint16Hash();

        public static ushort SubscribeToNotifications = nameof(SubscribeToNotifications).ToUint16Hash();
        public static ushort UnsubscribeFromNotifications = nameof(UnsubscribeFromNotifications).ToUint16Hash();
        public static ushort Notification = nameof(Notification).ToUint16Hash();

        public static ushort PlayerDied = nameof(PlayerDied).ToUint16Hash();

        public static ushort JoinDashboard = nameof(JoinDashboard).ToUint16Hash();
        public static ushort SystemInfo = nameof(SystemInfo).ToUint16Hash();
        public static ushort ServerInfo = nameof(ServerInfo).ToUint16Hash();
        public static ushort ModulesInfo = nameof(ModulesInfo).ToUint16Hash();

        public static ushort ClientUpdateAchievementProgress = nameof(ClientUpdateAchievementProgress).ToUint16Hash();
        public static ushort ClientCheckAchievementProgress = nameof(ClientCheckAchievementProgress).ToUint16Hash();
        public static ushort ClientAchievementProgressIsMet = nameof(ClientAchievementProgressIsMet).ToUint16Hash();
        public static ushort ServerUpdateAchievementProgress = nameof(ServerUpdateAchievementProgress).ToUint16Hash();
    }
}