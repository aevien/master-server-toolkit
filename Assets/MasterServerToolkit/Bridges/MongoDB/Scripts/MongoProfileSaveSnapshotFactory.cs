#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.Bridges.MongoDB
{
    internal sealed class MongoBinaryProfileSaveSnapshot
    {
        public string UserId { get; }
        public byte[] Data { get; }

        public MongoBinaryProfileSaveSnapshot(string userId, byte[] data)
        {
            UserId = userId;
            Data = data;
        }
    }

    internal sealed class MongoDocumentProfileSaveSnapshot
    {
        public string UserId { get; }
        public Dictionary<string, string> Document { get; }

        public MongoDocumentProfileSaveSnapshot(string userId, Dictionary<string, string> document)
        {
            UserId = userId;
            Document = document;
        }
    }

    internal static class MongoProfileSaveSnapshotFactory
    {
        public static List<MongoBinaryProfileSaveSnapshot> CreateBinarySnapshots(
            IEnumerable<ObservableServerProfile> profiles)
        {
            List<ObservableServerProfile> profilesList = ValidateAndMaterialize(profiles);
            var snapshots = new List<MongoBinaryProfileSaveSnapshot>(profilesList.Count);

            foreach (ObservableServerProfile profile in profilesList)
                snapshots.Add(new MongoBinaryProfileSaveSnapshot(profile.UserId, profile.ToBytes()));

            return snapshots;
        }

        public static List<MongoDocumentProfileSaveSnapshot> CreateDocumentSnapshots(
            IEnumerable<ObservableServerProfile> profiles)
        {
            List<ObservableServerProfile> profilesList = ValidateAndMaterialize(profiles);
            var snapshots = new List<MongoDocumentProfileSaveSnapshot>(profilesList.Count);

            foreach (ObservableServerProfile profile in profilesList)
            {
                snapshots.Add(new MongoDocumentProfileSaveSnapshot(
                    profile.UserId,
                    profile.ToJson().ToDictionary()));
            }

            return snapshots;
        }

        private static List<ObservableServerProfile> ValidateAndMaterialize(
            IEnumerable<ObservableServerProfile> profiles)
        {
            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            List<ObservableServerProfile> profilesList = profiles.ToList();

            foreach (ObservableServerProfile profile in profilesList)
            {
                if (profile == null)
                    throw new ArgumentException("Profiles collection cannot contain null entries.", nameof(profiles));

                if (string.IsNullOrWhiteSpace(profile.UserId))
                    throw new ArgumentException("Every profile must have a valid UserId.", nameof(profiles));
            }

            return profilesList;
        }
    }
}
#endif
