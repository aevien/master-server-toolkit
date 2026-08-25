using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This package represents current state of the lobby
    /// </summary>
    public class LobbyDataPacket : SerializablePacket
    {
        public int LobbyId { get; set; }
        public LobbyState LobbyState { get; set; }
        public string LobbyType { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string GameMaster { get; set; } = string.Empty;
        public string CurrentUserUsername { get; set; } = string.Empty;
        public string LobbyName { get; set; } = string.Empty;
        public int MaxPlayers { get; set; }
        public Dictionary<string, string> LobbyProperties { get; set; }
        public Dictionary<string, LobbyMemberData> Members { get; set; }
        public Dictionary<string, LobbyTeamData> Teams { get; set; }
        public List<LobbyPropertyData> Controls { get; set; }
        public byte[] AdditionalData { get; set; }

        // Settings
        public bool EnableManualStart { get; set; }
        public bool EnableReadySystem { get; set; }
        public bool EnableTeamSwitching { get; set; }


        public LobbyDataPacket()
        {
            // Just to avoid handling "null" cases
            Members = new Dictionary<string, LobbyMemberData>();
            Teams = new Dictionary<string, LobbyTeamData>();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write((int)LobbyState);
            writer.Write(LobbyType);
            writer.Write(StatusText);
            writer.Write(GameMaster);
            writer.Write(CurrentUserUsername);

            writer.Write(LobbyId);
            writer.Write(LobbyName);
            writer.WriteDictionary(LobbyProperties);
            writer.Write(MaxPlayers);

            // Write additional data
            writer.Write(AdditionalData == null ? 0 : AdditionalData.Length);
            if (AdditionalData != null)
            {
                writer.Write(AdditionalData);
            }

            // Write player properties
            writer.WriteCount32(
                Members.Count,
                MstNetworkLimits.MaxCollectionEntryCount,
                "Lobby member list");
            foreach (var playerProperty in Members)
            {
                writer.Write(playerProperty.Key);

                // Write the member info
                playerProperty.Value.ToBinaryWriter(writer);
            }

            // Write teams info
            writer.WriteCount32(
                Teams.Count,
                MstNetworkLimits.MaxCollectionEntryCount,
                "Lobby team list");
            foreach (var team in Teams)
            {
                writer.Write(team.Key);

                // Write team data
                team.Value.ToBinaryWriter(writer);
            }

            // Write controls
            writer.WriteCount32(
                Controls.Count,
                MstNetworkLimits.MaxCollectionEntryCount,
                "Lobby control list");
            foreach (var control in Controls)
            {
                control.ToBinaryWriter(writer);
            }

            // Other settings
            writer.Write(EnableManualStart);
            writer.Write(EnableReadySystem);
            writer.Write(EnableTeamSwitching);

        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            LobbyState = (LobbyState)reader.ReadInt32();
            LobbyType = reader.ReadString();
            StatusText = reader.ReadString();
            GameMaster = reader.ReadString();
            CurrentUserUsername = reader.ReadString();

            LobbyId = reader.ReadInt32();
            LobbyName = reader.ReadString();
            LobbyProperties = reader.ReadDictionary();
            MaxPlayers = reader.ReadInt32();

            // Read additional data
            var size = reader.ReadLength32(
                MstNetworkLimits.MaxMessagePayloadByteCount,
                "Lobby additional data");
            if (size > 0)
            {
                AdditionalData = reader.ReadBytesExact(
                    size,
                    MstNetworkLimits.MaxMessagePayloadByteCount);
            }

            // Clear, in case we're reusing the object
            Members.Clear();

            // Read player properties
            var playerCount = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "Lobby member list");

            for (var i = 0; i < playerCount; i++)
            {
                var data = CreateLobbyMemberData();
                var username = reader.ReadString();
                data.FromBinaryReader(reader);

                Members.Add(username, data);
            }

            // Read teams
            Teams.Clear();
            var teamsCount = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "Lobby team list");
            for (int i = 0; i < teamsCount; i++)
            {
                var teamKey = reader.ReadString();
                var teamData = CreateTeamData();
                teamData.FromBinaryReader(reader);
                Teams.Add(teamKey, teamData);
            }

            // Read controls
            Controls = new List<LobbyPropertyData>();
            var controlsCount = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "Lobby control list");
            for (int i = 0; i < controlsCount; i++)
            {
                var control = new LobbyPropertyData();
                control.FromBinaryReader(reader);
                Controls.Add(control);
            }

            // Other settings
            EnableManualStart = reader.ReadBoolean();
            EnableReadySystem = reader.ReadBoolean();
            EnableTeamSwitching = reader.ReadBoolean();
        }

        protected virtual LobbyMemberData CreateLobbyMemberData()
        {
            return new LobbyMemberData();
        }

        protected virtual LobbyTeamData CreateTeamData()
        {
            return new LobbyTeamData();
        }
    }
}
