using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern void MstAnalyticsEvent(string eventData);
#endif

        private HashSet<string> analyticsSingltons = new HashSet<string>();

        #region ANALYTICS

        public virtual void AnalyticsEvent(MstJson eventData, bool singleton = false)
        {
            AnalyticsEvent(eventData.ToString(), singleton);
        }

        public virtual void AnalyticsEvent(string eventData, bool singleton = false)
        {
            if (string.IsNullOrEmpty(eventData))
                return;

            if (singleton)
            {
                if (analyticsSingltons.Contains(eventData))
                {
                    return;
                }
                else
                {
                    analyticsSingltons.Add(eventData);
                }
            }

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            MstAnalyticsEvent(eventData);
#else
            Logs.Info(eventData);
#endif
        }

        #endregion
    }
}
