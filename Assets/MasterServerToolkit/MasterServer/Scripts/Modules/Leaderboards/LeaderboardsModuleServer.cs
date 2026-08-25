using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Trusted room/server API for submitting authoritative player scores.
    /// </summary>
    public class LeaderboardsModuleServer : MstBaseClient
    {
        /// <summary>
        /// Creates a trusted leaderboard API bound to the supplied room/server connection.
        /// </summary>
        /// <param name="connection">Connection used for authoritative score submissions.</param>
        public LeaderboardsModuleServer(IClientSocket connection) : base(connection)
        {
            RegisterErrors(
                MstErrorCodes.LEADERBOARD_ACCOUNT_REQUIRED,
                MstErrorCodes.LEADERBOARD_ACCOUNT_NOT_FOUND,
                MstErrorCodes.LEADERBOARD_DATABASE_UNAVAILABLE,
                MstErrorCodes.LEADERBOARD_GUEST_SUBMISSION_FORBIDDEN,
                MstErrorCodes.LEADERBOARD_KEY_REQUIRED,
                MstErrorCodes.LEADERBOARD_NOT_ACTIVE,
                MstErrorCodes.LEADERBOARD_NOT_FOUND,
                MstErrorCodes.LEADERBOARD_RESPONSE_INVALID,
                MstErrorCodes.LEADERBOARD_SCORE_OUT_OF_RANGE,
                MstErrorCodes.LEADERBOARD_SUBMISSION_FAILED,
                MstErrorCodes.PERMISSION_DENIED);
        }

        /// <summary>
        /// Submits an authoritative score through the current trusted room/server connection.
        /// </summary>
        /// <param name="accountId">Target MST account identifier.</param>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="score">Signed 64-bit score in the definition's fixed-point units.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="playerName">Current public player name, or empty to use the account username.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the active season.</param>
        /// <param name="playerAvatar">Current public HTTPS avatar URL, or empty to clear the stored avatar.</param>
        public void SubmitScore(
            string accountId,
            string key,
            long score,
            LeaderboardResultCallback<LeaderboardSubmitResultPacket> callback,
            string playerName = "",
            string seasonId = "",
            string playerAvatar = "")
        {
            SubmitScore(accountId, key, score, callback, playerName, seasonId, playerAvatar, Connection);
        }

        /// <summary>
        /// Submits an authoritative score through the specified trusted connection.
        /// </summary>
        /// <param name="accountId">Target MST account identifier.</param>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="score">Signed 64-bit score in the definition's fixed-point units.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="playerName">Current public player name, or empty to use the account username.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the active season.</param>
        /// <param name="playerAvatar">Current public HTTPS avatar URL, or empty to clear the stored avatar.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void SubmitScore(
            string accountId,
            string key,
            long score,
            LeaderboardResultCallback<LeaderboardSubmitResultPacket> callback,
            string playerName,
            string seasonId,
            string playerAvatar,
            IClientSocket connection)
        {
            int completionState = 0;

            void Complete(LeaderboardSubmitResultPacket result, string error)
            {
                if (Interlocked.Exchange(ref completionState, 1) != 0)
                    return;

                callback?.Invoke(result, error);
            }

            if (connection == null || !connection.IsConnected)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            var request = new LeaderboardSubmitScorePacket
            {
                AccountId = accountId,
                Key = key,
                Score = score,
                PlayerName = playerName,
                PlayerAvatar = playerAvatar,
                SeasonId = seasonId
            };

            try
            {
                connection.SendMessage(MstOpCodes.ServerSubmitLeaderboardScore,
                    request, (status, response) =>
                    {
                        if (status != ResponseStatus.Success)
                        {
                            Complete(null, Mst.Errors.Parse(status, response));
                            return;
                        }

                        if (response == null || !response.HasData)
                        {
                            Complete(null, Mst.Errors.Parse(
                                ResponseStatus.DependencyError,
                                CreateErrorProperties(MstErrorCodes.LEADERBOARD_RESPONSE_INVALID)));
                            return;
                        }

                        LeaderboardSubmitResultPacket result;

                        try
                        {
                            result = response.AsPacket<LeaderboardSubmitResultPacket>();
                        }
                        catch (Exception exception)
                        {
                            Logs.Error($"Failed to decode leaderboard submission response: {exception}");
                            Complete(null, Mst.Errors.Parse(
                                ResponseStatus.DependencyError,
                                CreateErrorProperties(MstErrorCodes.LEADERBOARD_RESPONSE_INVALID)));
                            return;
                        }

                        Complete(result, string.Empty);
                    });
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to submit leaderboard score: {exception}");
                Complete(null, Mst.Errors.Parse(
                    connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
            }
        }

        private static MstProperties CreateErrorProperties(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties;
        }
    }
}
