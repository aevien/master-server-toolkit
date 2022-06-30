using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstApplicationConfig : MonoBehaviour
    {
        [Header("Application Settings"), Tooltip("Unique application key. Must be the same both on client and on server."), SerializeField]
        private string applicationKey = "mst";

        [Header("Security Settings"), Tooltip("Whether or not to use secure connection"), SerializeField]
        private bool useSecure = false;

        [SerializeField]
        protected HelpBox securityInfo = new HelpBox()
        {
            Text = "If \"Use Secure\" is enabled you are required to setup path and password of security certificate in application.cfg file",
            Type = HelpBoxType.Warning
        };

        private void Awake()
        {
            Mst.Settings.UseSecure = useSecure;
            Mst.Settings.ApplicationKey = applicationKey;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(applicationKey)) applicationKey = Mst.Helper.CreateGuidString();
        }
    }
}
