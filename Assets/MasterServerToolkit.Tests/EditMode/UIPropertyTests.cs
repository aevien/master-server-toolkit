using MasterServerToolkit.UI;
using NUnit.Framework;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class UIPropertyTests
    {
        private GameObject testObject;

        [TearDown]
        public void TearDown()
        {
            if (testObject != null)
                Object.DestroyImmediate(testObject);
        }

        [Test]
        public void ValueText_WithNonZeroMinimum_DisplaysCurrentValue()
        {
            testObject = new GameObject("UIPropertyTests");
            TestUIProperty property = testObject.AddComponent<TestUIProperty>();
            TextMeshProUGUI valueText = new GameObject("Value", typeof(RectTransform))
                .AddComponent<TextMeshProUGUI>();
            valueText.transform.SetParent(testObject.transform, false);

            property.Configure(valueText, 10f, 20f, 15f);
            property.Refresh();

            bool parsed = float.TryParse(
                valueText.text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out float displayedValue);

            Assert.That(parsed, Is.True);
            Assert.That(displayedValue, Is.EqualTo(15f));
        }

        public sealed class TestUIProperty : UIProperty
        {
            public void Configure(TextMeshProUGUI text, float min, float max, float value)
            {
                valueText = text;
                SetMin(min);
                SetMax(max);
                SetValue(value);
            }

            public void Refresh()
            {
                Update();
            }
        }
    }
}
