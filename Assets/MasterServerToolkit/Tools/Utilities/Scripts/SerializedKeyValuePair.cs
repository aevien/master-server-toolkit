using System;

namespace MasterServerToolkit.Utils
{
    [Serializable]
    public struct SerializedKeyValuePair
    {
        public string key;
        public string value;

        public SerializedKeyValuePair(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }
}
