using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstTimerTests
    {
        [SetUp]
        public void SetUp()
        {
            Assert.That(Mst.Create, Is.Not.Null);
        }

        [Test]
        public void Tick_WhenSubscriberThrows_ContinuesRemainingSubscribersInOrder()
        {
            var calls = new List<string>();
            TickActionHandler first = _ => calls.Add("first");
            TickActionHandler failing = _ =>
            {
                calls.Add("failing");
                throw new InvalidOperationException("Expected tick failure");
            };
            TickActionHandler last = _ => calls.Add("last");

            MstTimer.OnTickEvent += first;
            MstTimer.OnTickEvent += failing;
            MstTimer.OnTickEvent += last;

            try
            {
                LogAssert.Expect(LogType.Error, new Regex("MstTimer tick subscriber.*Expected tick failure"));
                LogAssert.Expect(LogType.Error, new Regex("MstTimer tick subscriber.*Expected tick failure"));
                Assert.DoesNotThrow(() =>
                {
                    InvokeTickHandlers(42);
                    InvokeTickHandlers(43);
                });
            }
            finally
            {
                MstTimer.OnTickEvent -= first;
                MstTimer.OnTickEvent -= failing;
                MstTimer.OnTickEvent -= last;
            }

            CollectionAssert.AreEqual(
                new[] { "first", "failing", "last", "first", "failing", "last" },
                calls);
        }

        [Test]
        public void RemoveSubscriber_WhenAddedTwice_PreservesOneSubscription()
        {
            int invocationCount = 0;
            TickActionHandler handler = _ => invocationCount++;

            MstTimer.OnTickEvent += handler;
            MstTimer.OnTickEvent += handler;
            MstTimer.OnTickEvent -= handler;

            try
            {
                InvokeTickHandlers(42);
            }
            finally
            {
                MstTimer.OnTickEvent -= handler;
            }

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        private static void InvokeTickHandlers(long currentTick)
        {
            MethodInfo invokeMethod = typeof(MstTimer).GetMethod(
                "InvokeTickHandlers",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(invokeMethod, Is.Not.Null);
            invokeMethod.Invoke(null, new object[] { currentTick });
        }
    }
}
