using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MasterServerToolkit.CommandTerminal
{
    internal static class BuiltinCommands
    {
        public static void Register(CommandShell shell)
        {
            if (shell == null)
                return;

            shell.AddCommand("noop", CommandNoop, 0, 0, "Does nothing", true);
            shell.AddCommand("clear", CommandClear, 0, 0, "Clears the terminal log buffer", true);
            shell.AddCommand("help", CommandHelp, 0, 1, "Lists commands or displays help for one command", true);
            shell.AddCommand("time", CommandTime, 1, -1, "Times the execution of another command", true);
            shell.AddCommand("echo", CommandEcho, 0, -1, "Outputs text", true);
            shell.AddCommand("print", CommandEcho, 0, -1, "Outputs text", true);
            shell.AddCommand("logs", CommandLogs, 0, 0, "Shows terminal log buffer counters", true);

#if DEBUG
            shell.AddCommand("trace", CommandTrace, 0, 0, "Outputs the stack trace of the previous log entry", true);
#endif

            shell.AddCommand("quit", CommandQuit, 0, 0, "Quits the running application", true);
        }

        private static void CommandNoop(CommandArg[] args) { }

        private static void CommandClear(CommandArg[] args)
        {
            Terminal.Buffer?.Clear();
        }

        private static void CommandHelp(CommandArg[] args)
        {
            CommandShell shell = Terminal.Shell;

            if (shell == null)
                return;

            if (args.Length == 0)
            {
                var names = new List<string>(shell.Commands.Keys);
                names.Sort();

                foreach (string name in names)
                {
                    CommandInfo command = shell.Commands[name];
                    Terminal.Log("{0} - {1}", name, command.Help);
                }

                return;
            }

            string commandName = args[0].String;

            if (!shell.Commands.TryGetValue(commandName, out CommandInfo info))
            {
                shell.IssueErrorMessage("Command {0} could not be found.", commandName);
                return;
            }

            if (string.IsNullOrWhiteSpace(info.Help))
                Terminal.Log("{0} does not provide help text.", commandName);
            else
                Terminal.Log(info.Help);
        }

        private static void CommandTime(CommandArg[] args)
        {
            var sw = new Stopwatch();
            sw.Start();

            Terminal.Shell.RunCommand(JoinArguments(args));

            sw.Stop();
            Terminal.Log("Time: {0}ms", (double)sw.ElapsedTicks / 10000);
        }

        private static void CommandEcho(CommandArg[] args)
        {
            Terminal.Log(JoinArguments(args));
        }

        private static void CommandLogs(CommandArg[] args)
        {
            Terminal terminal = Terminal.Instance;

            if (terminal == null || Terminal.Buffer == null)
                return;

            TerminalSearchResult search = terminal.GetCurrentSearchResult();
            Terminal.Log(
                "Stored {0}/{1} lines. Visible limit: {2}. Search matches: {3}. Search page: {4}/{5}.",
                Terminal.Buffer.Count,
                Terminal.Buffer.Capacity,
                terminal.MaxVisibleLines,
                search.TotalMatches,
                search.PageCount == 0 ? 0 : search.PageIndex + 1,
                search.PageCount);
        }

#if DEBUG
        private static void CommandTrace(CommandArg[] args)
        {
            CommandLog buffer = Terminal.Buffer;

            if (buffer == null || buffer.Logs.Count < 2)
            {
                Terminal.Log("Nothing to trace.");
                return;
            }

            LogItem logItem = buffer.Logs[buffer.Logs.Count - 2];

            if (string.IsNullOrEmpty(logItem.stack_trace))
                Terminal.Log("{0} (no trace)", logItem.message);
            else
                Terminal.Log(logItem.stack_trace);
        }
#endif

        private static void CommandQuit(CommandArg[] args)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private static string JoinArguments(CommandArg[] args)
        {
            var sb = new StringBuilder();
            int argLength = args == null ? 0 : args.Length;

            for (int i = 0; i < argLength; i++)
            {
                sb.Append(args[i].String);

                if (i < argLength - 1)
                    sb.Append(' ');
            }

            return sb.ToString();
        }
    }
}
