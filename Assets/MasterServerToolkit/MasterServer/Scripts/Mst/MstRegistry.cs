using MasterServerToolkit.Logging;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace MasterServerToolkit.MasterServer
{
    public class MstRegistry
    {
        private readonly ConcurrentDictionary<ushort, string> messageOpCodes = new ConcurrentDictionary<ushort, string>();
        private readonly ConcurrentDictionary<ushort, string> profileOpCodes = new ConcurrentDictionary<ushort, string>();

        public MstRegistry()
        {
            RegisterMessageOpCodes();
            RegisterProfilePropertyOpCodes();
        }

        private void RegisterMessageOpCodes()
        {
            Type messageOpCodesStruct = typeof(MstOpCodes);

            foreach (var field in messageOpCodesStruct.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                object value = field.GetValue(null);

                if (value.GetType() == typeof(ushort))
                {
                    AddMessageOpCode((ushort)value, field.Name);
                }
            }
        }

        private void RegisterProfilePropertyOpCodes()
        {
            Type profilePropertyOpCodesStruct = typeof(ProfilePropertyOpCodes);

            foreach (var field in profilePropertyOpCodesStruct.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                object value = field.GetValue(null);

                if (value.GetType() == typeof(ushort))
                {
                    AddProfilePropertyOpCode((ushort)value, field.Name);
                }
            }
        }

        /// <summary>
        /// Adds opcode and its name to registry
        /// </summary>
        /// <param name="code"></param>
        /// <param name="name"></param>
        public void AddMessageOpCode(ushort code, string name)
        {
            if (messageOpCodes.ContainsKey(code))
            {
                Logs.Error($"Code {code} with name {name} is already registered");
                return;
            }

            messageOpCodes[code] = name;
        }

        /// <summary>
        /// Adds opcode and its name to registry
        /// </summary>
        /// <param name="code"></param>
        /// <param name="name"></param>
        public void AddProfilePropertyOpCode(ushort code, string name)
        {
            if (profileOpCodes.ContainsKey(code))
            {
                Logs.Error($"Code {code} with name {name} is already registered");
                return;
            }

            profileOpCodes[code] = name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public string GetMessageOpCodeName(ushort code)
        {
            messageOpCodes.TryGetValue(code, out string name);
            return !string.IsNullOrEmpty(name) ? name : code.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public string GetProfilePropertyOpCodeName(ushort code)
        {
            profileOpCodes.TryGetValue(code, out string name);
            return !string.IsNullOrEmpty(name) ? name : code.ToString();
        }
    }
}