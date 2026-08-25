using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField, Tooltip("Delay in seconds used to batch newly received analytics events before writing them to the database. 0 makes the next update eligible to save immediately.")]
        protected float saveDebounceTime = 5f;
        [SerializeField, Tooltip("Enables analytics event persistence. When disabled, incoming analytics packets are accepted by the module but are not queued for database storage.")]
        protected bool useAnalytics = true;

        /// <summary>
        /// Database accessor factory that helps to create integration with analytics db
        /// </summary>
        [Tooltip("Factory that creates the analytics database accessor. Required when Use Analytics is enabled."), SerializeField]
        protected DatabaseAccessorFactory databaseAccessorFactory;

        #endregion

        protected readonly List<AnalyticsDataInfoPacket> eventsToSave = new List<AnalyticsDataInfoPacket>();
        private readonly object saveSync = new object();
        private Task activeSaveTask = Task.CompletedTask;
        private DateTime nextSaveUtc;
        private bool isRunActive;
        private bool saveInProgress;

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

            if (useAnalytics && databaseAccessor == null)
            {
                logger.Error($"{nameof(AnalyticsModule)} is enabled, but {nameof(IAnalyticsDatabaseAccessor)} not found. Analytics events will not be saved.");
                useAnalytics = false;
            }
        }

        public override void StartServerRun(CancellationToken runCancellationToken)
        {
            if (!useAnalytics)
                return;

            lock (saveSync)
            {
                isRunActive = true;
                saveInProgress = false;
                activeSaveTask = Task.CompletedTask;
                nextSaveUtc = DateTime.MinValue;
            }

            InvokeRepeating(nameof(UpdateThrottle), 0.1f, 0.1f);
        }

        public override async Task StopServerRunAsync()
        {
            Task saveTask;

            lock (saveSync)
            {
                isRunActive = false;
                saveTask = activeSaveTask;
            }

            if (this != null)
                CancelInvoke(nameof(UpdateThrottle));

            try
            {
                await saveTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.Error($"Analytics batch failed before shutdown flush and will be retried: {exception}");
            }

            await SaveEventsAsync().ConfigureAwait(false);
        }

        protected virtual void UpdateThrottle()
        {
            List<AnalyticsDataInfoPacket> snapshot;
            TaskCompletionSource<bool> saveCompletion;

            lock (saveSync)
            {
                if (!isRunActive || saveInProgress || eventsToSave.Count == 0 || DateTime.UtcNow < nextSaveUtc)
                    return;

                saveInProgress = true;
                nextSaveUtc = DateTime.UtcNow.AddSeconds(Math.Max(0f, saveDebounceTime));
                snapshot = TakeEventsSnapshotLocked();
                saveCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                activeSaveTask = saveCompletion.Task;
            }

            _ = SaveBatchAsync(snapshot, saveCompletion);
            _ = ObserveSaveTaskAsync(saveCompletion.Task);
        }

        protected virtual async Task SaveEventsAsync()
        {
            List<AnalyticsDataInfoPacket> snapshot;

            lock (saveSync)
                snapshot = TakeEventsSnapshotLocked();

            if (snapshot.Count == 0 || databaseAccessor == null)
                return;

            try
            {
                await databaseAccessor.Insert(snapshot).ConfigureAwait(false);
            }
            catch
            {
                lock (saveSync)
                    eventsToSave.InsertRange(0, snapshot);

                throw;
            }
        }

        private List<AnalyticsDataInfoPacket> TakeEventsSnapshotLocked()
        {
            var snapshot = new List<AnalyticsDataInfoPacket>(eventsToSave);
            eventsToSave.Clear();
            return snapshot;
        }

        private async Task SaveBatchAsync(List<AnalyticsDataInfoPacket> snapshot,
            TaskCompletionSource<bool> saveCompletion)
        {
            Exception failure = null;

            try
            {
                if (snapshot.Count > 0 && databaseAccessor != null)
                    await databaseAccessor.Insert(snapshot).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;

                lock (saveSync)
                    eventsToSave.InsertRange(0, snapshot);
            }
            finally
            {
                lock (saveSync)
                {
                    if (ReferenceEquals(activeSaveTask, saveCompletion.Task))
                        saveInProgress = false;
                }

                if (failure == null)
                    saveCompletion.TrySetResult(true);
                else
                    saveCompletion.TrySetException(failure);
            }
        }

        private async Task ObserveSaveTaskAsync(Task saveTask)
        {
            try
            {
                await saveTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.Error($"Failed to save analytics batch: {exception}");
            }
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
            if (databaseAccessor == null)
                return Array.Empty<IAnalyticsInfoData>();

            return await databaseAccessor.Get(size, page);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public async Task<IAnalyticsInfoData> GetById(string eventId)
        {
            if (databaseAccessor == null)
                return null;

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
            if (databaseAccessor == null)
                return Array.Empty<IAnalyticsInfoData>();

            return await databaseAccessor.GetByKey(eventKey, size, page);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timastamp"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IAnalyticsInfoData>> GetByTimestamp(DateTime timastamp)
        {
            if (databaseAccessor == null)
                return Array.Empty<IAnalyticsInfoData>();

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
            if (databaseAccessor == null)
                return Array.Empty<IAnalyticsInfoData>();

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
            if (databaseAccessor == null)
                return Array.Empty<IAnalyticsInfoData>();

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
            if (databaseAccessor == null)
                return Array.Empty<IAnalyticsInfoData>();

            return await databaseAccessor.GetWithQuery(query, size, page);
        }

        public async Task<DatabaseEntriesInfo<IAnalyticsInfoData>> Search(Dictionary<string, object> filter)
        {
            if (databaseAccessor == null)
                return new DatabaseEntriesInfo<IAnalyticsInfoData>();

            return await databaseAccessor.Search(filter);
        }

        /// <summary>
        /// Saves event to database
        /// </summary>
        /// <param name="eventinfo"></param>
        /// <returns><c>true</c> when the event was accepted by the active server run.</returns>
        public bool Save(AnalyticsDataInfoPacket eventinfo)
        {
            if (!useAnalytics || eventinfo == null)
                return false;

            lock (saveSync)
            {
                if (!isRunActive)
                    return false;

                eventsToSave.Add(eventinfo);
                return true;
            }
        }

        #region MESSAGE HANDLERS

        private Task OuSendAnalyticsDataMessageHandler(IIncomingMessage message)
        {
            var data = message.AsPacket<AnalyticsDataInfoPacket>();

            if (Save(data))
                message.Respond(ResponseStatus.Success);
            else
                message.RespondError(ResponseStatus.ServiceUnavailable,
                    MstErrorCodes.ANALYTICS_UNAVAILABLE);

            return Task.CompletedTask;
        }

        #endregion
    }
}
