using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public sealed class LeaderboardsDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        [Header("Components"), SerializeField]
        [Tooltip("MongoDB client factory that supplies the shared client connection and database name used by the leaderboards accessor. Assign the same client factory used by other MongoDB accessors on this server.")]
        private MongoDbClientFactory mongoDbClientFactory;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private LeaderboardsDatabaseAccessor leaderboardsAccessor;
#endif

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
            ILeaderboardsDatabaseAccessor registeredAccessor =
                Mst.Server.DbAccessors.GetAccessor<ILeaderboardsDatabaseAccessor>();

            if (ReferenceEquals(registeredAccessor, leaderboardsAccessor) &&
                leaderboardsAccessor != null)
            {
                return;
            }

            if (registeredAccessor != null)
            {
                logger.Warn("A leaderboards database accessor is already registered; MongoDB registration was skipped");
                return;
            }

            try
            {
                if (mongoDbClientFactory == null)
                    throw new InvalidOperationException(
                        $"{nameof(MongoDbClientFactory)} is not assigned");

                if (leaderboardsAccessor == null)
                {
                    leaderboardsAccessor = new LeaderboardsDatabaseAccessor(
                        mongoDbClientFactory.Client,
                        mongoDbClientFactory.Database)
                    {
                        Logger = logger
                    };
                }

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
    }
}
