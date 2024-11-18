using MasterServerToolkit.DebounceThrottle;
using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        protected float saveDebounceTime = 5f;

        /// <summary>
        /// Database accessor factory that helps to create integration with accounts db
        /// </summary>
        [Tooltip("Database accessor factory that helps to create integration with accounts db"), SerializeField]
        protected DatabaseAccessorFactory databaseAccessorFactory;

        #endregion

        protected readonly List<AnalyticsDataInfoPacket> eventsToSave = new List<AnalyticsDataInfoPacket>();

        protected ThrottleDispatcher saveThrottleDispatcher;

        /// <summary>
        /// 
        /// </summary>
        protected IAnalyticsDatabaseAccessor databaseAccessor;

        /// <summary>
        /// 
        /// </summary>
        public IAnalyticsDatabaseAccessor DatabaseAccessor
        {
            get => databaseAccessor;
            set => databaseAccessor = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public DatabaseAccessorFactory DatabaseAccessorFactory
        {
            get => databaseAccessorFactory;
            set => databaseAccessorFactory = value;
        }

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler(MstOpCodes.SendAnalyticsData, OuSendAnalyticsDataMessageHandler);

            if (databaseAccessorFactory != null)
                databaseAccessorFactory.CreateAccessors();

            databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IAnalyticsDatabaseAccessor>();

            saveThrottleDispatcher = new ThrottleDispatcher((int)(saveDebounceTime * 1000f));
        }

        /// <summary>
        /// Saves event to database
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="userId"></param>
        /// <param name="eventData"></param>
        public void Save(string eventId, string userId, Dictionary<string, string> eventData)
        {
            Save(new AnalyticsDataInfoPacket()
            {
                EventId = eventId,
                UserId = userId,
                Data = eventData
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsData>> GetAll(int size, int page)
        {
            return await databaseAccessor.GetAll();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<IAnalyticsData> GetById(string eventId)
        {
            return await databaseAccessor.GetById(eventId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsData>> GetByUserId(string userId, int size, int page)
        {
            return await databaseAccessor.GetByUserId(userId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsData>> GetWithQuery(string query, int size, int page)
        {
            return await databaseAccessor.GetWithQuery(query);
        }

        /// <summary>
        /// Saves event to database
        /// </summary>
        /// <param name="eventinfo"></param>
        public void Save(AnalyticsDataInfoPacket eventinfo)
        {
            lock (eventsToSave)
            {
                eventsToSave.Add(eventinfo);
            }

            saveThrottleDispatcher.ThrottleAsync(async() =>
            {
                var snapshot = new List<AnalyticsDataInfoPacket>();

                lock (eventsToSave)
                {
                    snapshot.AddRange(eventsToSave);
                    eventsToSave.Clear();
                }

                if (snapshot.Count > 0)
                {
                    await databaseAccessor.Insert(snapshot);
                }
            });
        }

        #region MESSAGE HANDLERS

        private Task OuSendAnalyticsDataMessageHandler(IIncomingMessage message)
        {
            var data = message.AsPacket<AnalyticsDataInfoPacket>();
            Save(data);
            message.Respond(ResponseStatus.Success);
            return Task.CompletedTask;
        }

        #endregion
    }
}
