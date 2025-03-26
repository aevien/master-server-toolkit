using MasterServerToolkit.DebounceThrottle;
using MasterServerToolkit.Networking;
using System;
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
        [SerializeField]
        protected bool useAnalytics = true;

        /// <summary>
        /// Database accessor factory that helps to create integration with analytics db
        /// </summary>
        [Tooltip("Database accessor factory that helps to create integration with analytics db"), SerializeField]
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

        protected override void Awake()
        {
            base.Awake();

            useAnalytics = Mst.Args.AsBool(Mst.Args.Names.UseAnalyticsModule, useAnalytics);
        }

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler(MstOpCodes.SendAnalyticsData, OuSendAnalyticsDataMessageHandler);

            if (databaseAccessorFactory != null)
                databaseAccessorFactory.CreateAccessors();

            databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IAnalyticsDatabaseAccessor>();
            saveThrottleDispatcher = new ThrottleDispatcher((int)(saveDebounceTime * 1000f));

            if (useAnalytics)
                InvokeRepeating(nameof(UpdateThrottle), 0.1f, 0.1f);
        }

        protected virtual void UpdateThrottle()
        {
            saveThrottleDispatcher.ThrottleAsync(async () =>
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
                Key = eventId,
                UserId = userId,
                Data = eventData
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="size"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetAll(int size = 1000, int page = 0)
        {
            return await databaseAccessor.Get(size, page);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public async Task<IAnalyticsInfoData> GetById(string eventId)
        {
            return await databaseAccessor.GetById(eventId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventKey"></param>
        /// <param name="size"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetByKey(string eventKey, int size = 1000, int page = 0)
        {
            return await databaseAccessor.GetByKey(eventKey, size, page);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timastamp"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetByTimestamp(DateTime timastamp)
        {
            return await databaseAccessor.GetByTimestamp(timastamp);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="size"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetByTimestampRange(DateTime start, DateTime end, int size = 1000, int page = 0)
        {
            return await databaseAccessor.GetByTimestampRange(start, end, size, page);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="size"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetByUserId(string userId, int size = 1000, int page = 0)
        {
            return await databaseAccessor.GetByUserId(userId, size, page);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="size"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetWithQuery(string query, int size = 1000, int page = 0)
        {
            return await databaseAccessor.GetWithQuery(query, size, page);
        }

        /// <summary>
        /// Saves event to database
        /// </summary>
        /// <param name="eventinfo"></param>
        public void Save(AnalyticsDataInfoPacket eventinfo)
        {
            if (useAnalytics)
            {
                lock (eventsToSave)
                {
                    eventsToSave.Add(eventinfo);
                }
            }
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
