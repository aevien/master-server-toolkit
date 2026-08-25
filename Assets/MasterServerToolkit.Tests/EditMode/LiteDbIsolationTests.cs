using MasterServerToolkit.Bridges.LiteDB;
using MasterServerToolkit.GameService;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Tests.EditMode
{
    public class LiteDbIsolationTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(
                Path.GetTempPath(),
                "mst_litedb_tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, true);
        }

        [Test]
        public async Task AccountsAndChat_SeparateFiles_RemainIndependent()
        {
            string accountsName = Path.Combine(testDirectory, "accounts");
            string chatName = Path.Combine(testDirectory, "chat");
            AccountsDatabaseAccessor accountsAccessor = null;
            ChatDatabaseAccessor chatAccessor = null;

            try
            {
                accountsAccessor = new AccountsDatabaseAccessor(accountsName);
                chatAccessor = new ChatDatabaseAccessor(chatName);

                var message = new ChatMessageInfo
                {
                    Id = "message-1",
                    MessageType = ChatMessageType.Private,
                    Sender = "alice",
                    Receiver = "bob",
                    Message = "raw message",
                    CreatedAtUtc = new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc)
                };

                await chatAccessor.SaveMessageAsync(message);

                accountsAccessor.Dispose();
                accountsAccessor = null;

                List<ChatMessageInfo> messages =
                    await chatAccessor.GetPrivateMessagesAsync("alice", "bob", 10);

                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(messages[0].Message, Is.EqualTo("raw message"));
                Assert.That(File.Exists(accountsName + ".db"), Is.True);
                Assert.That(File.Exists(chatName + ".db"), Is.True);

                chatAccessor.Dispose();
                chatAccessor = null;
                chatAccessor = new ChatDatabaseAccessor(chatName);

                messages = await chatAccessor.GetPrivateMessagesAsync("alice", "bob", 10);
                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(messages[0].CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            }
            finally
            {
                chatAccessor?.Dispose();
                accountsAccessor?.Dispose();
            }
        }

        [Test]
        public async Task ServiceBindingReplacement_UsesOneStableAccountServiceSlot()
        {
            string accountsName = Path.Combine(testDirectory, "accounts_bindings");
            using var accessor = new AccountsDatabaseAccessor(accountsName);

            IAccountServiceBindingData binding = accessor.CreateServiceBindingInstance();
            binding.AccountId = "account-1";
            binding.ServiceId = GameServiceId.YandexGames.ToString();
            binding.PlayerId = "player-old";

            await accessor.ReplaceServiceBindingAsync(binding);

            binding.PlayerId = "player-new";
            await accessor.ReplaceServiceBindingAsync(binding);

            IAccountServiceBindingData oldBinding = await accessor.GetServiceBindingAsync(
                GameServiceId.YandexGames,
                "player-old");
            IAccountServiceBindingData newBinding = await accessor.GetServiceBindingAsync(
                GameServiceId.YandexGames,
                "player-new");
            IReadOnlyList<IAccountServiceBindingData> accountBindings =
                await accessor.GetServiceBindingsByAccountIdAsync("account-1");

            Assert.That(oldBinding, Is.Null);
            Assert.That(newBinding, Is.Not.Null);
            Assert.That(newBinding.Id, Is.EqualTo("account-1:YandexGames"));
            Assert.That(newBinding.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(newBinding.UpdatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(newBinding.LastLoginAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(accountBindings.Count, Is.EqualTo(1));

            IAccountServiceBindingData conflictingBinding = accessor.CreateServiceBindingInstance();
            conflictingBinding.AccountId = "account-2";
            conflictingBinding.ServiceId = GameServiceId.YandexGames.ToString();
            conflictingBinding.PlayerId = "player-new";

            await AssertThrowsAsync<InvalidOperationException>(
                () => accessor.ReplaceServiceBindingAsync(conflictingBinding));
        }

        [Test]
        public async Task ConfirmationCode_ConcurrentChecks_ConsumeItOnlyOnce()
        {
            string accountsName = Path.Combine(testDirectory, "accounts_codes");
            using var accessor = new AccountsDatabaseAccessor(accountsName);

            await accessor.SaveEmailConfirmationCodeAsync(
                "Player@Example.com",
                "123456",
                DateTime.UtcNow.AddMinutes(10),
                5);

            Task<VerificationCodeResult>[] checks = Enumerable.Range(0, 8)
                .Select(_ => accessor.CheckEmailConfirmationCodeAsync("player@example.com", "123456"))
                .ToArray();
            VerificationCodeResult[] results = await Task.WhenAll(checks);

            Assert.That(
                results.Count(result => result == VerificationCodeResult.Success),
                Is.EqualTo(1));
            Assert.That(
                results.Count(result => result == VerificationCodeResult.Invalid),
                Is.EqualTo(7));
        }

        [Test]
        public async Task Leaderboards_KeepBestPreservesScoreAndRefreshesPlayerMetadata()
        {
            string leaderboardsName = Path.Combine(testDirectory, "leaderboards_keep_best");
            DateTime firstSubmissionAt = new DateTime(
                2026, 8, 20, 1, 0, 0, DateTimeKind.Utc);
            DateTime secondSubmissionAt = firstSubmissionAt.AddMinutes(1);

            using (var accessor = new LeaderboardsDatabaseAccessor(leaderboardsName))
            {
                await accessor.SubmitScoreAsync(CreateLeaderboardSubmission(
                    "account-a",
                    "Old Name",
                    200,
                    firstSubmissionAt,
                    "https://cdn.example.com/old.png"));
                LeaderboardScoreUpdateResult replay = await accessor.SubmitScoreAsync(
                    CreateLeaderboardSubmission(
                        "account-a",
                        "Current Name",
                        100,
                        secondSubmissionAt,
                        "https://cdn.example.com/current.png"));

                Assert.That(replay.ScoreChanged, Is.False);
                Assert.That(replay.Entry.Score, Is.EqualTo(200));
                Assert.That(replay.Entry.PlayerName, Is.EqualTo("Current Name"));
                Assert.That(replay.Entry.PlayerAvatar,
                    Is.EqualTo("https://cdn.example.com/current.png"));
                Assert.That(replay.Entry.CreatedAtUtc, Is.EqualTo(firstSubmissionAt));
                Assert.That(replay.Entry.UpdatedAtUtc, Is.EqualTo(secondSubmissionAt));
                Assert.That(replay.Entry.UpdatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            }

            using var restoredAccessor = new LeaderboardsDatabaseAccessor(leaderboardsName);
            LeaderboardEntry restored = await restoredAccessor.GetEntryAsync(
                "survivalTime",
                "season-test",
                "account-a");
            Assert.That(restored.PlayerAvatar,
                Is.EqualTo("https://cdn.example.com/current.png"));
        }

        [Test]
        public async Task Leaderboards_ConcurrentSubmissionsAndRanksUseOneOrderingContract()
        {
            string leaderboardsName = Path.Combine(testDirectory, "leaderboards_ranking");
            using var accessor = new LeaderboardsDatabaseAccessor(leaderboardsName);
            var startGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Task<LeaderboardScoreUpdateResult>[] sameAccountSubmissions =
                Enumerable.Range(1, 20)
                    .Select(score => Task.Run(async () =>
                    {
                        await startGate.Task;
                        return await accessor.SubmitScoreAsync(
                            CreateLeaderboardSubmission(
                                "account-c", "Player C", score, DateTime.UtcNow));
                    }))
                    .ToArray();

            startGate.SetResult(true);
            await Task.WhenAll(sameAccountSubmissions);
            await accessor.SubmitScoreAsync(CreateLeaderboardSubmission(
                "account-a", "Player A", 20, DateTime.UtcNow));
            await accessor.SubmitScoreAsync(CreateLeaderboardSubmission(
                "account-b", "Player B", 30, DateTime.UtcNow));

            IReadOnlyList<LeaderboardEntry> entries = await accessor.GetEntriesAsync(
                "survivalTime", "season-test", LeaderboardSortOrder.Descending, 0, 10);
            LeaderboardEntry accountC = await accessor.GetEntryAsync(
                "survivalTime", "season-test", "account-c");
            long betterThanC = await accessor.CountBetterEntriesAsync(
                "survivalTime", "season-test", LeaderboardSortOrder.Descending,
                accountC.Score, accountC.AccountId);

            Assert.That(entries.Select(entry => entry.AccountId), Is.EqualTo(new[]
            {
                "account-b",
                "account-a",
                "account-c"
            }));
            Assert.That(accountC.Score, Is.EqualTo(20));
            Assert.That(betterThanC + 1L, Is.EqualTo(3L));
        }

        private static LeaderboardScoreSubmission CreateLeaderboardSubmission(
            string accountId,
            string playerName,
            long score,
            DateTime submittedAtUtc,
            string playerAvatar = null)
        {
            return new LeaderboardScoreSubmission
            {
                LeaderboardKey = "survivalTime",
                SeasonId = "season-test",
                AccountId = accountId,
                PlayerName = playerName,
                PlayerAvatar = playerAvatar,
                Score = score,
                SortOrder = LeaderboardSortOrder.Descending,
                KeepBest = true,
                SubmittedAtUtc = submittedAtUtc
            };
        }

        [Test]
        public async Task PasswordResetCode_InvalidAttempts_RemoveCodeOnLastAttempt()
        {
            string accountsName = Path.Combine(testDirectory, "accounts_reset_attempts");
            using var accessor = new AccountsDatabaseAccessor(accountsName);

            await accessor.SavePasswordResetCodeAsync(
                "player@example.com",
                "123456",
                DateTime.UtcNow.AddMinutes(10),
                2);

            VerificationCodeResult first = await accessor.CheckPasswordResetCodeAsync(
                "player@example.com",
                "000000");
            VerificationCodeResult second = await accessor.CheckPasswordResetCodeAsync(
                "player@example.com",
                "111111");
            VerificationCodeResult afterRemoval = await accessor.CheckPasswordResetCodeAsync(
                "player@example.com",
                "123456");

            Assert.That(first, Is.EqualTo(VerificationCodeResult.Invalid));
            Assert.That(second, Is.EqualTo(VerificationCodeResult.AttemptsExceeded));
            Assert.That(afterRemoval, Is.EqualTo(VerificationCodeResult.Invalid));
        }

        [Test]
        public async Task PasswordResetCode_WhenExpired_IsRemoved()
        {
            string accountsName = Path.Combine(testDirectory, "accounts_reset_expired");
            using var accessor = new AccountsDatabaseAccessor(accountsName);

            await accessor.SavePasswordResetCodeAsync(
                "player@example.com",
                "123456",
                DateTime.UtcNow.AddSeconds(-1),
                5);

            VerificationCodeResult expired = await accessor.CheckPasswordResetCodeAsync(
                "player@example.com",
                "123456");
            VerificationCodeResult afterRemoval = await accessor.CheckPasswordResetCodeAsync(
                "player@example.com",
                "123456");

            Assert.That(expired, Is.EqualTo(VerificationCodeResult.Expired));
            Assert.That(afterRemoval, Is.EqualTo(VerificationCodeResult.Invalid));
        }

        [Test]
        public async Task ConfirmationCode_WhenReplaced_UsesNewCodeAndAttemptBudget()
        {
            string accountsName = Path.Combine(testDirectory, "accounts_confirmation_replaced");
            using var accessor = new AccountsDatabaseAccessor(accountsName);

            await accessor.SaveEmailConfirmationCodeAsync(
                "player@example.com",
                "111111",
                DateTime.UtcNow.AddMinutes(10),
                1);
            await accessor.SaveEmailConfirmationCodeAsync(
                "player@example.com",
                "222222",
                DateTime.UtcNow.AddMinutes(10),
                2);

            VerificationCodeResult oldCode = await accessor.CheckEmailConfirmationCodeAsync(
                "player@example.com",
                "111111");
            VerificationCodeResult newCode = await accessor.CheckEmailConfirmationCodeAsync(
                "player@example.com",
                "222222");

            Assert.That(oldCode, Is.EqualTo(VerificationCodeResult.Invalid));
            Assert.That(newCode, Is.EqualTo(VerificationCodeResult.Success));
        }

        [Test]
        public async Task AuthTokenRevision_ConcurrentIncrements_AreNotLost()
        {
            string accountsName = Path.Combine(testDirectory, "accounts_token_revision");
            using var accessor = new AccountsDatabaseAccessor(accountsName);
            const string accountId = "account-1";

            Assert.That(await accessor.GetAuthTokenRevisionAsync(accountId), Is.EqualTo(0));

            int[] revisions = await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(_ => accessor.IncrementAuthTokenRevisionAsync(accountId)));

            Assert.That(revisions.Distinct().Count(), Is.EqualTo(16));
            Assert.That(revisions.Min(), Is.EqualTo(1));
            Assert.That(revisions.Max(), Is.EqualTo(16));
            Assert.That(await accessor.GetAuthTokenRevisionAsync(accountId), Is.EqualTo(16));
        }

        private static async Task AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception exception)
            {
                Assert.Fail(
                    $"Expected {typeof(TException).Name}, but {exception.GetType().Name} was thrown: {exception.Message}");
            }

            Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown");
        }
    }
}
