using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
        protected SuccessCallback authenticateCallback;

        protected PlayerDataHandler playerDataCallback;
        protected SuccessCallback setPlayerDataCallback;

        protected FullScreenVideoHandler fullScreenVideoCallback;
        protected RewardedVideoHandler rewardedVideoCallback;

        protected ProductsHandler getProductsHandler;
        protected PurchaseHandler purchaseHandler;
        protected PurchaseHandler getPurchasesHandler;

        private event Action readyEvent;
        private event PlayerInfoHandler playerInfoEvent;
        private event PlayerDataHandler playerDataEvent;

        public event Action OnReadyEvent
        {
            add
            {
                if (isReady)
                {
                    value?.Invoke();
                }
                else
                {
                    readyEvent += value;
                }
            }
            remove
            {
                readyEvent -= value;
            }
        }

        public event PlayerInfoHandler OnPlayerInfoEvent
        {
            add
            {
                if (!string.IsNullOrEmpty(Player.Id))
                {
                    value?.Invoke(Player);
                }

                playerInfoEvent += value;
            }
            remove
            {
                playerInfoEvent -= value;
            }
        }

        public event PlayerDataHandler OnPlayerDataEvent
        {
            add
            {
                if (!Data.IsNull)
                {
                    value?.Invoke(Data);
                }

                playerDataEvent += value;
            }
            remove
            {
                playerDataEvent -= value;
            }
        }

        public event PauseHandler OnPauseEvent;

        protected void NotifyOnReady()
        {
            isReady = true;
            readyEvent?.Invoke();
        }

        protected void NotifyOnPlayerInfo()
        {
            playerInfoEvent?.Invoke(Player);
        }

        protected void NotifyOnPause(bool state)
        {
            OnPauseEvent?.Invoke(state);
        }

        protected void NotifyOnAuthenticated(bool isSuccess, string error)
        {
            authenticateCallback?.Invoke(isSuccess, error);
            authenticateCallback = null;
        }

        protected void NotifyOnPlayerData()
        {
            playerDataCallback?.Invoke(Data);
            playerDataCallback = null;
            playerDataEvent?.Invoke(Data);
        }

        protected void NotifyOnSetPlayerData(bool isSuccess, string error)
        {
            setPlayerDataCallback?.Invoke(isSuccess, error);
            playerDataCallback = null;
        }

        protected void NotifyOnFullScreenVideoStatus(FullScreenVideoStatus status)
        {
            IsAdVisible = false;
            fullScreenVideoCallback?.Invoke(status);
        }

        protected void NotifyOnRewardedVideoStatus(RewardedVideoStatus status)
        {
            switch (status)
            {
                case RewardedVideoStatus.Rewarded:
                case RewardedVideoStatus.Closed:
                case RewardedVideoStatus.Error:
                    IsAdVisible = false;
                    break;
            }

            rewardedVideoCallback?.Invoke(status);
        }

        protected void NotifyOnGetProducts(IEnumerable<ProductInfo> products)
        {
            getProductsHandler?.Invoke(products);
            getProductsHandler = null;
        }

        protected void NotifyOnPurchase(PurchasesInfo purchase)
        {
            purchaseHandler?.Invoke(purchase);
            purchaseHandler = null;
        }

        protected void NotifyOnGetPurchases(PurchasesInfo purchases)
        {
            getPurchasesHandler?.Invoke(purchases);
            getPurchasesHandler = null;
        }
    }
}