using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    /// <summary>
    /// Creates and owns the SqlSugar accessor used by <see cref="LeaderboardsModule"/>.
    /// </summary>
    public sealed class LeaderboardsDatabaseAccessorFactory : SqlSugarDatabaseAccessorFactory
    {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private LeaderboardsDatabaseAccessor accessor;
#endif

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            accessor?.Dispose();
            accessor = null;
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            ILeaderboardsDatabaseAccessor registered =
                Mst.Server.DbAccessors.GetAccessor<ILeaderboardsDatabaseAccessor>();

            if (accessor != null)
            {
                if (registered == null)
                    Mst.Server.DbAccessors.AddAccessor(accessor);
                else if (!ReferenceEquals(registered, accessor))
                    logger.Warn("A different leaderboards database accessor is already registered; SqlSugar registration was skipped");

                return;
            }

            if (registered != null)
            {
                logger.Warn("A leaderboards database accessor is already registered; SqlSugar registration was skipped");
                return;
            }

            try
            {
                accessor = new LeaderboardsDatabaseAccessor(configuration)
                {
                    Logger = logger
                };

                Mst.Server.DbAccessors.AddAccessor(accessor);
            }
            catch (Exception exception)
            {
                accessor?.Dispose();
                accessor = null;
                logger.Error($"Failed to setup {GetType().Name}");
                logger.Error(exception);
            }
#endif
        }
    }
}
