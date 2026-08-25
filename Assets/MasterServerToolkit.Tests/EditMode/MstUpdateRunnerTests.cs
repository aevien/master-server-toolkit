using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstUpdateRunnerTests
    {
        private static readonly MethodInfo updateMethod = typeof(MstUpdateRunner).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo instanceField = typeof(SingletonBehaviour<MstUpdateRunner>).GetField(
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo wasCreatedField = typeof(SingletonBehaviour<MstUpdateRunner>).GetField(
            "_wasCreated",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo creationHasPendingConfigField = typeof(SingletonBehaviour<MstUpdateRunner>).GetField(
            "_creationHasPendingConfig",
            BindingFlags.Static | BindingFlags.NonPublic);

        private MstUpdateRunner runner;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Mst.Create, Is.Not.Null);
            DestroyRunner();
            runner = null;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyRunner();
        }

        [Test]
        public void NonCreatingOperations_WhenRunnerDoesNotExist_DoNotCreateSingleton()
        {
            var updatable = new TestUpdatable("unused", 0, new List<string>());

            MstUpdateRunner.Remove(updatable);
            Assert.That(GetRunner(), Is.Null);

            Assert.That(MstUpdateRunner.Contains(updatable), Is.False);
            Assert.That(GetRunner(), Is.Null);

            MstUpdateRunner.UpdateParameters(updatable);
            Assert.That(GetRunner(), Is.Null);

            Assert.That(MstUpdateRunner.GetPerformanceStats(), Is.EqualTo("MstUpdateRunner not initialized"));
            Assert.That(GetRunner(), Is.Null);
        }

        [Test]
        public void Registration_UsesReferenceIdentity_WhenObjectsAreValueEqual()
        {
            var calls = new List<string>();
            var first = new ValueEqualTestUpdatable("first", 0, calls);
            var second = new ValueEqualTestUpdatable("second", 0, calls);
            Register(first);
            Register(second);

            RunUpdate();

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            Assert.That(runner.Count, Is.EqualTo(2));

            calls.Clear();
            MstUpdateRunner.Remove(first);
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "second" }, calls);
            Assert.That(MstUpdateRunner.Contains(first), Is.False);
            Assert.That(MstUpdateRunner.Contains(second), Is.True);
            Assert.That(runner.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeferredOperations_UseReferenceIdentity_WhenObjectsAreValueEqual()
        {
            var calls = new List<string>();
            var removed = new ValueEqualTestUpdatable("removed", 0, calls);
            var reprioritized = new ValueEqualTestUpdatable("reprioritized", 100, calls);
            bool mutate = true;
            var owner = new TestUpdatable("owner", -100, calls, _ =>
            {
                if (!mutate)
                    return;

                mutate = false;
                MstUpdateRunner.Remove(removed);
                reprioritized.Priority = -200;
                MstUpdateRunner.UpdateParameters(reprioritized);
            });
            Register(removed);
            Register(reprioritized);
            Register(owner);

            RunUpdate();
            RunUpdate();

            CollectionAssert.AreEqual(
                new[] { "owner", "reprioritized", "reprioritized", "owner" },
                calls);
            Assert.That(MstUpdateRunner.Contains(removed), Is.False);
            Assert.That(MstUpdateRunner.Contains(reprioritized), Is.True);
            Assert.That(runner.Count, Is.EqualTo(2));
        }

        [Test]
        public void PendingAdditions_UseReferenceIdentity_WhenObjectsAreValueEqual()
        {
            var calls = new List<string>();
            var first = new ValueEqualTestUpdatable("first", 0, calls);
            var second = new ValueEqualTestUpdatable("second", 0, calls);
            bool addItems = true;
            var owner = new TestUpdatable("owner", 0, calls, _ =>
            {
                if (!addItems)
                    return;

                addItems = false;
                MstUpdateRunner.Add(first);
                MstUpdateRunner.Add(second);
            });
            Register(owner);

            RunUpdate();
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "owner", "owner", "first", "second" }, calls);
            Assert.That(MstUpdateRunner.Contains(first), Is.True);
            Assert.That(MstUpdateRunner.Contains(second), Is.True);
            Assert.That(runner.Count, Is.EqualTo(3));
        }

        [Test]
        public void Registry_DoesNotCallOverriddenEqualityMembers()
        {
            var calls = new List<string>();
            var first = new ThrowingEqualityTestUpdatable("first", 0, calls);
            var second = new ThrowingEqualityTestUpdatable("second", 100, calls);

            Assert.DoesNotThrow(() =>
            {
                Register(first);
                Register(second);
                first.Priority = 200;
                MstUpdateRunner.UpdateParameters(first);
                MstUpdateRunner.Remove(first);
                RunUpdate();
            });

            CollectionAssert.AreEqual(new[] { "second" }, calls);
            Assert.That(MstUpdateRunner.Contains(second), Is.True);
            Assert.That(runner.Count, Is.EqualTo(1));
        }

        [Test]
        public void EveryFrameItems_RunLowerPriorityFirst_AndPreserveRegistrationOrderForTies()
        {
            var calls = new List<string>();
            Register(new TestUpdatable("normal-first", 0, calls));
            Register(new TestUpdatable("low", -100, calls));
            Register(new TestUpdatable("high", 100, calls));
            Register(new TestUpdatable("normal-second", 0, calls));

            RunUpdate();

            CollectionAssert.AreEqual(
                new[] { "low", "normal-first", "normal-second", "high" },
                calls);
        }

        [Test]
        public void IntervalItems_WhenDue_RunLowerPriorityFirst()
        {
            var calls = new List<string>();
            Register(new TestIntervalUpdatable("high", 100, 1f, calls));
            Register(new TestIntervalUpdatable("low", -100, 1f, calls));
            Register(new TestIntervalUpdatable("normal", 0, 1f, calls));
            MakeIntervalItemsDue();

            RunUpdate();

            CollectionAssert.AreEqual(new[] { "low", "normal", "high" }, calls);
        }

        [Test]
        public void Update_KeepsEveryFramePhaseBeforeDueIntervalPhase()
        {
            var calls = new List<string>();
            Register(new TestIntervalUpdatable("interval", -100, 1f, calls));
            Register(new TestUpdatable("every-frame", 100, calls));
            MakeIntervalItemsDue();

            RunUpdate();

            CollectionAssert.AreEqual(new[] { "every-frame", "interval" }, calls);
        }

        [Test]
        public void Remove_DuringUpdate_PreventsLaterInvocationInSamePass()
        {
            var calls = new List<string>();
            var later = new TestUpdatable("later", 100, calls);
            var first = new TestUpdatable("first", -100, calls, _ => MstUpdateRunner.Remove(later));
            Register(later);
            Register(first);

            RunUpdate();

            CollectionAssert.AreEqual(new[] { "first" }, calls);
            Assert.That(MstUpdateRunner.Contains(later), Is.False);
            Assert.That(runner.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddThenRemove_DuringSameUpdate_CancelsPendingAddition()
        {
            var calls = new List<string>();
            var pending = new TestUpdatable("pending", 0, calls);
            bool mutate = true;
            var owner = new TestUpdatable("owner", 0, calls, _ =>
            {
                if (!mutate)
                    return;

                mutate = false;
                MstUpdateRunner.Add(pending);
                MstUpdateRunner.Remove(pending);
            });
            Register(owner);

            RunUpdate();
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "owner", "owner" }, calls);
            Assert.That(MstUpdateRunner.Contains(pending), Is.False);
            Assert.That(runner.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveThenAdd_DuringSameUpdate_CancelsDeferredRemoval()
        {
            var calls = new List<string>();
            var later = new TestUpdatable("later", 100, calls);
            bool mutate = true;
            var first = new TestUpdatable("first", -100, calls, _ =>
            {
                if (!mutate)
                    return;

                mutate = false;
                MstUpdateRunner.Remove(later);
                MstUpdateRunner.Add(later);
            });
            Register(later);
            Register(first);

            RunUpdate();
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "first", "later", "first", "later" }, calls);
            Assert.That(MstUpdateRunner.Contains(later), Is.True);
            Assert.That(runner.Count, Is.EqualTo(2));
        }

        [Test]
        public void UpdateParameters_ForEveryFrameItem_UsesCurrentPriority()
        {
            var calls = new List<string>();
            var first = new TestUpdatable("first", 0, calls);
            var second = new TestUpdatable("second", 100, calls);
            Register(first);
            Register(second);

            RunUpdate();
            calls.Clear();
            first.Priority = 200;
            MstUpdateRunner.UpdateParameters(first);
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "second", "first" }, calls);
        }

        [Test]
        public void UpdateParameters_DuringUpdate_AppliesAfterCurrentPass()
        {
            var calls = new List<string>();
            bool changePriority = true;
            var later = new TestUpdatable("later", 100, calls);
            var first = new TestUpdatable("first", 0, calls, _ =>
            {
                if (!changePriority)
                    return;

                changePriority = false;
                later.Priority = -100;
                MstUpdateRunner.UpdateParameters(later);
            });
            Register(first);
            Register(later);

            RunUpdate();
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "first", "later", "later", "first" }, calls);
        }

        [Test]
        public void UpdateParametersThenThrow_DoesNotCorruptRemainingRegistrations()
        {
            var calls = new List<string>();
            TestUpdatable failing = null;
            failing = new TestUpdatable("failing", -100, calls, _ =>
            {
                failing.Priority = 100;
                MstUpdateRunner.UpdateParameters(failing);
                throw new InvalidOperationException("Expected update failure");
            });
            var healthy = new TestUpdatable("healthy", 0, calls);
            Register(failing);
            Register(healthy);

            LogAssert.Expect(LogType.Error, new Regex("Error updating TestUpdatable.*Expected update failure"));
            RunUpdate();

            CollectionAssert.AreEqual(new[] { "failing", "healthy" }, calls);
            Assert.That(MstUpdateRunner.Contains(failing), Is.False);
            Assert.That(MstUpdateRunner.Contains(healthy), Is.True);
            Assert.That(runner.Count, Is.EqualTo(1));
        }

        private void Register(IUpdatable updatable)
        {
            MstUpdateRunner.Add(updatable);
            runner = GetRunner();
            Assert.That(runner, Is.Not.Null);
        }

        private void RunUpdate()
        {
            Assert.That(updateMethod, Is.Not.Null);
            updateMethod.Invoke(runner, null);
        }

        private void MakeIntervalItemsDue()
        {
            FieldInfo intervalItemsField = typeof(MstUpdateRunner).GetField(
                "intervalItems",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(intervalItemsField, Is.Not.Null);

            var intervalItems = (IEnumerable)intervalItemsField.GetValue(runner);

            foreach (object item in intervalItems)
            {
                FieldInfo nextUpdateTimeField = item.GetType().GetField(
                    "nextUpdateTime",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(nextUpdateTimeField, Is.Not.Null);
                nextUpdateTimeField.SetValue(item, float.MinValue);
            }
        }

        private static MstUpdateRunner GetRunner()
        {
            Assert.That(instanceField, Is.Not.Null);
            return (MstUpdateRunner)instanceField.GetValue(null);
        }

        private static void DestroyRunner()
        {
            MstUpdateRunner currentRunner = GetRunner();

            if (currentRunner != null)
                UnityEngine.Object.DestroyImmediate(currentRunner.gameObject);

            Assert.That(wasCreatedField, Is.Not.Null);
            Assert.That(creationHasPendingConfigField, Is.Not.Null);
            instanceField.SetValue(null, null);
            wasCreatedField.SetValue(null, false);
            creationHasPendingConfigField.SetValue(null, false);
        }

        private class TestUpdatable : IUpdatable
        {
            private readonly string name;
            private readonly IList<string> calls;
            private readonly Action<TestUpdatable> onUpdate;

            public int Priority { get; set; }

            public TestUpdatable(
                string name,
                int priority,
                IList<string> calls,
                Action<TestUpdatable> onUpdate = null)
            {
                this.name = name;
                Priority = priority;
                this.calls = calls;
                this.onUpdate = onUpdate;
            }

            public void DoUpdate()
            {
                calls.Add(name);
                onUpdate?.Invoke(this);
            }
        }

        private sealed class TestIntervalUpdatable : TestUpdatable, IIntervalUpdatable
        {
            public float UpdateInterval { get; set; }

            public TestIntervalUpdatable(
                string name,
                int priority,
                float updateInterval,
                IList<string> calls)
                : base(name, priority, calls)
            {
                UpdateInterval = updateInterval;
            }
        }

        private sealed class ValueEqualTestUpdatable : TestUpdatable
        {
            public ValueEqualTestUpdatable(string name, int priority, IList<string> calls)
                : base(name, priority, calls)
            {
            }

            public override bool Equals(object obj)
            {
                return obj is ValueEqualTestUpdatable;
            }

            public override int GetHashCode()
            {
                return 1;
            }
        }

        private sealed class ThrowingEqualityTestUpdatable : TestUpdatable
        {
            public ThrowingEqualityTestUpdatable(string name, int priority, IList<string> calls)
                : base(name, priority, calls)
            {
            }

            public override bool Equals(object obj)
            {
                throw new InvalidOperationException("MstUpdateRunner must not call IUpdatable.Equals");
            }

            public override int GetHashCode()
            {
                throw new InvalidOperationException("MstUpdateRunner must not call IUpdatable.GetHashCode");
            }
        }
    }
}
