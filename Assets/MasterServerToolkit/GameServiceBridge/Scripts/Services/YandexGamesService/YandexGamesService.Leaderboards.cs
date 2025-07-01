using MasterServerToolkit.Json;
using System.Collections;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    public partial class YandexGamesService : BaseGameService
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern void Gb_Yg_SetLeaderboardScore(string options);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetLeaderboardDescription(string name);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetLeaderboardEntries(string name, string options);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetLeaderboardPlayerEntry(string name);
#endif

        protected Coroutine leaderboardEntriesCoroutine;
        protected Coroutine leaderboardPlayerEntryCoroutine;

        public override void SetLeaderboardScore(string name, int score, MstJson extra)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            var options = MstJson.EmptyObject;
            options.AddField("leaderboardName", name);
            options.AddField("score", score);
            options.AddField("extraData", extra.ToString());
            Gb_Yg_SetLeaderboardScore(options.ToString());
#endif
        }

        public override void GetLeaderboardInfo(string name, LeaderboardInfoHandler callback)
        {
            if (LeaderboardDescription == null)
            {
                base.GetLeaderboardInfo(name, callback);
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_GetLeaderboardDescription(name);
#endif
            }
            else
            {
                callback?.Invoke(LeaderboardDescription);
            }
        }

        public override void GetLeaderboardPlayerInfo(string name, LeaderboardPlayerInfoHandler callback)
        {
            if (leaderboardPlayerEntryCoroutine == null)
            {
                leaderboardPlayerEntryCoroutine = StartCoroutine(coroutine());
            }
            else
            {
                callback?.Invoke(LeaderboardPlayerEntry);
            }

            IEnumerator coroutine()
            {
                base.GetLeaderboardPlayerInfo(name, callback);
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_GetLeaderboardPlayerEntry(name);
#endif

                yield return new WaitForSecondsRealtime(options.GetField(GameServiceOptionKeys.YG_LEADERBOARD_ENTRIES_INTERVAL).FloatValue);
                leaderboardEntriesCoroutine = null;
            }
        }

        public override void GetLeaderboardEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            if (leaderboardEntriesCoroutine == null)
            {
                leaderboardEntriesCoroutine = StartCoroutine(coroutine());
            }
            else
            {
                callback?.Invoke(LeaderboardEntries);
            }

            IEnumerator coroutine()
            {
                base.GetLeaderboardEntries(name, options, callback);
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_GetLeaderboardEntries(name, options.ToString());
#endif

                yield return new WaitForSecondsRealtime(options.GetField(GameServiceOptionKeys.YG_LEADERBOARD_ENTRIES_INTERVAL).FloatValue);
                leaderboardEntriesCoroutine = null;
            }
        }
    }
}