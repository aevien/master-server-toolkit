using MasterServerToolkit.Json;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnersApiController : WebController
    {
        private SpawnersModule spawnersModule;

        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            spawnersModule = Server.GetModule<SpawnersModule>();

            // Info API
            webServer.RegisterGetHandler("api/v1/spawners/info", GetSpawnersInfoHandler, UseCredentials);
        }

        #region HANDLERS

        private Task<IHttpResult> GetSpawnersInfoHandler(HttpListenerRequest request)
        {
            MstJson json = MstJson.CreateObject();

            if (spawnersModule != null)
                json = spawnersModule.Details();

            return Task.FromResult<IHttpResult>(new JsonResult(json));
        }

        #endregion
    }
}