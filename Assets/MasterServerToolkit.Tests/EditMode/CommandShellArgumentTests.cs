using MasterServerToolkit.CommandTerminal;
using NUnit.Framework;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class CommandShellArgumentTests
    {
        [Test]
        public void TryGetInt_WhenValueIsInvalid_ReturnsFalseAndReportsError()
        {
            var shell = new CommandShell();
            bool handlerCompleted = false;

            shell.AddCommand("parse", args =>
            {
                handlerCompleted = args[0].TryGetInt(out _);
            }, 1, 1);

            shell.RunCommand("parse invalid");

            Assert.That(handlerCompleted, Is.False);
            Assert.That(shell.IssuedErrorMessage, Does.Contain("expected <int>"));
        }

        [Test]
        public void TryGetFloat_WhenValueUsesInvariantFormat_ReturnsParsedValue()
        {
            var shell = new CommandShell();
            bool parsed = false;
            float value = 0f;

            shell.AddCommand("parse", args =>
            {
                parsed = args[0].TryGetFloat(out value);
            }, 1, 1);

            shell.RunCommand("parse 12.5");

            Assert.That(parsed, Is.True);
            Assert.That(value, Is.EqualTo(12.5f));
            Assert.That(shell.IssuedErrorMessage, Is.Null);
        }

        [TestCase("true", true)]
        [TestCase("FALSE", false)]
        public void TryGetBool_WhenValueIsSupported_ReturnsParsedValue(string input, bool expected)
        {
            var shell = new CommandShell();
            bool parsed = false;
            bool value = false;

            shell.AddCommand("parse", args =>
            {
                parsed = args[0].TryGetBool(out value);
            }, 1, 1);

            shell.RunCommand($"parse {input}");

            Assert.That(parsed, Is.True);
            Assert.That(value, Is.EqualTo(expected));
            Assert.That(shell.IssuedErrorMessage, Is.Null);
        }
    }
}
