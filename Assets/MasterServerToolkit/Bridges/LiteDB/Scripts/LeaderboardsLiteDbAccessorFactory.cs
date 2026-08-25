using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public sealed class LeaderboardsLiteDbAccessorFactory : LiteDatabaseAccessorFactory
    {
        private const string DefaultDatabaseName = "leaderboards";

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private LeaderboardsDatabaseAccessor leaderboardsAccessor;
#endif

        protected override void Awake()
        {
            databaseName = NormalizeDatabaseName(databaseName);
            base.Awake();
        }

        protected override void OnValidate()
        {
            databaseName = NormalizeDatabaseName(databaseName);
        }

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            leaderboardsAccessor?.Dispose();
            leaderboardsAccessor = null;
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            if (leaderboardsAccessor != null)
            {
                ILeaderboardsDatabaseAccessor registeredAccessor =
                    Mst.Server.DbAccessors.GetAccessor<ILeaderboardsDatabaseAccessor>();

                if (registeredAccessor == null)
                    Mst.Server.DbAccessors.AddAccessor(leaderboardsAccessor);
                else if (!ReferenceEquals(registeredAccessor, leaderboardsAccessor))
                    logger.Warn("A different leaderboards database accessor is already registered; LiteDB registration was skipped");

                return;
            }

            if (Mst.Server.DbAccessors.GetAccessor<ILeaderboardsDatabaseAccessor>() != null)
            {
                logger.Warn("A leaderboards database accessor is already registered");
                return;
            }

            try
            {
                leaderboardsAccessor = new LeaderboardsDatabaseAccessor(databaseName)
                {
                    Logger = logger
                };

                Mst.Server.DbAccessors.AddAccessor(leaderboardsAccessor);
            }
            catch (Exception exception)
            {
                leaderboardsAccessor?.Dispose();
                leaderboardsAccessor = null;
                logger.Error($"Failed to setup {nameof(LeaderboardsDatabaseAccessor)}");
                logger.Error(exception);
            }
#endif
        }

        private static string NormalizeDatabaseName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultDatabaseName
                : value.Trim();
        }
    }
}
