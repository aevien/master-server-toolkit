using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.CommandTerminal
{
    public enum TerminalLogType
    {
        Error = LogType.Error,
        Assert = LogType.Assert,
        Warning = LogType.Warning,
        Message = LogType.Log,
        Exception = LogType.Exception,
        Input,
        ShellMessage
    }

    public struct LogItem
    {
        public long sequenceId;
        public DateTime timestamp;
        public TerminalLogType type;
        public string message;
        public string stack_trace;

        public string Message => message;
        public string StackTrace => stack_trace;
        public TerminalLogType Type => type;
    }

    public sealed class TerminalSearchResult
    {
        public TerminalSearchResult()
            : this(string.Empty, Array.Empty<LogItem>(), 0, 0, 0, 0, 0, -1)
        {
        }

        public TerminalSearchResult(
            string query,
            IReadOnlyList<LogItem> entries,
            int totalMatches,
            int pageIndex,
            int pageCount,
            int pageSize,
            int startMatchIndex,
            int endMatchIndex)
        {
            Query = query ?? string.Empty;
            Entries = entries ?? Array.Empty<LogItem>();
            TotalMatches = totalMatches;
            PageIndex = pageIndex;
            PageCount = pageCount;
            PageSize = pageSize;
            StartMatchIndex = startMatchIndex;
            EndMatchIndex = endMatchIndex;
        }

        public string Query { get; }
        public IReadOnlyList<LogItem> Entries { get; }
        public int TotalMatches { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public int PageSize { get; }
        public int StartMatchIndex { get; }
        public int EndMatchIndex { get; }
        public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
        public bool HasMatches => TotalMatches > 0;
        public bool IsFirstPage => PageIndex <= 0;
        public bool IsLastPage => PageCount == 0 || PageIndex >= PageCount - 1;
    }

    public class CommandLog
    {
        private readonly List<LogItem> logs = new List<LogItem>();
        private int max_items;
        private long nextSequenceId;
        private int version;
        private string cachedSearchQuery;
        private int cachedSearchPageIndex;
        private int cachedSearchPageSize;
        private int cachedSearchVersion = -1;
        private TerminalSearchResult cachedSearchResult;

        public event Action<LogItem> EntryAdded;
        public event Action Cleared;

        public List<LogItem> Logs => logs;
        public int Count => logs.Count;
        public int Capacity => max_items;
        public int Version => version;

        public CommandLog(int max_items)
        {
            SetCapacity(max_items);
        }

        public void SetCapacity(int capacity)
        {
            max_items = Mathf.Max(1, capacity);
            TrimToCapacity();
        }

        public void HandleLog(string message, TerminalLogType type)
        {
            HandleLog(message, string.Empty, type);
        }

        public void HandleLog(string message, string stack_trace, TerminalLogType type)
        {
            var log = new LogItem()
            {
                sequenceId = ++nextSequenceId,
                timestamp = DateTime.Now,
                message = message ?? string.Empty,
                stack_trace = stack_trace ?? string.Empty,
                type = type
            };

            logs.Add(log);
            if (!TrimToCapacity())
                version++;

            EntryAdded?.Invoke(log);
        }

        public IReadOnlyList<LogItem> Latest(int maxCount)
        {
            return Latest(maxCount, 0);
        }

        public IReadOnlyList<LogItem> Latest(int maxCount, int offsetFromLatest)
        {
            maxCount = Mathf.Max(0, maxCount);
            offsetFromLatest = Mathf.Max(0, offsetFromLatest);

            if (maxCount == 0 || logs.Count == 0)
                return Array.Empty<LogItem>();

            int available = Mathf.Max(0, logs.Count - offsetFromLatest);

            if (available == 0)
                return Array.Empty<LogItem>();

            int count = Mathf.Min(maxCount, available);
            int start = available - count;
            var result = new LogItem[count];
            logs.CopyTo(start, result, 0, count);
            return result;
        }

        public TerminalSearchResult Search(string query, int pageIndex, int pageSize)
        {
            query = query ?? string.Empty;
            pageSize = Mathf.Max(1, pageSize);
            int requestedPageIndex = pageIndex;

            if (cachedSearchResult != null &&
                cachedSearchVersion == version &&
                cachedSearchPageIndex == requestedPageIndex &&
                cachedSearchPageSize == pageSize &&
                string.Equals(cachedSearchQuery, query, StringComparison.Ordinal))
            {
                return cachedSearchResult;
            }

            TerminalSearchResult result;

            if (string.IsNullOrWhiteSpace(query) || logs.Count == 0)
            {
                result = new TerminalSearchResult(query, Array.Empty<LogItem>(), 0, 0, 0, pageSize, 0, -1);
                CacheSearch(query, requestedPageIndex, pageSize, result);
                return result;
            }

            var matches = new List<LogItem>();

            for (int i = 0; i < logs.Count; i++)
            {
                string message = logs[i].message ?? string.Empty;
                if (message.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches.Add(logs[i]);
            }

            int totalMatches = matches.Count;

            if (totalMatches == 0)
            {
                result = new TerminalSearchResult(query, Array.Empty<LogItem>(), 0, 0, 0, pageSize, 0, -1);
                CacheSearch(query, requestedPageIndex, pageSize, result);
                return result;
            }

            int pageCount = Mathf.CeilToInt(totalMatches / (float)pageSize);

            if (pageIndex < 0)
                pageIndex = pageCount - 1;

            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            int start = pageIndex * pageSize;
            int count = Mathf.Min(pageSize, totalMatches - start);
            var page = new LogItem[count];
            matches.CopyTo(start, page, 0, count);

            result = new TerminalSearchResult(
                query,
                page,
                totalMatches,
                pageIndex,
                pageCount,
                pageSize,
                start + 1,
                start + count);
            CacheSearch(query, requestedPageIndex, pageSize, result);
            return result;
        }

        public void Clear()
        {
            logs.Clear();
            version++;
            Cleared?.Invoke();
        }

        private bool TrimToCapacity()
        {
            int overflow = logs.Count - max_items;

            if (overflow > 0)
            {
                logs.RemoveRange(0, overflow);
                version++;
                return true;
            }

            return false;
        }

        private void CacheSearch(string query, int pageIndex, int pageSize, TerminalSearchResult result)
        {
            cachedSearchQuery = query;
            cachedSearchPageIndex = pageIndex;
            cachedSearchPageSize = pageSize;
            cachedSearchVersion = version;
            cachedSearchResult = result;
        }
    }
}
