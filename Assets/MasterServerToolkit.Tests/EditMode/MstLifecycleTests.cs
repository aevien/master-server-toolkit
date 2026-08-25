using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstLifecycleTests
    {
        [Test]
        public void EnteredEditMode_ReinitializesMstFacadeAndErrorRegistry()
        {
            MstErrorParser previousErrors = Mst.Errors;
            MethodInfo playModeStateHandler = typeof(Mst).GetMethod(
                "OnEditorPlayModeStateChanged",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(playModeStateHandler, Is.Not.Null);
            Assert.DoesNotThrow(() => playModeStateHandler.Invoke(
                null,
                new object[] { PlayModeStateChange.EnteredEditMode }));

            Assert.That(Mst.Args, Is.Not.Null);
            Assert.That(Mst.Client, Is.Not.Null);
            Assert.That(Mst.Connection, Is.Not.Null);
            Assert.That(Mst.Create, Is.Not.Null);
            Assert.That(Mst.Events, Is.Not.Null);
            Assert.That(Mst.Errors, Is.Not.Null);
            Assert.That(Mst.Helper, Is.Not.Null);
            Assert.That(Mst.Localization, Is.Not.Null);
            Assert.That(Mst.Options, Is.Not.Null);
            Assert.That(Mst.Runtime, Is.Not.Null);
            Assert.That(Mst.Security, Is.Not.Null);
            Assert.That(Mst.Server, Is.Not.Null);
            Assert.That(Mst.Settings, Is.Not.Null);
            Assert.That(Mst.Thread, Is.Not.Null);
            Assert.That(Mst.Traffic, Is.Not.Null);
            Assert.That(Mst.Errors, Is.Not.SameAs(previousErrors));

            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.AUTH_PERMISSION_DENIED);
            Assert.That(
                Mst.Errors.Parse(Networking.ResponseStatus.Forbidden, properties),
                Is.EqualTo(Mst.Errors.Localize("ui.error.auth.permission_denied.message")));
        }
    }
}
