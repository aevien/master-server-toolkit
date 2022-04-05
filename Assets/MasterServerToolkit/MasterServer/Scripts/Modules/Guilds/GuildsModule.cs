using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class GuildsModule : BaseServerModule
    {
        #region INSPECTOR

        /// <summary>
        /// Database accessor factory that helps to create integration with guilds db
        /// </summary>
        [Header("Components"), Tooltip("Database accessor factory that helps to create integration with guilds db"), SerializeField]
        protected DatabaseAccessorFactory guildsAccessorFactory;

        #endregion

        /// <summary>
        /// Auth module for listening to auth events
        /// </summary>
        protected AuthModule authModule;

        public override void Initialize(IServer server)
        {
            guildsAccessorFactory?.CreateAccessors();


        }
    }
}
