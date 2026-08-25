using System.Collections.Generic;

namespace MasterServerToolkit.CommandTerminal
{
    public class CommandHistory
    {
        private readonly List<string> history = new List<string>();
        private int position;
        private int capacity = 100;

        public IReadOnlyList<string> Entries => history;
        public int Count => history.Count;
        public int Capacity => capacity;

        public void SetCapacity(int value)
        {
            capacity = value < 1 ? 1 : value;
            TrimToCapacity();
            position = history.Count;
        }

        public void Push(string commandString)
        {
            if (string.IsNullOrWhiteSpace(commandString))
                return;

            history.Add(commandString);
            TrimToCapacity();
            position = history.Count;
        }

        public string Next()
        {
            position++;

            if (position >= history.Count)
            {
                position = history.Count;
                return string.Empty;
            }

            return history[position];
        }

        public string Previous()
        {
            if (history.Count == 0)
                return string.Empty;

            position--;

            if (position < 0)
                position = 0;

            return history[position];
        }

        public void ResetCursor()
        {
            position = history.Count;
        }

        public void Clear()
        {
            history.Clear();
            position = 0;
        }

        private void TrimToCapacity()
        {
            int overflow = history.Count - capacity;

            if (overflow > 0)
                history.RemoveRange(0, overflow);
        }
    }
}
