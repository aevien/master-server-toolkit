using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    public class AnalyticsModuleServerTests
    {
        [Test]
        public void SendEvent_WhenDisconnected_DoesNotCreateRequest()
        {
            var socket = new FakeClientSocket(false);
            var analytics = new AnalyticsModuleServer(socket);

            analytics.SendEvent("user", "event", "category", NewData());

            Assert.That(socket.Requests, Is.Empty);
        }

        [Test]
        public void SendSessionEvent_WhenInitiallyDisconnected_CanSendAfterReconnect()
        {
            var socket = new FakeClientSocket(false);
            var analytics = new AnalyticsModuleServer(socket);

            analytics.SendSessionEvent("user", "session", "category", NewData());
            socket.Connect("127.0.0.1", 7777);
            analytics.SendSessionEvent("user", "session", "category", NewData());

            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(socket.Requests[0].Message.OpCode, Is.EqualTo(MstOpCodes.SendAnalyticsData));
        }

        [Test]
        public void SendEvent_WhenTransportDisconnects_DoesNotDecodeTerminalResponseAsMasterError()
        {
            var socket = new FakeClientSocket();
            var analytics = new AnalyticsModuleServer(socket);
            bool analyticsErrorLogged = false;
            LogHandler captureAppender = (logger, level, channel, message) =>
            {
                if (level == LogLevel.Error &&
                    message?.ToString().Contains("analytics") == true)
                {
                    analyticsErrorLogged = true;
                }
            };
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;

            LogManager.AddAppender(captureAppender);
            LogAssert.ignoreFailingMessages = true;

            try
            {
                analytics.SendEvent("user", "event", "category", NewData());
                socket.Close(false);
                socket.RespondNext(
                    MstOpCodes.SendAnalyticsData,
                    ResponseStatus.NotConnected,
                    "-1".ToUint16Hash(),
                    Encoding.UTF8.GetBytes("Not connected"));
            }
            finally
            {
                LogManager.RemoveAppender(captureAppender);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(analyticsErrorLogged, Is.False);
        }

        [Test]
        public void SendEvent_WhenMasterRejectsStructuredError_LogsItsCode()
        {
            var socket = new FakeClientSocket();
            var analytics = new AnalyticsModuleServer(socket);
            bool errorCodeLogged = false;
            LogHandler captureAppender = (logger, level, channel, message) =>
            {
                if (level == LogLevel.Error &&
                    message?.ToString().Contains(MstErrorCodes.ANALYTICS_UNAVAILABLE) == true)
                {
                    errorCodeLogged = true;
                }
            };
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.ANALYTICS_UNAVAILABLE);

            LogManager.AddAppender(captureAppender);
            LogAssert.ignoreFailingMessages = true;

            try
            {
                analytics.SendEvent("user", "event", "category", NewData());
                socket.RespondNext(
                    MstOpCodes.SendAnalyticsData,
                    ResponseStatus.ServiceUnavailable,
                    properties.ToBytes());
            }
            finally
            {
                LogManager.RemoveAppender(captureAppender);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(errorCodeLogged, Is.True);
        }

        private static Dictionary<string, string> NewData()
        {
            return new Dictionary<string, string>();
        }
    }
}
