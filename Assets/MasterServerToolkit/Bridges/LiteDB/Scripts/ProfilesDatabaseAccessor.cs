#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.MasterServer;
using System;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private ILiteCollection<ProfileInfoData> profiles;
        private readonly ILiteDatabase database;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();

        public ProfilesDatabaseAccessor(string databaseName)
        {
            database = new LiteDatabase($"{databaseName}.db");

            profiles = database.GetCollection<ProfileInfoData>("profiles");
            profiles.EnsureIndex(a => a.UserId, true);
        }

        /// <summary>
        /// Get profile info from database
        /// </summary>
        /// <param name="profile"></param>
        public async Task RestoreProfileAsync(ObservableServerProfile profile)
        {
            try
            {
                var data = await FindOrCreateData(profile);
                profile.FromBytes(data.Data);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }
        }

        public void Dispose()
        {
            database?.Dispose();
            profiles = null;
        }

        /// <summary>
        /// Update profile info in database
        /// </summary>
        /// <param name="profile"></param>
        public async Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            var data = await FindOrCreateData(profile);
            data.Data = profile.ToBytes();

            await Task.Run(() =>
            {
                profiles?.Update(data);
            });
        }

        /// <summary>
        /// Find profile data in database or create new data and insert them to database
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        private async Task<ProfileInfoData> FindOrCreateData(ObservableServerProfile profile)
        {
            string userId = profile.UserId;

            var data = await Task.Run(() =>
            {
                return profiles?.FindOne(a => a.UserId == userId);
            });

            if (data == null)
            {
                data = new ProfileInfoData()
                {
                    UserId = profile.UserId,
                    Data = profile.ToBytes()
                };

                await Task.Run(() =>
                {
                    profiles?.Insert(data);
                });
            }

            return data;
        }
    }
}

#endif