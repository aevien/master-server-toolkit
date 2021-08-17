using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstApplicationConfig : DynamicSingletonBehaviour<MstApplicationConfig>
    {
        [Header("Application Settings"), Tooltip("Unique application key. Must be the same both on client and on server."), SerializeField]
        private string applicationKey = "mst";

        [Header("Security Settings"), Tooltip("Whether or not to use secure connection"), SerializeField]
        private bool useSecure = false;

        [SerializeField]
        protected HelpBox securityInfoжно = new HelpBox()
        {
            Text = "If \"Use Secure\" is enabled you are required to setup path and password of security certificate in application.cfg file",
            Type = HelpBoxType.Warning
        };

        /// <summary>
        /// Application key
        /// </summary>
        public string ApplicationKey
        {
            get
            {
                return Mst.Args.AsString(Mst.Args.Names.ApplicationKey, applicationKey).Trim();
            }
        }

        /// <summary>
        /// Whether or not to use secure connection 
        /// </summary>
        public bool UseSecure
        {
            get
            {
                if (Mst.Args.IsProvided(Mst.Args.Names.UseSecure))
                {
                    return Mst.Args.UseSecure;
                }
                else
                {
                    return useSecure;
                }
            }
        }

        /// <summary>
        /// Path to certificate
        /// </summary>
        public string CertificatePath
        {
            get
            {
                return Mst.Args.CertificatePath?.Trim();
            }
        }

        /// <summary>
        /// Path to certificate
        /// </summary>
        public string CertificatePassword
        {
            get
            {
                return Mst.Args.CertificatePassword?.Trim();
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(applicationKey)) applicationKey = Mst.Helper.CreateRandomAlphanumericString(32);
        }
    }
}
