using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class PlayWeb3Demo : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI output;

        private void Start()
        {
            output.text = $"{GameBridge.Service.Id} platform has been detected";
        }

        public void OnClickGetUserWalletByKey()
        {
            if (GameBridge.Service.Id == GameServiceId.PlayWeb3)
            {
                GameBridge.Service.Authenticate((isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                    }
                    else
                    {
                        output.text = GameBridge.Service.Player.Id;
                    }
                });
            }
            else
            {
                Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("The Play Web 3 platform has not been detected"));
            }
        }

        public void OnClickGetArtifacts()
        {
            if (GameBridge.Service.Id == GameServiceId.PlayWeb3)
            {
                GameBridge.Service.GetProducts((isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                    }
                    else
                    {
                        //output.text = GameBridge.Service.Products.Print(true);
                        Debug.Log(GameBridge.Service.Products);
                    }
                });
            }
            else
            {
                Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("The Play Web 3 platform has not been detected"));
            }
        }

        public void OnClickGetArtifactPurchases()
        {
            if (GameBridge.Service.Id == GameServiceId.PlayWeb3)
            {
                GameBridge.Service.GetProductPurchases((isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                    }
                    else
                    {
                        output.text = GameBridge.Service.Purchases.Print(true);
                        Debug.Log(GameBridge.Service.Products);
                    }
                });
            }
            else
            {
                Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("The Play Web 3 platform has not been detected"));
            }
        }
    }
}