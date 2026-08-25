using MasterServerToolkit.UI;
using NUnit.Framework;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class UIViewSyncTests
    {
        private GameObject ownerObject;
        private GameObject targetObject;

        [TearDown]
        public void TearDown()
        {
            if (ownerObject != null)
                Object.DestroyImmediate(ownerObject);

            if (targetObject != null)
                Object.DestroyImmediate(targetObject);
        }

        [Test]
        public void OwnerHideEvent_HidesSynchronizedView()
        {
            TestView ownerView = CreateView("UIViewSyncTests.Owner", out ownerObject);
            TestView targetView = CreateView("UIViewSyncTests.Target", out targetObject);
            TestViewSync sync = ownerObject.AddComponent<TestViewSync>();
            sync.SetSynchronizedViews(targetView);
            sync.InitializeForTests();

            targetView.Show(true);
            Assert.That(targetView.IsVisible, Is.True);

            ownerView.OnHideEvent.Invoke();

            Assert.That(targetView.IsVisible, Is.False);
        }

        private static TestView CreateView(string name, out GameObject gameObject)
        {
            gameObject = new GameObject(name, typeof(RectTransform));
            return gameObject.AddComponent<TestView>();
        }

        public sealed class TestView : UIView
        {
        }

        public sealed class TestViewSync : UIViewSync
        {
            public void InitializeForTests()
            {
                base.Awake();
            }

            public void SetSynchronizedViews(params UIView[] views)
            {
                syncedViews = views;
            }
        }
    }
}
