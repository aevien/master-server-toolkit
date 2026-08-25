using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Globalization;
using System.Threading;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstJsonValidationTests
    {
        [Test]
        public void CreateObject_ReturnsIndependentMutableContainers()
        {
            MstJson first = MstJson.CreateObject();
            MstJson second = MstJson.CreateObject();

            first.AddField("value", 1);

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.Zero);
            Assert.That(ReferenceEquals(first, second), Is.False);
        }

        [Test]
        public void CreateArray_ReturnsIndependentMutableContainers()
        {
            MstJson first = MstJson.CreateArray();
            MstJson second = MstJson.CreateArray();

            first.Add(1);

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.Zero);
            Assert.That(ReferenceEquals(first, second), Is.False);
        }

        [Test]
        public void CreateNull_ReturnsNewNullValue()
        {
            MstJson first = MstJson.CreateNull();
            MstJson second = MstJson.CreateNull();

            Assert.That(first.IsNull, Is.True);
            Assert.That(second.IsNull, Is.True);
            Assert.That(ReferenceEquals(first, second), Is.False);
        }

        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("  {\"name\":\"room\",\"values\":[1,-2.5e+3,true,false,null]}  ")]
        [TestCase("[\"escaped \\\"text\\\"\",{\"unicode\":\"\\u041f\\u0440\\u0438\\u0432\\u0435\\u0442\"}]")]
        public void IsJson_WhenContainerIsValid_ReturnsTrue(string json)
        {
            Assert.That(MstJson.IsJson(json), Is.True);
        }

        [TestCase("")]
        [TestCase("true")]
        [TestCase("\"text\"")]
        [TestCase("{\"x\": }")]
        [TestCase("[broken,]")]
        [TestCase("[1,]")]
        [TestCase("{\"x\":1,}")]
        [TestCase("{\"x\":01}")]
        [TestCase("{\"x\":1} trailing")]
        [TestCase("{\"x\":\"unterminated}")]
        public void IsJson_WhenInputIsInvalidOrNotAContainer_ReturnsFalse(string json)
        {
            Assert.That(MstJson.IsJson(json), Is.False);
        }

        [Test]
        public void IsJson_WhenInputIsNull_ReturnsFalse()
        {
            Assert.That(MstJson.IsJson(null), Is.False);
        }

        [Test]
        public void IsJson_WhenContainerDepthExceedsLimit_ReturnsFalse()
        {
            string json = new string('[', 129) + "0" + new string(']', 129);

            Assert.That(MstJson.IsJson(json), Is.False);
        }

        [Test]
        public void Parse_WhenStringContainsStandardEscapes_DecodesValue()
        {
            var json = new MstJson(
                "{\"slash\":\"a\\/b\",\"backslash\":\"a\\\\b\"," +
                "\"unicode\":\"\\u041f\\u0440\\u0438\\u0432\\u0435\\u0442\"}");

            Assert.That(json["slash"].StringValue, Is.EqualTo("a/b"));
            Assert.That(json["backslash"].StringValue, Is.EqualTo("a\\b"));
            Assert.That(
                json["unicode"].StringValue,
                Is.EqualTo("\u041f\u0440\u0438\u0432\u0435\u0442"));
        }

        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Local)]
        [TestCase(DateTimeKind.Unspecified)]
        public void DateTime_RoundTrip_PreservesValueAndKind(DateTimeKind kind)
        {
            var value = new DateTime(2026, 7, 28, 9, 15, 30, 123, kind).AddTicks(4567);
            var json = MstJson.CreateObject();

            json.AddField("timestamp", value);

            Assert.That(
                json["timestamp"].StringValue,
                Is.EqualTo(value.ToString("O", CultureInfo.InvariantCulture)));

            var parsedJson = new MstJson(json.ToString());
            DateTime parsedValue = parsedJson["timestamp"].GetDateTimeValue();

            Assert.That(parsedValue, Is.EqualTo(value));
            Assert.That(parsedValue.Kind, Is.EqualTo(kind));
        }

        [Test]
        public void DateTime_RoundTrip_IsIndependentOfCurrentCulture()
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            var value = new DateTime(2026, 7, 28, 9, 15, 30, DateTimeKind.Utc);

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                var json = MstJson.CreateObject();
                json.AddField("timestamp", value);
                string serializedJson = json.ToString();

                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
                DateTime parsedValue = new MstJson(serializedJson)["timestamp"].GetDateTimeValue();

                Assert.That(parsedValue, Is.EqualTo(value));
                Assert.That(parsedValue.Kind, Is.EqualTo(DateTimeKind.Utc));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void AchievementProgressInfo_JsonRoundTrip_PreservesUnlockState()
        {
            var source = new AchievementProgressInfo
            {
                key = "first_steps",
                progress = 1,
                required = 1,
                unlockedAt = 1785214830123,
                rewardApplied = true
            };
            var result = new AchievementProgressInfo();

            result.FromJson(new MstJson(source.ToJson().ToString()));

            Assert.That(result.unlockedAt, Is.EqualTo(source.unlockedAt));
            Assert.That(result.rewardApplied, Is.True);
            Assert.That(result.IsRewardApplied, Is.True);
        }

        [Test]
        public void AnalyticsDataInfoPacket_JsonRoundTrip_PreservesUtcTimestamp()
        {
            var source = new AnalyticsDataInfoPacket
            {
                Timestamp = new DateTime(2026, 7, 28, 11, 25, 35, DateTimeKind.Utc)
            };
            var result = new AnalyticsDataInfoPacket();

            result.FromJson(new MstJson(source.ToJson().ToString()));

            Assert.That(result.Timestamp, Is.EqualTo(source.Timestamp));
            Assert.That(result.Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void ObservableDateTime_StringAndJsonRoundTrips_PreserveUtcValue()
        {
            var value = new DateTime(2026, 7, 28, 12, 30, 40, DateTimeKind.Utc);
            var source = new ObservableDateTime(1, value);
            var stringResult = new ObservableDateTime(1);
            var jsonResult = new ObservableDateTime(1);

            stringResult.Deserialize(source.Serialize());
            jsonResult.FromJson(source.ToJson().ToString());

            Assert.That(stringResult.Value, Is.EqualTo(value));
            Assert.That(stringResult.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(jsonResult.Value, Is.EqualTo(value));
            Assert.That(jsonResult.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }
}
