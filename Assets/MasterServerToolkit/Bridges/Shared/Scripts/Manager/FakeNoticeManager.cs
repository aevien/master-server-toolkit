using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.Games
{
    public class FakeNoticeManager : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private RoomServerManager roomServerManager;

        [Header("Settings"), SerializeField]
        private bool active = true;

        #endregion

        private void Awake()
        {
            if (!active) return;

            roomServerManager.OnBeforeRoomRegisterEvent.AddListener((roomOptions) =>
            {
                // Start loading fake data for notifications
                StartCoroutine(LoadUsers());
                StartCoroutine(LoadFakeData());
                StartTaskNotices();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private JArray tasks;
        /// <summary>
        /// 
        /// </summary>
        private JArray users;

        private IEnumerator LoadUsers()
        {
            using (UnityWebRequest www = UnityWebRequest.Get("https://jsonplaceholder.typicode.com/users"))
            {
                yield return www.SendWebRequest();

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                if (www.isHttpError || www.isNetworkError)
                {
#elif UNITY_2020_3_OR_NEWER
                if (www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.DataProcessingError)
                {
#endif
                    Debug.LogError(www.error);
                }
                else if (www.result == UnityWebRequest.Result.Success)
                {
                    var json = www.downloadHandler.text;
                    users = JArray.Parse(json);
                }
            }
        }

        private IEnumerator LoadFakeData()
        {
            using (UnityWebRequest www = UnityWebRequest.Get("https://jsonplaceholder.typicode.com/todos"))
            {
                yield return www.SendWebRequest();

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                if (www.isHttpError || www.isNetworkError)
                {
#elif UNITY_2020_3_OR_NEWER
                if (www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.DataProcessingError)
                {
#endif
                    Debug.LogError(www.error);
                }
                else if (www.result == UnityWebRequest.Result.Success)
                {
                    var json = www.downloadHandler.text;
                    tasks = JArray.Parse(json);
                }
            }
        }

        private void StartTaskNotices()
        {
            float timeToWait = Random.Range(3f, 10f);

            MstTimer.WaitWhile(() => tasks == null || users == null, (isSuccess) =>
            {
                if (isSuccess)
                {
                    MstTimer.WaitForRealtimeSeconds(timeToWait, () =>
                    {
                        var task = tasks[Random.Range(0, tasks.Count)];
                        var user = users.Where(i => i.Value<string>("id") == task.Value<string>("userId")).FirstOrDefault();

                        if (user != null)
                        {
                            string message = $"<b>{user["username"]}</b>\n{task["title"]}";

                            if (roomServerManager.HasPlayers)
                            {
                                Mst.Server.Notifications.NotifyRoom(roomServerManager.RoomController.RoomId, message, null, roomServerManager.Connection);
                                StartTaskNotices();
                            }
                        }
                        else
                        {
                            Logs.Error($"User {task.Value<string>("userId")} not found");
                            StartTaskNotices();
                        }
                    });
                }
                else
                {
                    Logs.Error("Unable to load fake data");
                }
            }, 10f);
        }
    }
}