#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MasterServerToolkit.Windows.Editor
{
    static class WelcomeScreen
    {
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            //if (!SessionState.GetBool("MIRROR_WELCOME", false))
            //{
            //    SessionState.SetBool("MIRROR_WELCOME", true);
            //    Debug.Log("Mirror | mirror-networking.com | discord.gg/N9QVxbM");
            //}
        }
    }
}
#endif