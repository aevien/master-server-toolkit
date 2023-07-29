using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.Bridges
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
        private MstJson tasks;
        /// <summary>
        /// 
        /// </summary>
        private MstJson users;

        private IEnumerator LoadUsers()
        {
            using (UnityWebRequest www = UnityWebRequest.Get("https://jsonplaceholder.typicode.com/users"))
            {
                yield return www.SendWebRequest();

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                if (www.isHttpError || www.isNetworkError)
                {
                    Debug.LogError(www.error);
                }
                else if (www.isDone)
                {
                    var json = www.downloadHandler.text;
                    users = new MstJson(json);
                }
#endif

#if UNITY_2020_3_OR_NEWER                                                                                                                                                          
                if (www.result == UnityWebRequest.Result.ConnectionError
                    || www.result == UnityWebRequest.Result.ProtocolError
                    || www.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogError(www.error);
                }
                else if (www.result == UnityWebRequest.Result.Success)
                {
                    var json = www.downloadHandler.text;
                    users = new MstJson(json);
                }
#endif
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
                    Debug.LogError(www.error);
                }
                else if (www.isDone)
                {
                    var json = www.downloadHandler.text;
                    tasks = new MstJson(json);
                }
#endif

#if UNITY_2020_3_OR_NEWER                                                                                                                                                          
                if (www.result == UnityWebRequest.Result.ConnectionError
                    || www.result == UnityWebRequest.Result.ProtocolError
                    || www.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogError(www.error);
                }
                else if (www.result == UnityWebRequest.Result.Success)
                {
                    var json = www.downloadHandler.text;
                    tasks = new MstJson(json);
                }
#endif
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
                        var task = tasks[Random.Range(0, tasks.Values.Count)];
                        var user = users.Values.Where(i => i.GetField("id").StringValue == task.GetField("userId").StringValue).FirstOrDefault();

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
                            Logs.Error($"User {task.GetField("userId")} not found");
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