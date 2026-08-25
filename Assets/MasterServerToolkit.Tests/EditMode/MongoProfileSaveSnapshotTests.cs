using MasterServerToolkit.Bridges.MongoDB;
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Tests.EditMode
{
    public class MongoProfileSaveSnapshotTests
    {
        private const ushort PropertyKey = 8701;

        [Test]
        public void CreateBinarySnapshots_NullCollection_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MongoProfileSaveSnapshotFactory.CreateBinarySnapshots(null));
        }

        [Test]
        public void CreateDocumentSnapshots_EmptyCollection_ReturnsEmptyList()
        {
            List<MongoDocumentProfileSaveSnapshot> snapshots =
                MongoProfileSaveSnapshotFactory.CreateDocumentSnapshots(Array.Empty<ObservableServerProfile>());

            Assert.That(snapshots, Is.Empty);
        }

        [Test]
        public void CreateBinarySnapshots_NullProfile_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                MongoProfileSaveSnapshotFactory.CreateBinarySnapshots(new ObservableServerProfile[] { null }));
        }

        [Test]
        public void CreateDocumentSnapshots_BlankUserId_ThrowsArgumentException()
        {
            using (var profile = new ObservableServerProfile(" "))
            {
                Assert.Throws<ArgumentException>(() =>
                    MongoProfileSaveSnapshotFactory.CreateDocumentSnapshots(new[] { profile }));
            }
        }

        [Test]
        public void CreateBinarySnapshots_ProfileChangesAfterCapture_DoNotChangeSnapshot()
        {
            using (var profile = CreateProfile("user-1", 10))
            using (var restored = CreateProfile("user-1", 0))
            {
                MongoBinaryProfileSaveSnapshot snapshot =
                    MongoProfileSaveSnapshotFactory.CreateBinarySnapshots(new[] { profile })[0];

                profile.Get<ObservableInt>(PropertyKey).Value = 20;
                restored.FromBytes(snapshot.Data);

                Assert.That(snapshot.UserId, Is.EqualTo("user-1"));
                Assert.That(restored.Get<ObservableInt>(PropertyKey).Value, Is.EqualTo(10));
            }
        }

        [Test]
        public void CreateDocumentSnapshots_ProfileChangesAfterCapture_DoNotChangeSnapshot()
        {
            using (var profile = CreateProfile("user-2", 10))
            {
                MongoDocumentProfileSaveSnapshot snapshot =
                    MongoProfileSaveSnapshotFactory.CreateDocumentSnapshots(new[] { profile })[0];
                var expectedDocument = new Dictionary<string, string>(snapshot.Document);

                profile.Get<ObservableInt>(PropertyKey).Value = 20;

                Assert.That(snapshot.UserId, Is.EqualTo("user-2"));
                CollectionAssert.AreEquivalent(expectedDocument, snapshot.Document);
                CollectionAssert.AreNotEquivalent(profile.ToJson().ToDictionary(), snapshot.Document);
            }
        }

        private static ObservableServerProfile CreateProfile(string userId, int value)
        {
            var profile = new ObservableServerProfile(userId);
            profile.Add(new ObservableInt(PropertyKey, value));
            return profile;
        }
    }
}
