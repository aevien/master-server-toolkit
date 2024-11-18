#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private ILiteCollection<ProfileInfoData> profiles;
        private readonly ILiteDatabase database;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public ProfilesDatabaseAccessor(string databaseName)
        {
            database = new LiteDatabase($"{databaseName}.db");

            profiles = database.GetCollection<ProfileInfoData>("profiles");
            profiles.EnsureIndex(a => a.UserId, true);
        }

        public void Dispose()
        {
            database?.Dispose();
            profiles = null;
        }

        /// <summary>
        /// Get profile info from database
        /// </summary>
        /// <param name="profile"></param>
        public async Task RestoreProfileAsync(ObservableServerProfile profile)
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

            profile.FromBytes(data.Data);
        }

        /// <summary>
        /// Update profile info in database
        /// </summary>
        /// <param name="profile"></param>
        public async Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            await UpdateProfilesAsync(new List<ObservableServerProfile>()
            {
                profile
            });
        }

        /// <summary>
        /// Update profiles info in database
        /// </summary>
        /// <param name="profiles"></param>
        public async Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles)
        {
            if (profiles == null || !profiles.Any())
                throw new ArgumentException("Profiles collection is null or empty.");

            await Task.Run(() =>
            {
                // Extract all UserIds from the input profiles
                var userIds = profiles.Select(p => p.UserId).ToList();

                // Perform a batch search for all existing profiles with matching UserIds
                var existingData = this.profiles?.Find(a => userIds.Contains(a.UserId)).ToList();

                // Lists to store profiles for update and insertion
                var newProfiles = new List<ProfileInfoData>();
                var updatedProfiles = new List<ProfileInfoData>();

                foreach (var profile in profiles)
                {
                    // Check if the profile already exists in the database
                    var existingProfile = existingData?.FirstOrDefault(p => p.UserId == profile.UserId);

                    if (existingProfile != null)
                    {
                        // Update the existing profile data
                        existingProfile.Data = profile.ToBytes();
                        updatedProfiles.Add(existingProfile);
                    }
                    else
                    {
                        // Create a new profile for insertion
                        var newProfile = new ProfileInfoData
                        {
                            UserId = profile.UserId,
                            Data = profile.ToBytes()
                        };
                        newProfiles.Add(newProfile);
                    }
                }

                // Perform batch update for existing profiles
                if (updatedProfiles.Any())
                {
                    this.profiles?.Update(updatedProfiles);
                }

                // Perform batch insert for new profiles
                if (newProfiles.Any())
                {
                    // Use InsertBulk for optimized insertion
                    this.profiles?.InsertBulk(newProfiles);
                }
            });
        }
    }
}

#endif