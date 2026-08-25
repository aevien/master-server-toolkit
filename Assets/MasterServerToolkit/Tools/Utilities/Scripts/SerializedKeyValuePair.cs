using System;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    [Serializable]
    public struct SerializedKeyValuePair
    {
        [Tooltip("Key used to identify this custom option. Use the exact key expected by the receiving module.")]
        public string key;

        [Tooltip("String value associated with the key. Its expected format is defined by the receiving module.")]
        public string value;

        public SerializedKeyValuePair(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }
}
