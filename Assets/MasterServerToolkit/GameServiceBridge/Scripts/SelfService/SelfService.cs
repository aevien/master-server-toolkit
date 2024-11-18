using MasterServerToolkit.MasterServer;
using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class SelfService : BaseGameService
    {
        protected override void Awake()
        {
            base.Awake();

            Id = GameServiceId.Self;
            IsInAppPurchaseSupported = false;
        }

        public override void Authenticate(SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
            {
                yield return new WaitForEndOfFrame();
                callback?.Invoke(true, string.Empty);
            };

            StartCoroutine(Coroutine(callback));
        }

        public override void GetProductPurchases(SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
            {
                yield return new WaitForEndOfFrame();
                callback?.Invoke(true, string.Empty);
            }

            StartCoroutine(Coroutine(callback));
        }

        public override void GetProducts(SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
            {
                yield return new WaitForEndOfFrame();
                callback?.Invoke(true, string.Empty);
            }

            StartCoroutine(Coroutine(callback));
        }

        public override void Init()
        {
            IEnumerator Coroutine()
            {
                yield return new WaitForEndOfFrame();
                NotifyOnReady();
            }

            StartCoroutine(Coroutine());
        }

        public override void Purchase(string productId, SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
            {
                yield return new WaitForEndOfFrame();
                callback?.Invoke(true, string.Empty);
            }

            StartCoroutine(Coroutine(callback));
        }
    }
}
