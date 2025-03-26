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
        private static extern void Gb_Yg_ShowFullScreenAdv();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ShowRewardedVideo();
#endif

        private Coroutine interstitialAdCoroutine;

        #region ADVERTISEMENT

        public override void ShowFullScreenVideo(FullScreenVideoHandler callback)
        {
            if (interstitialAdCoroutine == null)
            {
                interstitialAdCoroutine = StartCoroutine(coroutine());
            }
            else
            {
                callback?.Invoke(FullScreenVideoStatus.Error);
            }

            IEnumerator coroutine()
            {
                base.ShowFullScreenVideo(callback);
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_ShowFullScreenAdv();
#endif

                yield return new WaitForSecondsRealtime(options.GetField(GameServiceOptionKeys.YG_INTERSTITIAL_AD_INTERVAL).FloatValue);

                interstitialAdCoroutine = null;
            }
        }

        public override void ShowRewardedVideo(RewardedVideoHandler callback)
        {
            base.ShowRewardedVideo(callback);
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_ShowRewardedVideo();
#endif
        }

        #endregion
    }
}