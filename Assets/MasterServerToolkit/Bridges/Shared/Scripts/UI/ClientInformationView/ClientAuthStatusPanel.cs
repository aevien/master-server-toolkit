using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class ClientAuthStatusPanel : UIViewPanel
    {
        [Header("Components"), SerializeField]
        [Tooltip("Required label populated with the signed-in account summary whenever this panel becomes visible.")]
        private TextMeshProUGUI accountInfo;

        protected override void OnSetVisible(bool visible)
        {
            base.OnSetVisible(visible);

            if (visible == true && Mst.Client.Auth.IsSignedIn)
            {
                accountInfo.text = Mst.Client.Auth.Account.ToString();
            }
        }
    }
}
