using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
        #region ADVERTISEMENT

        public bool IsAdVisible { get; protected set; }
        public bool IsAdSupported { get; protected set; }
        public virtual bool IsAdFullScreenVideoReady { get; protected set; }

        public virtual void ShowFullScreenVideo(FullScreenVideoHandler callback)
        {
            if (IsAdSupported)
            {
                IsAdVisible = true;
                fullScreenVideoCallback = callback;
            }
            else
            {
                IsAdVisible = false;
                callback?.Invoke(FullScreenVideoStatus.Error);
            }
        }

        public virtual void ShowRewardedVideo(RewardedVideoHandler callback)
        {
            if (IsAdSupported)
            {
                IsAdVisible = true;
                rewardedVideoCallback = callback;
            }
            else
            {
                IsAdVisible = false;
                callback?.Invoke(RewardedVideoStatus.Error);
            }
        }

        #endregion
    }
}
