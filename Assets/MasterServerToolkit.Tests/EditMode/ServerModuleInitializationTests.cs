using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ServerModuleInitializationTests
    {
        private GameObject testObject;
        private TestServerBehaviour server;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(ServerModuleInitializationTests));
            server = testObject.AddComponent<TestServerBehaviour>();
            server.InitializeForTest();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public void InitializeModules_WhenModuleThrows_ContinuesIndependentModulesAndPreservesDependencies()
        {
            var failing = new FailingModule();
            var independent = new IndependentModule();
            var requiredDependent = new RequiredDependentModule();
            var optionalDependent = new OptionalDependentModule();

            server.AddModule(failing);
            server.AddModule(independent);
            server.AddModule(requiredDependent);
            server.AddModule(optionalDependent);

            ExpectInitializationFailureLog();

            bool allInitialized = true;
            Assert.DoesNotThrow(() => allInitialized = server.InitializeModules());

            Assert.That(allInitialized, Is.False);
            Assert.That(failing.InitializeCount, Is.EqualTo(1));
            Assert.That(failing.Server, Is.Null);
            Assert.That(independent.InitializeCount, Is.EqualTo(1));
            Assert.That(independent.Server, Is.SameAs(server));
            Assert.That(requiredDependent.InitializeCount, Is.Zero);
            Assert.That(requiredDependent.Server, Is.Null);
            Assert.That(optionalDependent.InitializeCount, Is.EqualTo(1));
            Assert.That(optionalDependent.Server, Is.SameAs(server));
        }

        [Test]
        public void InitializeModules_DoesNotRetryFailedModuleDuringServerLifecycle()
        {
            var failing = new FailingModule();
            server.AddModule(failing);

            ExpectInitializationFailureLog();

            Assert.That(server.InitializeModules(), Is.False);
            Assert.That(server.InitializeModules(), Is.False);
            Assert.That(failing.InitializeCount, Is.EqualTo(1));
        }

        private static void ExpectInitializationFailureLog()
        {
            LogAssert.Expect(LogType.Error, new Regex("Failed to initialize module FailingModule"));
        }

        public sealed class TestServerBehaviour : ServerBehaviour
        {
            public void InitializeForTest()
            {
                base.Awake();
            }
        }

        private abstract class TestModule : IBaseServerModule
        {
            public string Id => GetType().Name;
            public List<Type> Dependencies { get; } = new List<Type>();
            public List<Type> OptionalDependencies { get; } = new List<Type>();
            public ServerBehaviour Server { get; set; }
            public int InitializeCount { get; protected set; }

            public virtual void Initialize(IServer server)
            {
                InitializeCount++;
            }

            public MstJson Info()
            {
                return MstJson.CreateObject();
            }

            public MstJson Details()
            {
                return MstJson.CreateObject();
            }
        }

        private sealed class FailingModule : TestModule
        {
            public override void Initialize(IServer server)
            {
                base.Initialize(server);
                throw new InvalidOperationException("Expected initialization failure");
            }
        }

        private sealed class IndependentModule : TestModule
        {
        }

        private sealed class RequiredDependentModule : TestModule
        {
            public RequiredDependentModule()
            {
                Dependencies.Add(typeof(FailingModule));
            }
        }

        private sealed class OptionalDependentModule : TestModule
        {
            public OptionalDependentModule()
            {
                OptionalDependencies.Add(typeof(FailingModule));
            }
        }
    }
}
