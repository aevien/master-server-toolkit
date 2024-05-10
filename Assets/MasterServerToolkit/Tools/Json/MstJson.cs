/*
Copyright (c) 2010-2021 Matt Schoen
Copyright (c) 2022-Next years Aevien

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

//#define JSONOBJECT_DISABLE_PRETTY_PRINT // Use when you no longer need to read JSON to disable pretty Print system-wide
//#define JSONOBJECT_USE_FLOAT //Use floats for numbers instead of doubles (enable if you don't need support for doubles and want to cut down on significant digits in output)
//#define JSONOBJECT_POOLING //Create MstJsons from a pool and prevent finalization by returning objects to the pool

// ReSharper disable ArrangeAccessorOwnerBody
// ReSharper disable MergeConditionalExpression
// ReSharper disable UseStringInterpolation

#if UNITY_2 || UNITY_3 || UNITY_4 || UNITY_5 || UNITY_5_3_OR_NEWER
#define USING_UNITY
#endif

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

#if USING_UNITY
using UnityEngine;
using Debug = UnityEngine.Debug;
using MasterServerToolkit.Extensions;
#endif

namespace MasterServerToolkit.Json
{
    public class MstJson : IEnumerable
    {
        public delegate void FieldNotFoundCallbackHandler(string fieldName);
        public delegate void GetFieldResponseHandler(MstJson jsonObject);
        public delegate void AddJsonContentsHandler(MstJson jsonObject);

        public enum ValueType
        {
            Null,
            String,
            Number,
            Object,
            Array,
            Bool,
            Baked
        }

#if JSONOBJECT_POOLING
		private const int _maxPoolSize = 100000;
		private static readonly Queue<MstJson> _pool = new Queue<MstJson>();
		private static bool _poolingEnabled = true;

		private bool _isPooled;
#endif

#if !JSONOBJECT_DISABLE_PRETTY_PRINT
        private const string _newline = "\r\n";
        private const string _tab = "\t";
#endif

        private const string _infinity = "Infinity";
        private const string _negativeInfinity = "-Infinity";
        private const string _naN = "NaN";
        private const string _true = "true";
        private const string _false = "false";
        private const string _null = "null";

        private const float _maxFrameTime = 0.008f;
        private static readonly Stopwatch _printWatch = new Stopwatch();
        private static readonly char[] _whitespace = { ' ', '\r', '\n', '\t', '\uFEFF', '\u0009' };

        public bool IsContainer => Type == ValueType.Array || Type == ValueType.Object;
        public ValueType Type { get; private set; } = ValueType.Null;
        public int Count => Values == null ? 0 : Values.Count;
        public static MstJson NullObject => Create(ValueType.Null);
        public static MstJson EmptyObject => Create(ValueType.Object);
        public static MstJson EmptyArray => Create(ValueType.Array);
        public bool IsNumber => Type == ValueType.Number;
        public bool IsNull => Type == ValueType.Null;
        public bool IsString => Type == ValueType.String;
        public bool IsBool => Type == ValueType.Bool;
        public bool IsArray => Type == ValueType.Array;
        public bool IsObject => Type == ValueType.Object;
        public bool IsBaked => Type == ValueType.Baked;
        public List<MstJson> Values { get; set; } = new List<MstJson>();
        public List<string> Keys { get; set; } = new List<string>();
        public string StringValue { get; set; }
        public bool IsInteger { get; set; }
        public long LongValue { get; set; }
        public bool BoolValue { get; set; }
#if JSONOBJECT_USE_FLOAT
		public float FloatValue;
		public double DoubleValue {
			get {
				return FloatValue;
			}
			set {
				FloatValue = (float) value;
			}
		}
#else
        public double DoubleValue;
        public float FloatValue
        {
            get
            {
                return (float)DoubleValue;
            }
            set
            {
                DoubleValue = value;
            }
        }
#endif

        public int IntValue
        {
            get
            {
                return (int)LongValue;
            }
            set
            {
                LongValue = value;
            }
        }

        public T EnumValue<T>() where T : Enum
        {
            return (T)Enum.Parse(typeof(T), StringValue);
        }

        public MstJson(ValueType type) { this.Type = type; }

        public MstJson(bool value)
        {
            Type = ValueType.Bool;
            BoolValue = value;
        }

        public MstJson(float value)
        {
            Type = ValueType.Number;
#if JSONOBJECT_USE_FLOAT
			FloatValue = value;
#else
            DoubleValue = value;
#endif
        }

        public MstJson(double value)
        {
            Type = ValueType.Number;
#if JSONOBJECT_USE_FLOAT
			FloatValue = (float)value;
#else
            DoubleValue = value;
#endif
        }

        public MstJson(int value)
        {
            Type = ValueType.Number;
            LongValue = value;
            IsInteger = true;
#if JSONOBJECT_USE_FLOAT
			FloatValue = value;
#else
            DoubleValue = value;
#endif
        }

        public MstJson(long value)
        {
            Type = ValueType.Number;
            LongValue = value;
            IsInteger = true;
#if JSONOBJECT_USE_FLOAT
			FloatValue = value;
#else
            DoubleValue = value;
#endif
        }

        public MstJson(Dictionary<string, string> dictionary)
        {
            Type = ValueType.Object;
            Keys = new List<string>();
            Values = new List<MstJson>();
            foreach (KeyValuePair<string, string> kvp in dictionary)
            {
                Keys.Add(kvp.Key);
                Values.Add(Create(kvp.Value));
            }
        }

        public MstJson(Dictionary<string, MstJson> dictionary)
        {
            Type = ValueType.Object;
            Keys = new List<string>();
            Values = new List<MstJson>();
            foreach (KeyValuePair<string, MstJson> kvp in dictionary)
            {
                Keys.Add(kvp.Key);
                Values.Add(kvp.Value);
            }
        }

        public MstJson(AddJsonContentsHandler content)
        {
            content.Invoke(this);
        }

        public MstJson(MstJson[] objects)
        {
            Type = ValueType.Array;
            Values = new List<MstJson>(objects);
        }

        public MstJson(List<MstJson> objects)
        {
            Type = ValueType.Array;
            Values = objects;
        }

        public void Absorb(MstJson other)
        {
            var otherList = other.Values;
            if (otherList != null)
            {
                if (Values == null)
                {
                    Values = new List<MstJson>();
                }

                Values.AddRange(otherList);
            }

            var otherKeys = other.Keys;
            if (otherKeys != null)
            {
                if (Keys == null)
                {
                    Keys = new List<string>();
                }

                Keys.AddRange(otherKeys);
            }

            StringValue = other.StringValue;
#if JSONOBJECT_USE_FLOAT
			FloatValue = other.FloatValue;
#else
            DoubleValue = other.DoubleValue;
#endif

            IsInteger = other.IsInteger;
            LongValue = other.LongValue;
            BoolValue = other.BoolValue;
            Type = other.Type;
        }

        public static MstJson Create()
        {
#if JSONOBJECT_POOLING
			lock (_pool) {
				if (_pool.Count > 0) {
					var result = _pool.Dequeue();

					result._isPooled = false;
					return result;
				}
			}
#endif

            return new MstJson();
        }

        public static MstJson Create(ValueType type)
        {
            var jsonObject = Create();
            jsonObject.Type = type;
            return jsonObject;
        }

        public static MstJson Create(bool value)
        {
            var jsonObject = Create();
            jsonObject.Type = ValueType.Bool;
            jsonObject.BoolValue = value;
            return jsonObject;
        }

        public static MstJson Create(float value)
        {
            var jsonObject = Create();
            jsonObject.Type = ValueType.Number;
#if JSONOBJECT_USE_FLOAT
			jsonObject.FloatValue = value;
#else
            jsonObject.DoubleValue = value;
#endif

            return jsonObject;
        }

        public static MstJson Create(double value)
        {
            var jsonObject = Create();
            jsonObject.Type = ValueType.Number;
#if JSONOBJECT_USE_FLOAT
			jsonObject.FloatValue = (float)value;
#else
            jsonObject.DoubleValue = value;
#endif

            return jsonObject;
        }

        public static MstJson Create(int value)
        {
            var jsonObject = Create();
            jsonObject.Type = ValueType.Number;
            jsonObject.IsInteger = true;
            jsonObject.LongValue = value;
#if JSONOBJECT_USE_FLOAT
			jsonObject.FloatValue = value;
#else
            jsonObject.DoubleValue = value;
#endif

            return jsonObject;
        }

        public static MstJson Create(long value)
        {
            var jsonObject = Create();
            jsonObject.Type = ValueType.Number;
            jsonObject.IsInteger = true;
            jsonObject.LongValue = value;
#if JSONOBJECT_USE_FLOAT
			jsonObject.FloatValue = value;
#else
            jsonObject.DoubleValue = value;
#endif

            return jsonObject;
        }

        public static MstJson Create(string value)
        {
            string parsed = value;

            if (!string.IsNullOrEmpty(parsed))
            {
                if (parsed.StartsWith('\"'))
                {
                    parsed = parsed.Substring(1);
                }

                if (parsed.EndsWith('\"'))
                {
                    parsed = parsed.Substring(0, parsed.Length - 1);
                }
            }

            var jsonObject = Create();
            jsonObject.Type = ValueType.String;
            jsonObject.StringValue = parsed;
            return jsonObject;
        }

        public static MstJson CreateBakedObject(string value)
        {
            var bakedObject = Create();
            bakedObject.Type = ValueType.Baked;
            bakedObject.StringValue = value;
            return bakedObject;
        }

        /// <summary>
        /// Create a MstJson (using pooling if enabled) using a string containing valid JSON
        /// </summary>
        /// <param name="json">A string containing valid JSON to be parsed into objects</param>
        /// <param name="offset">An offset into the string at which to start parsing</param>
        /// <param name="endOffset">The length of the string after the offset to parse
        /// Specify a length of -1 (default) to use the full string length</param>
        /// <param name="maxDepth">The maximum depth for the parser to search.</param>
        /// <param name="storeExcessLevels">Whether to store levels beyond maxDepth in baked MstJsons</param>
        /// <returns>A MstJson containing the parsed data</returns>
        public static MstJson Create(string json, int offset = 0, int endOffset = -1, int maxDepth = -1, bool storeExcessLevels = false)
        {
            var jsonObject = Create();
            Parse(json, ref offset, endOffset, jsonObject, maxDepth, storeExcessLevels);
            return jsonObject;
        }

        public static MstJson Create(AddJsonContentsHandler content)
        {
            var jsonObject = Create();
            content.Invoke(jsonObject);
            return jsonObject;
        }

        public static MstJson Create(MstJson[] objects)
        {
            var jsonObject = EmptyArray;
            jsonObject.Values.AddRange(objects);
            return jsonObject;
        }

        public static MstJson Create(List<MstJson> objects)
        {
            var jsonObject = EmptyArray;
            jsonObject.Values.AddRange(objects);
            return jsonObject;
        }

        public static MstJson Create(Dictionary<string, string> dictionary)
        {
            var jsonObject = EmptyObject;

            foreach (var kvp in dictionary)
            {
                jsonObject.AddField(kvp.Key, kvp.Value);
            }

            return jsonObject;
        }

        public static MstJson Create(Dictionary<string, MstJson> dictionary)
        {
            var jsonObject = EmptyObject;

            foreach (var kvp in dictionary)
            {
                jsonObject.AddField(kvp.Key, kvp.Value);
            }

            return jsonObject;
        }

        /// <summary>
        /// Create a MstJson (using pooling if enabled) using a string containing valid JSON
        /// </summary>
        /// <param name="jsonString">A string containing valid JSON to be parsed into objects</param>
        /// <param name="offset">An offset into the string at which to start parsing</param>
        /// <param name="endOffset">The length of the string after the offset to parse
        /// Specify a length of -1 (default) to use the full string length</param>
        /// <param name="maxDepth">The maximum depth for the parser to search.</param>
        /// <param name="storeExcessLevels">Whether to store levels beyond maxDepth in baked MstJsons</param>
        /// <returns>A MstJson containing the parsed data</returns>
        public static IEnumerable<MstJsonParseResult> CreateAsync(string jsonString, int offset = 0, int endOffset = -1, int maxDepth = -1, bool storeExcessLevels = false)
        {
            var jsonObject = Create();
            _printWatch.Reset();
            _printWatch.Start();
            foreach (var e in ParseAsync(jsonString, offset, endOffset, jsonObject, maxDepth, storeExcessLevels))
            {
                if (e.pause)
                    yield return e;

                offset = e.offset;
            }

            yield return new MstJsonParseResult(jsonObject, offset, false);
        }

        public MstJson() { }

        /// <summary>
        /// Construct a new MstJson using a string containing valid JSON
        /// </summary>
        /// <param name="json">A string containing valid JSON to be parsed into objects</param>
        /// <param name="offset">An offset into the string at which to start parsing</param>
        /// <param name="endOffset">The length of the string after the offset to parse
        /// Specify a length of -1 (default) to use the full string length</param>
        /// <param name="maxDepth">The maximum depth for the parser to search.</param>
        /// <param name="storeExcessLevels">Whether to store levels beyond maxDepth in baked MstJsons</param>
        public MstJson(string json, int offset = 0, int endOffset = -1, int maxDepth = -1, bool storeExcessLevels = false)
        {
            Parse(json, ref offset, endOffset, this, maxDepth, storeExcessLevels);
        }

        // ReSharper disable UseNameofExpression
        static bool BeginParse(string inputString, int offset, ref int endOffset, MstJson container, int maxDepth, bool storeExcessLevels)
        {
            if (container == null)
                throw new ArgumentNullException("container");

            if (maxDepth == 0)
            {
                if (storeExcessLevels)
                {
                    container.StringValue = inputString;
                    container.Type = ValueType.Baked;
                }
                else
                {
                    container.Type = ValueType.Null;
                }

                return false;
            }

            var stringLength = inputString.Length;
            if (endOffset == -1)
                endOffset = stringLength - 1;

            if (string.IsNullOrEmpty(inputString))
            {
                return false;
            }

            if (endOffset >= stringLength)
            {
                throw new ArgumentException("Cannot parse if end offset is greater than or equal to string length", "endOffset");
            }

            if (offset > endOffset)
            {
                throw new ArgumentException("Cannot parse if offset is greater than or equal to end offset", "offset");
            }

            return true;
        }

        static void Parse(string json, ref int offset, int endOffset, MstJson container, int maxDepth,
            bool storeExcessLevels, int depth = 0, bool isRoot = true)
        {
            if (!BeginParse(json, offset, ref endOffset, container, maxDepth, storeExcessLevels))
                return;

            var startOffset = offset;
            var quoteStart = 0;
            var quoteEnd = 0;
            var lastValidOffset = offset;
            var openQuote = false;
            var bakeDepth = 0;

            while (offset <= endOffset)
            {
                var currentCharacter = json[offset++];
                if (Array.IndexOf(_whitespace, currentCharacter) > -1)
                    continue;

                MstJson newContainer;
                switch (currentCharacter)
                {
                    case '\\':
                        offset++;
                        break;
                    case '{':
                        if (openQuote)
                            break;

                        if (maxDepth >= 0 && depth >= maxDepth)
                        {
                            bakeDepth++;
                            break;
                        }

                        newContainer = container;
                        if (!isRoot)
                        {
                            newContainer = Create();
                            SafeAddChild(container, newContainer);
                        }

                        newContainer.Type = ValueType.Object;
                        Parse(json, ref offset, endOffset, newContainer, maxDepth, storeExcessLevels, depth + 1, false);

                        break;
                    case '[':
                        if (openQuote)
                            break;

                        if (maxDepth >= 0 && depth >= maxDepth)
                        {
                            bakeDepth++;
                            break;
                        }

                        newContainer = container;
                        if (!isRoot)
                        {
                            newContainer = Create();
                            SafeAddChild(container, newContainer);
                        }

                        newContainer.Type = ValueType.Array;
                        Parse(json, ref offset, endOffset, newContainer, maxDepth, storeExcessLevels, depth + 1, false);

                        break;
                    case '}':
                        if (!ParseObjectEnd(json, offset, openQuote, container, startOffset, lastValidOffset, maxDepth, storeExcessLevels, depth, ref bakeDepth))
                            return;

                        break;
                    case ']':
                        if (!ParseArrayEnd(json, offset, openQuote, container, startOffset, lastValidOffset, maxDepth, storeExcessLevels, depth, ref bakeDepth))
                            return;

                        break;
                    case '"':
                        ParseQuote(ref openQuote, offset, ref quoteStart, ref quoteEnd);
                        break;
                    case ':':
                        if (!ParseColon(json, openQuote, container, ref startOffset, offset, quoteStart, quoteEnd, bakeDepth))
                            return;

                        break;
                    case ',':
                        if (!ParseComma(json, openQuote, container, ref startOffset, offset, lastValidOffset, bakeDepth))
                            return;

                        break;
                }

                lastValidOffset = offset - 1;
            }
        }

        static IEnumerable<MstJsonParseResult> ParseAsync(string inputString, int offset, int endOffset, MstJson container,
            int maxDepth, bool storeExcessLevels, int depth = 0, bool isRoot = true)
        {
            if (!BeginParse(inputString, offset, ref endOffset, container, maxDepth, storeExcessLevels))
                yield break;

            var startOffset = offset;
            var quoteStart = 0;
            var quoteEnd = 0;
            var lastValidOffset = offset;
            var openQuote = false;
            var bakeDepth = 0;
            while (offset <= endOffset)
            {
                if (_printWatch.Elapsed.TotalSeconds > _maxFrameTime)
                {
                    _printWatch.Reset();
                    yield return new MstJsonParseResult(container, offset, true);
                    _printWatch.Start();
                }

                var currentCharacter = inputString[offset++];
                if (Array.IndexOf(_whitespace, currentCharacter) > -1)
                    continue;

                MstJson newContainer;
                switch (currentCharacter)
                {
                    case '\\':
                        offset++;
                        break;
                    case '{':
                        if (openQuote)
                            break;

                        if (maxDepth >= 0 && depth >= maxDepth)
                        {
                            bakeDepth++;
                            break;
                        }

                        newContainer = container;
                        if (!isRoot)
                        {
                            newContainer = Create();
                            SafeAddChild(container, newContainer);
                        }

                        newContainer.Type = ValueType.Object;
                        foreach (var e in ParseAsync(inputString, offset, endOffset, newContainer, maxDepth, storeExcessLevels, depth + 1, false))
                        {
                            if (e.pause)
                                yield return e;

                            offset = e.offset;
                        }

                        break;
                    case '[':
                        if (openQuote)
                            break;

                        if (maxDepth >= 0 && depth >= maxDepth)
                        {
                            bakeDepth++;
                            break;
                        }

                        newContainer = container;
                        if (!isRoot)
                        {
                            newContainer = Create();
                            SafeAddChild(container, newContainer);
                        }

                        newContainer.Type = ValueType.Array;
                        foreach (var e in ParseAsync(inputString, offset, endOffset, newContainer, maxDepth, storeExcessLevels, depth + 1, false))
                        {
                            if (e.pause)
                                yield return e;

                            offset = e.offset;
                        }

                        break;
                    case '}':
                        if (!ParseObjectEnd(inputString, offset, openQuote, container, startOffset, lastValidOffset, maxDepth, storeExcessLevels, depth, ref bakeDepth))
                        {
                            yield return new MstJsonParseResult(container, offset, false);
                            yield break;
                        }

                        break;
                    case ']':
                        if (!ParseArrayEnd(inputString, offset, openQuote, container, startOffset, lastValidOffset, maxDepth, storeExcessLevels, depth, ref bakeDepth))
                        {
                            yield return new MstJsonParseResult(container, offset, false);
                            yield break;
                        }

                        break;
                    case '"':
                        ParseQuote(ref openQuote, offset, ref quoteStart, ref quoteEnd);
                        break;
                    case ':':
                        if (!ParseColon(inputString, openQuote, container, ref startOffset, offset, quoteStart, quoteEnd, bakeDepth))
                        {
                            yield return new MstJsonParseResult(container, offset, false);
                            yield break;
                        }

                        break;
                    case ',':
                        if (!ParseComma(inputString, openQuote, container, ref startOffset, offset, lastValidOffset, bakeDepth))
                        {
                            yield return new MstJsonParseResult(container, offset, false);
                            yield break;
                        }

                        break;
                }

                lastValidOffset = offset - 1;
            }

            yield return new MstJsonParseResult(container, offset, false);
        }

        static void SafeAddChild(MstJson container, MstJson child)
        {
            var list = container.Values;
            if (list == null)
            {
                list = new List<MstJson>();
                container.Values = list;
            }

            list.Add(child);
        }

        void ParseValue(string inputString, int startOffset, int lastValidOffset)
        {
            var firstCharacter = inputString[startOffset];
            do
            {
                if (Array.IndexOf(_whitespace, firstCharacter) > -1)
                {
                    firstCharacter = inputString[++startOffset];
                    continue;
                }

                break;
            } while (true);

            // Use character comparison instead of string compare as performance optimization
            switch (firstCharacter)
            {
                case '"':
                    Type = ValueType.String;

                    // Trim quotes from string values
                    StringValue = UnEscapeString(inputString.Substring(startOffset + 1, lastValidOffset - startOffset - 1));
                    return;
                case 't':
                    Type = ValueType.Bool;
                    BoolValue = true;
                    return;
                case 'f':
                    Type = ValueType.Bool;
                    BoolValue = false;
                    return;
                case 'n':
                    Type = ValueType.Null;
                    return;
                case 'I':
                    Type = ValueType.Number;

#if JSONOBJECT_USE_FLOAT
					FloatValue = float.PositiveInfinity;
#else
                    DoubleValue = double.PositiveInfinity;
#endif

                    return;
                case 'N':
                    Type = ValueType.Number;

#if JSONOBJECT_USE_FLOAT
					FloatValue = float.NaN;
#else
                    DoubleValue = double.NaN;
#endif
                    return;
                case '-':
                    if (inputString[startOffset + 1] == 'I')
                    {
                        Type = ValueType.Number;
#if JSONOBJECT_USE_FLOAT
						FloatValue = float.NegativeInfinity;
#else
                        DoubleValue = double.NegativeInfinity;
#endif
                        return;
                    }

                    break;
            }

            var numericString = inputString.Substring(startOffset, lastValidOffset - startOffset + 1);
            try
            {
                if (numericString.Contains("."))
                {
#if JSONOBJECT_USE_FLOAT
					FloatValue = Convert.ToSingle(numericString, CultureInfo.InvariantCulture);
#else
                    DoubleValue = Convert.ToDouble(numericString, CultureInfo.InvariantCulture);
#endif
                }
                else
                {
                    LongValue = Convert.ToInt64(numericString, CultureInfo.InvariantCulture);
                    IsInteger = true;
#if JSONOBJECT_USE_FLOAT
					FloatValue = LongValue;
#else
                    DoubleValue = LongValue;
#endif
                }

                Type = ValueType.Number;
            }
            catch (OverflowException)
            {
                Type = ValueType.Number;
#if JSONOBJECT_USE_FLOAT
				FloatValue = numericString.StartsWith("-") ? float.NegativeInfinity : float.PositiveInfinity;
#else
                DoubleValue = numericString.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
#endif
            }
            catch (FormatException)
            {
                Type = ValueType.Null;
#if USING_UNITY
                Debug.LogWarning
#else
				Debug.WriteLine
#endif
                    (string.Format("Improper JSON formatting:{0}", numericString));
            }
        }

        static bool ParseObjectEnd(string inputString, int offset, bool openQuote, MstJson container, int startOffset,
            int lastValidOffset, int maxDepth, bool storeExcessLevels, int depth, ref int bakeDepth)
        {
            if (openQuote)
                return true;

            if (container == null)
            {
                Debug.LogError("Parsing error: encountered `}` with no container object");
                return false;
            }

            if (maxDepth >= 0 && depth >= maxDepth)
            {
                bakeDepth--;
                if (bakeDepth == 0)
                {
                    SafeAddChild(container,
                        storeExcessLevels
                            ? CreateBakedObject(inputString.Substring(startOffset, offset - startOffset))
                            : NullObject);
                }

                if (bakeDepth >= 0)
                    return true;
            }

            ParseFinalObjectIfNeeded(inputString, container, startOffset, lastValidOffset);
            return false;
        }

        static bool ParseArrayEnd(string inputString, int offset, bool openQuote, MstJson container,
            int startOffset, int lastValidOffset, int maxDepth, bool storeExcessLevels, int depth, ref int bakeDepth)
        {
            if (openQuote)
                return true;

            if (container == null)
            {
                Debug.LogError("Parsing error: encountered `]` with no container object");
                return false;
            }

            if (maxDepth >= 0 && depth >= maxDepth)
            {
                bakeDepth--;
                if (bakeDepth == 0)
                {
                    SafeAddChild(container,
                        storeExcessLevels
                            ? CreateBakedObject(inputString.Substring(startOffset, offset - startOffset))
                            : NullObject);
                }

                if (bakeDepth >= 0)
                    return true;
            }

            ParseFinalObjectIfNeeded(inputString, container, startOffset, lastValidOffset);
            return false;
        }

        static void ParseQuote(ref bool openQuote, int offset, ref int quoteStart, ref int quoteEnd)
        {
            if (openQuote)
            {
                quoteEnd = offset - 1;
                openQuote = false;
            }
            else
            {
                quoteStart = offset;
                openQuote = true;
            }
        }

        static bool ParseColon(string inputString, bool openQuote, MstJson container,
            ref int startOffset, int offset, int quoteStart, int quoteEnd, int bakeDepth)
        {
            if (openQuote || bakeDepth > 0)
                return true;

            if (container == null)
            {
                Debug.LogError("Parsing error: encountered `:` with no container object");
                return false;
            }

            var keys = container.Keys;
            if (keys == null)
            {
                keys = new List<string>();
                container.Keys = keys;
            }

            container.Keys.Add(inputString.Substring(quoteStart, quoteEnd - quoteStart));
            startOffset = offset;

            return true;
        }

        static bool ParseComma(string inputString, bool openQuote, MstJson container,
            ref int startOffset, int offset, int lastValidOffset, int bakeDepth)
        {
            if (openQuote || bakeDepth > 0)
                return true;

            if (container == null)
            {
                Debug.LogError("Parsing error: encountered `,` with no container object");
                return false;
            }

            ParseFinalObjectIfNeeded(inputString, container, startOffset, lastValidOffset);

            startOffset = offset;
            return true;
        }

        static void ParseFinalObjectIfNeeded(string inputString, MstJson container, int startOffset, int lastValidOffset)
        {
            if (IsClosingCharacter(inputString[lastValidOffset]))
                return;

            var child = Create();
            child.ParseValue(inputString, startOffset, lastValidOffset);
            SafeAddChild(container, child);
        }

        static bool IsClosingCharacter(char character)
        {
            switch (character)
            {
                case '}':
                case ']':
                    return true;
            }

            return false;
        }

        static string EscapeString(string input)
        {
            var escaped = input.Replace("\b", "\\b");
            escaped = escaped.Replace("\f", "\\f");
            escaped = escaped.Replace("\n", "\\n");
            escaped = escaped.Replace("\r", "\\r");
            escaped = escaped.Replace("\t", "\\t");
            escaped = escaped.Replace("\"", "\\\"");
            return escaped;
        }

        static string UnEscapeString(string input)
        {
            var unescaped = input.Replace("\\\"", "\"");
            unescaped = unescaped.Replace("\\b", "\b");
            unescaped = unescaped.Replace("\\f", "\f");
            unescaped = unescaped.Replace("\\n", "\n");
            unescaped = unescaped.Replace("\\r", "\r");
            unescaped = unescaped.Replace("\\t", "\t");
            return unescaped;
        }

        /// <summary>
        /// Merge object right into left recursively
        /// </summary>
        /// <param name="left">The left (base) object</param>
        /// <param name="right">The right (new) object</param>
        static void MergeRecur(MstJson left, MstJson right)
        {
            if (left.Type == ValueType.Null)
            {
                left.Absorb(right);
            }
            else if (left.Type == ValueType.Object && right.Type == ValueType.Object && right.Values != null && right.Keys != null)
            {
                for (var i = 0; i < right.Values.Count; i++)
                {
                    var key = right.Keys[i];
                    if (right[i].IsContainer)
                    {
                        if (left.HasField(key))
                            MergeRecur(left[key], right[i]);
                        else
                            left.AddField(key, right[i]);
                    }
                    else
                    {
                        if (left.HasField(key))
                            left.SetField(key, right[i]);
                        else
                            left.AddField(key, right[i]);
                    }
                }
            }
            else if (left.Type == ValueType.Array && right.Type == ValueType.Array && right.Values != null)
            {
                if (right.Count > left.Count)
                {
#if USING_UNITY
                    Debug.LogError
#else
					Debug.WriteLine
#endif
                        ("Cannot merge arrays when right object has more elements");
                    return;
                }

                for (var i = 0; i < right.Values.Count; i++)
                {
                    if (left[i].Type == right[i].Type)
                    {
                        //Only overwrite with the same type
                        if (left[i].IsContainer)
                            MergeRecur(left[i], right[i]);
                        else
                        {
                            left[i] = right[i];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Convert the MstJson into a string
        /// </summary>
        /// <param name="depth">How many containers deep this run has reached</param>
        /// <param name="builder">The StringBuilder used to build the string</param>
        /// <param name="pretty">Whether this string should be "pretty" and include whitespace for readability</param>
        /// <returns>An enumerator for this function</returns>
        IEnumerable<bool> StringifyAsync(int depth, StringBuilder builder, bool pretty = false)
        {
            if (_printWatch.Elapsed.TotalSeconds > _maxFrameTime)
            {
                _printWatch.Reset();
                yield return true;
                _printWatch.Start();
            }

            switch (Type)
            {
                case ValueType.Baked:
                    builder.Append(StringValue);
                    break;
                case ValueType.String:
                    StringifyString(builder);
                    break;
                case ValueType.Number:
                    StringifyNumber(builder);
                    break;
                case ValueType.Object:
                    var fieldCount = Count;
                    if (fieldCount <= 0)
                    {
                        StringifyEmptyObject(builder);
                        break;
                    }

                    depth++;

                    BeginStringifyObjectContainer(builder, pretty);
                    for (var index = 0; index < fieldCount; index++)
                    {
                        var jsonObject = Values[index];
                        if (jsonObject == null)
                            continue;

                        var key = Keys[index];
                        BeginStringifyObjectField(builder, pretty, depth, key);
                        foreach (var pause in jsonObject.StringifyAsync(depth, builder, pretty))
                        {
                            if (pause)
                                yield return true;
                        }

                        EndStringifyObjectField(builder, pretty);
                    }

                    EndStringifyObjectContainer(builder, pretty, depth);
                    break;
                case ValueType.Array:
                    var arraySize = Count;
                    if (arraySize <= 0)
                    {
                        StringifyEmptyArray(builder);
                        break;
                    }

                    BeginStringifyArrayContainer(builder, pretty);
                    for (var index = 0; index < arraySize; index++)
                    {
                        var jsonObject = Values[index];
                        if (jsonObject == null)
                            continue;

                        BeginStringifyArrayElement(builder, pretty, depth);
                        foreach (var pause in Values[index].StringifyAsync(depth, builder, pretty))
                        {
                            if (pause)
                                yield return true;
                        }

                        EndStringifyArrayElement(builder, pretty);
                    }

                    EndStringifyArrayContainer(builder, pretty, depth);
                    break;
                case ValueType.Bool:
                    StringifyBool(builder);
                    break;
                case ValueType.Null:
                    StringifyNull(builder);
                    break;
            }
        }

        /// <summary>
        /// Convert the MstJson into a string
        /// </summary>
        /// <param name="depth">How many containers deep this run has reached</param>
        /// <param name="builder">The StringBuilder used to build the string</param>
        /// <param name="pretty">Whether this string should be "pretty" and include whitespace for readability</param>
        void Stringify(int depth, StringBuilder builder, bool pretty = false)
        {
            depth++;
            switch (Type)
            {
                case ValueType.Baked:
                    builder.Append(StringValue);
                    break;
                case ValueType.String:
                    StringifyString(builder);
                    break;
                case ValueType.Number:
                    StringifyNumber(builder);
                    break;
                case ValueType.Object:
                    var fieldCount = Count;
                    if (fieldCount <= 0)
                    {
                        StringifyEmptyObject(builder);
                        break;
                    }

                    BeginStringifyObjectContainer(builder, pretty);

                    for (var index = 0; index < fieldCount; index++)
                    {
                        var jsonObject = Values[index];
                        if (jsonObject == null)
                            continue;

                        if (Keys == null || index >= Keys.Count)
                            break;

                        var key = Keys[index];
                        BeginStringifyObjectField(builder, pretty, depth, key);
                        jsonObject.Stringify(depth, builder, pretty);
                        EndStringifyObjectField(builder, pretty);
                    }

                    EndStringifyObjectContainer(builder, pretty, depth);
                    break;
                case ValueType.Array:
                    if (Count <= 0)
                    {
                        StringifyEmptyArray(builder);
                        break;
                    }

                    BeginStringifyArrayContainer(builder, pretty);
                    foreach (var jsonObject in Values)
                    {
                        if (jsonObject == null)
                            continue;

                        BeginStringifyArrayElement(builder, pretty, depth);
                        jsonObject.Stringify(depth, builder, pretty);
                        EndStringifyArrayElement(builder, pretty);
                    }

                    EndStringifyArrayContainer(builder, pretty, depth);
                    break;
                case ValueType.Bool:
                    StringifyBool(builder);
                    break;
                case ValueType.Null:
                    StringifyNull(builder);
                    break;
            }
        }

        void StringifyString(StringBuilder builder)
        {
            builder.AppendFormat("\"{0}\"", EscapeString(StringValue));
        }

        void BeginStringifyObjectContainer(StringBuilder builder, bool pretty)
        {
            builder.Append("{");

#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                builder.Append(_newline);
#endif
        }

        static void StringifyEmptyObject(StringBuilder builder)
        {
            builder.Append("{}");
        }

        void BeginStringifyObjectField(StringBuilder builder, bool pretty, int depth, string key)
        {
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                for (var j = 0; j < depth; j++)
                    builder.Append(_tab); //for a bit more readability
#endif

            builder.AppendFormat("\"{0}\":", key);
        }

        void EndStringifyObjectField(StringBuilder builder, bool pretty)
        {
            builder.Append(",");
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                builder.Append(_newline);
#endif
        }

        void EndStringifyObjectContainer(StringBuilder builder, bool pretty, int depth)
        {
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                builder.Length -= 3;
            else
#endif
                builder.Length--;

#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty && Count > 0)
            {
                builder.Append(_newline);
                for (var j = 0; j < depth - 1; j++)
                    builder.Append(_tab);
            }
#endif

            builder.Append("}");
        }

        static void StringifyEmptyArray(StringBuilder builder)
        {
            builder.Append("[]");
        }

        void BeginStringifyArrayContainer(StringBuilder builder, bool pretty)
        {
            builder.Append("[");
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                builder.Append(_newline);
#endif

        }

        void BeginStringifyArrayElement(StringBuilder builder, bool pretty, int depth)
        {
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                for (var j = 0; j < depth; j++)
                    builder.Append(_tab); //for a bit more readability
#endif
        }

        void EndStringifyArrayElement(StringBuilder builder, bool pretty)
        {
            builder.Append(",");
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                builder.Append(_newline);
#endif
        }

        void EndStringifyArrayContainer(StringBuilder builder, bool pretty, int depth)
        {
#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty)
                builder.Length -= 3;
            else
#endif
                builder.Length--;

#if !JSONOBJECT_DISABLE_PRETTY_PRINT
            if (pretty && Count > 0)
            {
                builder.Append(_newline);
                for (var j = 0; j < depth - 1; j++)
                    builder.Append(_tab);
            }
#endif

            builder.Append("]");
        }

        void StringifyNumber(StringBuilder builder)
        {
            if (IsInteger)
            {
                builder.Append(LongValue.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
#if JSONOBJECT_USE_FLOAT
				if (float.IsNegativeInfinity(FloatValue))
					builder.Append(_negativeInfinity);
				else if (float.IsInfinity(FloatValue))
					builder.Append(_infinity);
				else if (float.IsNaN(FloatValue))
					builder.Append(_naN);
				else
					builder.Append(FloatValue.ToString("R", CultureInfo.InvariantCulture));
#else
                if (double.IsNegativeInfinity(DoubleValue))
                    builder.Append(_negativeInfinity);
                else if (double.IsInfinity(DoubleValue))
                    builder.Append(_infinity);
                else if (double.IsNaN(DoubleValue))
                    builder.Append(_naN);
                else
                    builder.Append(DoubleValue.ToString("R", CultureInfo.InvariantCulture));
#endif
            }
        }

        void StringifyBool(StringBuilder builder)
        {
            builder.Append(BoolValue ? _true : _false);
        }

        static void StringifyNull(StringBuilder builder)
        {
            builder.Append(_null);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(bool value)
        {
            Add(Create(value));
        }

        public void Add(float value)
        {
            Add(Create(value));
        }

        public void Add(double value)
        {
            Add(Create(value));
        }

        public void Add(long value)
        {
            Add(Create(value));
        }

        public void Add(int value)
        {
            Add(Create(value));
        }

        public void Add(string value)
        {
            Add(Create(value));
        }

        public void Add(AddJsonContentsHandler content)
        {
            Add(Create(content));
        }

        public void Add(MstJson jsonObject)
        {
            if (jsonObject == null)
                return;

            // Convert to array to support list
            Type = ValueType.Array;
            if (Values == null)
                Values = new List<MstJson>();

            Values.Add(jsonObject);
        }

        public void AddField(string name, bool value)
        {
            AddField(name, Create(value));
        }

        public void AddField(string name, float value)
        {
            AddField(name, Create(value));
        }

        public void AddField(string name, double value)
        {
            AddField(name, Create(value));
        }

        public void AddField(string name, int value)
        {
            AddField(name, Create(value));
        }

        public void AddField(string name, long value)
        {
            AddField(name, Create(value));
        }

        public void AddField(string name, AddJsonContentsHandler content)
        {
            AddField(name, Create(content));
        }

        public void AddField(string name, string value)
        {
            AddField(name, Create(value));
        }

        public void AddField(string name, MstJson jsonObject)
        {
            if (jsonObject == null)
                return;

            // Convert to object if needed to support fields
            Type = ValueType.Object;

            if (Values == null)
            {
                Values = new List<MstJson>();
            }

            if (Keys == null)
            {
                Keys = new List<string>();
            }

            while (Keys.Count < Values.Count)
            {
                Keys.Add(Keys.Count.ToString(CultureInfo.InvariantCulture));
            }

            Keys.Add(name);
            Values.Add(jsonObject);
        }

        public void SetField(string name, string value)
        {
            SetField(name, Create(value));
        }

        public void SetField(string name, bool value)
        {
            SetField(name, Create(value));
        }

        public void SetField(string name, float value)
        {
            SetField(name, Create(value));
        }

        public void SetField(string name, double value)
        {
            SetField(name, Create(value));
        }

        public void SetField(string name, long value)
        {
            SetField(name, Create(value));
        }

        public void SetField(string name, int value)
        {
            SetField(name, Create(value));
        }

        public void SetField(string name, MstJson jsonObject)
        {
            if (HasField(name))
            {
                Values.Remove(this[name]);
                Keys.Remove(name);
            }

            AddField(name, jsonObject);
        }

        public void RemoveField(string name)
        {
            if (Keys == null || Values == null)
                return;

            if (Keys.IndexOf(name) > -1)
            {
                Values.RemoveAt(Keys.IndexOf(name));
                Keys.Remove(name);
            }
        }

        public bool GetField(out bool field, string name, bool fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref bool field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
                    field = Values[index].BoolValue;
                    return true;
                }
            }

            fail?.Invoke(name);
            return false;
        }

        public bool GetField(out double field, string name, double fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref double field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
#if JSONOBJECT_USE_FLOAT
					field = Values[index].FloatValue;
#else
                    field = Values[index].DoubleValue;
#endif
                    return true;
                }
            }

            if (fail != null) fail.Invoke(name);
            return false;
        }

        public bool GetField(out float field, string name, float fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref float field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
#if JSONOBJECT_USE_FLOAT
					field = Values[index].FloatValue;
#else
                    field = (float)Values[index].DoubleValue;
#endif
                    return true;
                }
            }

            fail?.Invoke(name);
            return false;
        }

        public bool GetField(out int field, string name, int fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref int field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
                    field = (int)Values[index].LongValue;
                    return true;
                }
            }

            if (fail != null) fail.Invoke(name);
            return false;
        }

        public bool GetField(out long field, string name, long fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref long field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
                    field = Values[index].LongValue;
                    return true;
                }
            }

            if (fail != null) fail.Invoke(name);
            return false;
        }

        public bool GetField(out uint field, string name, uint fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref uint field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
                    field = (uint)Values[index].LongValue;
                    return true;
                }
            }

            if (fail != null) fail.Invoke(name);
            return false;
        }

        public bool GetField(out string field, string name, string fallback)
        {
            field = fallback;
            return GetField(ref field, name);
        }

        public bool GetField(ref string field, string name, FieldNotFoundCallbackHandler fail = null)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
                    field = Values[index].StringValue;
                    return true;
                }
            }

            if (fail != null) fail.Invoke(name);
            return false;
        }

        public void GetField(string name, GetFieldResponseHandler response, FieldNotFoundCallbackHandler fail = null)
        {
            if (response != null && Type == ValueType.Object && Keys != null && Values != null)
            {
                var index = Keys.IndexOf(name);
                if (index >= 0)
                {
                    response.Invoke(Values[index]);
                    return;
                }
            }

            if (fail != null)
                fail.Invoke(name);
        }

        public MstJson GetField(string name)
        {
            if (Type == ValueType.Object && Keys != null && Values != null)
            {
                for (var index = 0; index < Keys.Count; index++)
                    if (Keys[index] == name)
                        return Values[index];
            }

            return null;
        }

        public bool HasFields(string[] names)
        {
            if (Type != ValueType.Object || Keys == null || Values == null)
                return false;

            foreach (var name in names)
                if (!Keys.Contains(name))
                    return false;

            return true;
        }

        public bool HasField(string name)
        {
            if (Type != ValueType.Object || Keys == null || Values == null)
                return false;

            if (Keys == null || Values == null)
                return false;

            foreach (var fieldName in Keys)
                if (fieldName == name)
                    return true;

            return false;
        }

        public void Clear()
        {
            Type = ValueType.Null;
            if (Values != null)
                Values.Clear();

            if (Keys != null)
                Keys.Clear();

            StringValue = null;
            LongValue = 0;
            BoolValue = false;
            IsInteger = false;
#if JSONOBJECT_USE_FLOAT
			FloatValue = 0;
#else
            DoubleValue = 0;
#endif
        }

        /// <summary>
        /// Copy a MstJson. This could be more efficient
        /// </summary>
        /// <returns></returns>
        public MstJson Copy()
        {
            return Create(Print());
        }

        /// <summary>
        /// The Merge function is experimental. Use at your own risk.
        /// </summary>
        /// <param name="jsonObject"></param>
        public void Merge(MstJson jsonObject)
        {
            MergeRecur(this, jsonObject);
        }

        public void Bake()
        {
            if (Type == ValueType.Baked)
                return;

            StringValue = Print();
            Type = ValueType.Baked;
        }

        public IEnumerable BakeAsync()
        {
            if (Type == ValueType.Baked)
                yield break;


            var builder = new StringBuilder();
            using (var enumerator = PrintAsync(builder).GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current)
                        yield return null;
                }

                StringValue = builder.ToString();
                Type = ValueType.Baked;
            }
        }

        public string Print(bool pretty = false)
        {
            var builder = new StringBuilder();
            Print(builder, pretty);
            return builder.ToString();
        }

        public void Print(StringBuilder builder, bool pretty = false)
        {
            Stringify(0, builder, pretty);
        }

        public IEnumerable<string> PrintAsync(bool pretty = false)
        {
            var builder = new StringBuilder();
            foreach (var pause in PrintAsync(builder, pretty))
            {
                if (pause)
                    yield return null;
            }

            yield return builder.ToString();
        }

        public IEnumerable<bool> PrintAsync(StringBuilder builder, bool pretty = false)
        {
            _printWatch.Reset();
            _printWatch.Start();
            using (var enumerator = StringifyAsync(0, builder, pretty).GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current)
                        yield return true;
                }
            }
        }

#if USING_UNITY
        public static implicit operator WWWForm(MstJson jsonObject)
        {
            var form = new WWWForm();
            var count = jsonObject.Count;
            var list = jsonObject.Values;
            var keys = jsonObject.Keys;
            var hasKeys = jsonObject.Type == ValueType.Object && keys != null && keys.Count >= count;

            for (var i = 0; i < count; i++)
            {
                var key = hasKeys ? keys[i] : i.ToString(CultureInfo.InvariantCulture);
                var element = list[i];
                var val = element.ToString();
                if (element.Type == ValueType.String)
                    val = val.Replace("\"", "");

                form.AddField(key, val);
            }

            return form;
        }
#endif
        public MstJson this[int index]
        {
            get
            {
                return Count > index ? Values[index] : null;
            }
            set
            {
                if (Count > index)
                    Values[index] = value;
            }
        }

        public MstJson this[string index]
        {
            get { return GetField(index); }
            set { SetField(index, value); }
        }

        public override string ToString()
        {
            return Print();
        }

        public string ToString(bool pretty)
        {
            return Print(pretty);
        }

        public string ToBase64()
        {
            return ToString().ToBase64();
        }

        public Dictionary<string, string> ToDictionary()
        {
            if (Type != ValueType.Object)
            {
#if USING_UNITY
                Debug.Log
#else
				Debug.WriteLine
#endif
                    ("Tried to turn non-Object MstJson into a dictionary");

                return null;
            }

            var result = new Dictionary<string, string>();
            var listCount = Count;
            if (Keys == null || Keys.Count != listCount)
                return result;

            for (var index = 0; index < listCount; index++)
            {
                var element = Values[index];
                switch (element.Type)
                {
                    case ValueType.String:
                        result.Add(Keys[index], element.StringValue);
                        break;
                    case ValueType.Number:
#if JSONOBJECT_USE_FLOAT
						result.Add(Keys[index], element.FloatValue.ToString(CultureInfo.InvariantCulture));
#else
                        result.Add(Keys[index], element.DoubleValue.ToString(CultureInfo.InvariantCulture));
#endif

                        break;
                    case ValueType.Bool:
                        result.Add(Keys[index], element.BoolValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    default:
#if USING_UNITY
                        Debug.LogWarning
#else
						Debug.WriteLine
#endif
                            (string.Format("Omitting object: {0} in dictionary conversion", Keys[index]));
                        break;
                }
            }

            return result;
        }

        public static implicit operator bool(MstJson jsonObject)
        {
            return jsonObject != null;
        }

#if JSONOBJECT_POOLING
		public static void ClearPool() {
			_poolingEnabled = false;
			_poolingEnabled = true;
			lock (_pool) {
				_pool.Clear();
			}
		}

		~MstJson() {
			lock (_pool) {
				if (!_poolingEnabled || _isPooled || _pool.Count >= _maxPoolSize)
					return;

				Clear();
				_isPooled = true;
				_pool.Enqueue(this);
				GC.ReRegisterForFinalize(this);
			}
		}
#endif

        public MstJsonEnumerator GetEnumerator()
        {
            return new MstJsonEnumerator(this);
        }
    }
}