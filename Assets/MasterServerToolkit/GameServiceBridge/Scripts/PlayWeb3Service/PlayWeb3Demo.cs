using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
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
                GameBridge.Authenticate((isSuccess) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("Error"));
                    }
                    else
                    {
                        output.text = GameBridge.Service.PlayerId;
                        Debug.Log(GameBridge.Service.PlayerId);
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
                GameBridge.GetProducts((isSuccess) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("Error"));
                    }
                    else
                    {
                        output.text = GameBridge.Service.Products.Print(true);
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
                GameBridge.GetProductPurchases((isSuccess) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("Error"));
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