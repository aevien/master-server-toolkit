using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MasterServerToolkit.CommandTerminal
{
    public struct CommandInfo
    {
        public Action<CommandArg[]> Proc { get; set; }
        public int MaxArgCount { get; set; }
        public int MinArgCount { get; set; }
        public string Help { get; set; }
    }

    public struct CommandArg
    {
        private CommandShell shell;

        public string String { get; set; }

        internal CommandShell Shell
        {
            get => shell;
            set => shell = value;
        }

        /// <summary>
        /// Tries to parse this argument as an invariant-culture integer.
        /// </summary>
        public bool TryGetInt(out int value)
        {
            if (int.TryParse(String, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            TypeError("int");
            return false;
        }

        /// <summary>
        /// Tries to parse this argument as a floating-point number.
        /// Invariant culture is preferred, with current culture retained as a compatibility fallback.
        /// </summary>
        public bool TryGetFloat(out float value)
        {
            if (float.TryParse(String, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            if (float.TryParse(String, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            TypeError("float");
            return false;
        }

        /// <summary>
        /// Tries to parse this argument as a case-insensitive true or false value.
        /// </summary>
        public bool TryGetBool(out bool value)
        {
            if (string.Compare(String, "TRUE", StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }

            if (string.Compare(String, "FALSE", StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = false;
                return true;
            }

            value = false;
            TypeError("bool");
            return false;
        }

        public override string ToString()
        {
            return String;
        }

        private void TypeError(string expectedType)
        {
            CommandShell targetShell = shell ?? Terminal.Shell;

            targetShell?.IssueErrorMessage(
                "Incorrect type for {0}, expected <{1}>",
                String,
                expectedType
            );
        }
    }

    public class CommandShell
    {
        private readonly Dictionary<string, CommandInfo> commands =
            new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);

        private readonly List<CommandArg> arguments = new List<CommandArg>();

        public event Action CommandsChanged;

        public string IssuedErrorMessage { get; private set; }
        public Dictionary<string, CommandInfo> Commands => commands;

        public void RunCommand(string line)
        {
            IssuedErrorMessage = null;
            arguments.Clear();

            if (!TryParseArguments(line, arguments, out string parseError))
            {
                IssueErrorMessage(parseError);
                return;
            }

            if (arguments.Count == 0)
                return;

            string commandName = arguments[0].String;
            arguments.RemoveAt(0);
            RunCommand(commandName, arguments.ToArray());
        }

        public void RunCommand(string commandName, CommandArg[] args)
        {
            IssuedErrorMessage = null;

            if (string.IsNullOrWhiteSpace(commandName))
                return;

            if (!commands.TryGetValue(commandName, out CommandInfo command))
            {
                IssueErrorMessage("Command {0} could not be found", commandName);
                return;
            }

            args ??= Array.Empty<CommandArg>();
            AttachShell(args);

            int argCount = args.Length;
            string errorMessage = null;
            int requiredArg = 0;

            if (argCount < command.MinArgCount)
            {
                errorMessage = command.MinArgCount == command.MaxArgCount ? "exactly" : "at least";
                requiredArg = command.MinArgCount;
            }
            else if (command.MaxArgCount > -1 && argCount > command.MaxArgCount)
            {
                errorMessage = command.MinArgCount == command.MaxArgCount ? "exactly" : "at most";
                requiredArg = command.MaxArgCount;
            }

            if (errorMessage != null)
            {
                string pluralFix = requiredArg == 1 ? string.Empty : "s";
                IssueErrorMessage(
                    "{0} requires {1} {2} argument{3}",
                    commandName,
                    errorMessage,
                    requiredArg,
                    pluralFix
                );
                return;
            }

            if (command.Proc == null)
            {
                IssueErrorMessage("Command {0} has no handler", commandName);
                return;
            }

            try
            {
                command.Proc(args);
            }
            catch (Exception e)
            {
                IssueErrorMessage("Command {0} failed: {1}", commandName, e.Message);
                Terminal.Log(TerminalLogType.Exception, e.ToString());
            }
        }

        public bool AddCommand(string name, CommandInfo info)
        {
            return AddCommand(name, info, false);
        }

        public bool AddCommand(string name, CommandInfo info, bool replaceExisting)
        {
            ClearError();
            name = NormalizeCommandName(name);

            if (string.IsNullOrEmpty(name))
            {
                IssueErrorMessage("Command name is empty.");
                return false;
            }

            if (info.Proc == null)
            {
                IssueErrorMessage("Command {0} has no handler.", name);
                return false;
            }

            if (info.MaxArgCount > -1 && info.MinArgCount > info.MaxArgCount)
            {
                IssueErrorMessage("Command {0} min argument count is greater than max argument count.", name);
                return false;
            }

            if (commands.ContainsKey(name) && !replaceExisting)
            {
                IssueErrorMessage("Command {0} is already defined.", name);
                return false;
            }

            commands[name] = info;
            CommandsChanged?.Invoke();
            return true;
        }

        public bool AddCommand(string name,
                               Action<CommandArg[]> proc,
                               int minArgCount = 0,
                               int maxArgCount = -1,
                               string help = "")
        {
            return AddCommand(name, proc, minArgCount, maxArgCount, help, false);
        }

        public bool AddCommand(string name,
                               Action<CommandArg[]> proc,
                               int minArgCount,
                               int maxArgCount,
                               string help,
                               bool replaceExisting)
        {
            var info = new CommandInfo()
            {
                Proc = proc,
                MinArgCount = Math.Max(0, minArgCount),
                MaxArgCount = maxArgCount,
                Help = help ?? string.Empty
            };

            return AddCommand(name, info, replaceExisting);
        }

        public bool RemoveCommand(string name)
        {
            ClearError();
            name = NormalizeCommandName(name);

            if (string.IsNullOrEmpty(name))
                return false;

            bool removed = commands.Remove(name);

            if (removed)
                CommandsChanged?.Invoke();

            return removed;
        }

        public void IssueErrorMessage(string format, params object[] message)
        {
            IssuedErrorMessage = string.Format(format, message);
        }

        public void ClearError()
        {
            IssuedErrorMessage = null;
        }

        private void AttachShell(CommandArg[] args)
        {
            if (args == null)
                return;

            for (int i = 0; i < args.Length; i++)
                args[i].Shell = this;
        }

        public bool TryParseArguments(string line, IList<CommandArg> output, out string error)
        {
            error = null;

            if (output == null)
            {
                error = "Output argument list is null.";
                return false;
            }

            output.Clear();

            if (string.IsNullOrWhiteSpace(line))
                return true;

            var current = new StringBuilder();
            bool inQuotes = false;
            bool escaping = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (escaping)
                {
                    current.Append(c);
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    AddCurrentArgument(output, current);
                    continue;
                }

                current.Append(c);
            }

            if (escaping)
                current.Append('\\');

            if (inQuotes)
            {
                error = "Command contains an unterminated quote.";
                return false;
            }

            AddCurrentArgument(output, current);
            return true;
        }

        private void AddCurrentArgument(IList<CommandArg> output, StringBuilder current)
        {
            if (current.Length == 0)
                return;

            output.Add(new CommandArg { String = current.ToString(), Shell = this });
            current.Length = 0;
        }

        private string NormalizeCommandName(string name)
        {
            return (name ?? string.Empty).Trim();
        }
    }
}
