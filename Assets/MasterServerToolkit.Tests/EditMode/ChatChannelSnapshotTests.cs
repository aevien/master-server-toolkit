using MasterServerToolkit.MasterServer;
using NUnit.Framework;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ChatChannelSnapshotTests
    {
        [Test]
        public void InvitedUsersSnapshot_RemainsDetachedAfterInviteIsRevoked()
        {
            var channel = new ChatChannel("test");
            Assert.That(channel.InviteUser("Player"), Is.True);

            var snapshot = channel.GetInvitedUsersSnapshot();
            Assert.That(channel.RevokeInvite("Player"), Is.True);

            Assert.That(snapshot, Is.EquivalentTo(new[] { "Player" }));
            Assert.That(channel.GetInvitedUsersSnapshot(), Is.Empty);
        }

        [Test]
        public void BannedUsersSnapshot_RemainsDetachedAfterUserIsUnbanned()
        {
            var channel = new ChatChannel("test");
            Assert.That(channel.BanUser("Player"), Is.True);

            var snapshot = channel.GetBannedUsersSnapshot();
            Assert.That(channel.UnbanUser("Player"), Is.True);

            Assert.That(snapshot, Is.EquivalentTo(new[] { "Player" }));
            Assert.That(channel.GetBannedUsersSnapshot(), Is.Empty);
        }
    }
}
