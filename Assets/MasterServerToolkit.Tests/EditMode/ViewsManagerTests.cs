using MasterServerToolkit.UI;
using NUnit.Framework;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ViewsManagerTests
    {
        private GameObject testObject;

        [TearDown]
        public void TearDown()
        {
            if (testObject != null)
                Object.DestroyImmediate(testObject);

            ViewsManager.HasVisibleInputBlockView();
            ViewsManager.HasVisibleCursorUnlockView();
        }

        [Test]
        public void VisibilityQueries_ReflectRegisteredViewPolicy()
        {
            TestView view = CreateView();
            view.BlockInput = true;
            view.UnlockCursor = true;
            view.SetVisibleForTest(true);
            ViewsManager.Register(view);

            Assert.That(ViewsManager.HasVisibleInputBlockView(), Is.True);
            Assert.That(ViewsManager.HasVisibleCursorUnlockView(), Is.True);

            view.SetVisibleForTest(false);

            Assert.That(ViewsManager.HasVisibleInputBlockView(), Is.False);
            Assert.That(ViewsManager.HasVisibleCursorUnlockView(), Is.False);
        }

        [Test]
        public void VisibilityQueries_WhenRegisteredViewIsDestroyed_PruneItWithoutThrowing()
        {
            TestView view = CreateView();
            view.BlockInput = true;
            view.SetVisibleForTest(true);
            view.LeaveStaleRegistrationOnDestroy = true;
            ViewsManager.Register(view);

            Object.DestroyImmediate(testObject);
            testObject = null;

            Assert.DoesNotThrow(() => ViewsManager.HasVisibleInputBlockView());
            Assert.That(ViewsManager.HasVisibleInputBlockView(), Is.False);
        }

        private TestView CreateView()
        {
            testObject = new GameObject("ViewsManagerTests.View", typeof(RectTransform));
            return testObject.AddComponent<TestView>();
        }

        public sealed class TestView : UIView
        {
            public bool LeaveStaleRegistrationOnDestroy { get; set; }

            public void SetVisibleForTest(bool value)
            {
                IsVisible = value;
            }

            protected override void OnDestroy()
            {
                if (!LeaveStaleRegistrationOnDestroy)
                    base.OnDestroy();
            }
        }
    }
}
