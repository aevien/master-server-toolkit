#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            database.UtcDate = true;

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
        public async Task RestoreProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RestoreProfilesAsync(new[] { profile }, cancellationToken);
        }

        public async Task RestoreProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            var profilesList = profiles as IList<ObservableServerProfile> ?? profiles.ToList();

            foreach (var profile in profilesList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (profile == null)
                    continue;

                string userId = profile.UserId;

                var data = await Task.Run(() =>
                {
                    return this.profiles?.FindOne(a => a.UserId == userId);
                });
                cancellationToken.ThrowIfCancellationRequested();

                if (data == null)
                {
                    data = new ProfileInfoData()
                    {
                        UserId = profile.UserId,
                        Data = profile.ToBytes()
                    };

                    await Task.Run(() =>
                    {
                        this.profiles?.Insert(data);
                    });
                }

                profile.FromBytes(data.Data);
            }
        }

        public Task<DatabaseEntriesInfo<IProfilePropertyData>> Search(Dictionary<string, object> filter,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DatabaseEntriesInfo<IProfilePropertyData>());
        }

        /// <summary>
        /// Update profile info in database
        /// </summary>
        /// <param name="profile"></param>
        public async Task UpdateProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateProfilesAsync(new List<ObservableServerProfile>()
            {
                profile
            }, cancellationToken);
        }

        /// <summary>
        /// Update profiles info in database
        /// </summary>
        /// <param name="profiles"></param>
        public async Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            List<ObservableServerProfile> profilesList = profiles.ToList();

            if (profilesList.Count == 0)
                throw new ArgumentException("Profiles collection is empty.", nameof(profiles));

            var snapshots = new List<ProfileInfoData>(profilesList.Count);

            foreach (ObservableServerProfile profile in profilesList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (profile == null)
                    throw new ArgumentException("Profiles collection cannot contain null entries.", nameof(profiles));

                lock (profile)
                {
                    snapshots.Add(new ProfileInfoData
                    {
                        UserId = profile.UserId,
                        Data = profile.ToBytes()
                    });
                }
            }

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var userIds = snapshots.Select(p => p.UserId).ToList();

                // Perform a batch search for all existing profiles with matching UserIds
                var existingData = this.profiles?.Find(a => userIds.Contains(a.UserId)).ToList();

                // Lists to store profiles for update and insertion
                var newProfiles = new List<ProfileInfoData>();
                var updatedProfiles = new List<ProfileInfoData>();

                foreach (ProfileInfoData snapshot in snapshots)
                {
                    // Check if the profile already exists in the database
                    var existingProfile = existingData?.FirstOrDefault(p => p.UserId == snapshot.UserId);

                    if (existingProfile != null)
                    {
                        // Update the existing profile data
                        existingProfile.Data = snapshot.Data;
                        updatedProfiles.Add(existingProfile);
                    }
                    else
                    {
                        newProfiles.Add(snapshot);
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
