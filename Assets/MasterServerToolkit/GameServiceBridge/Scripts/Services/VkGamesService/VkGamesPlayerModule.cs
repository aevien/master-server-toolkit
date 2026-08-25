using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class VkGamesPlayerModule : BasePlayerModule
    {
        [DllImport("__Internal")] private static extern int Gb_Vk_IsReady();
        [DllImport("__Internal")] private static extern void Gb_Vk_GetPlayer();

        private Coroutine initRoutine;

        public override void OnInit(IService service)
        {
            if (initRoutine != null)
                return;

            base.OnInit(service);
            IsSupported = true;
            IsAuthenticationSupported = false;
            initRoutine = StartCoroutine(InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            yield return null;
            float elapsed = 0f;
            while (Gb_Vk_IsReady() != 1 && elapsed < Service.WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Gb_Vk_GetPlayer();
        }

        public override void Authenticate(SuccessCallback callback)
        {
            base.Authenticate(callback);
            NotifyOnAuthenticated(false, "VK Games does not support interactive platform authentication");
        }

        protected void Vk_OnGetPlayer(string json)
        {
            var data = MstJson.IsJson(json) ? new MstJson(json) : MstJson.CreateObject();
            if (data.HasField("error"))
                Logger.Warn($"VK player info load failed. Error: {data["error"].StringValue}");

            Id = data.HasField("id") ? data["id"].StringValue : string.Empty;
            Name = data.HasField("name") ? data["name"].StringValue : string.Empty;
            Avatar = data.HasField("avatar") ? data["avatar"].StringValue : string.Empty;
            IsGuest = !data.HasField("is_guest") || data["is_guest"].BoolValue;
            Extra = data.HasField("extra") ? data["extra"] : MstJson.CreateObject();
            IsReady = true;
            initRoutine = null;
            NotifyOnInfoChanged();
        }
    }
}
