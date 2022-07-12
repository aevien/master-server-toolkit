#if FISHNET
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public class FakeNoticeManager : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private RoomServerManager roomServerManager;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        private JArray tasks;

        public void Initialize()
        {
            // Start loading fake data for notifications
            StartCoroutine(LoadFakeData());
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
                    ParseData(json);
                }

            }
        }

        private void ParseData(string json)
        {
            tasks = JArray.Parse(json);
            StartTaskNotices();
        }

        private void StartTaskNotices()
        {
            float timeToWait = Random.Range(3f, 10f);

            MstTimer.Instance.WaitForRealtimeSeconds(timeToWait, () =>
            {
                var task = tasks[Random.Range(0, tasks.Count)];
                string message = $"<color=#B7E117FF>{task["title"]}</color>\n".ToUpper() +
                    $"Status: {(task.Value<bool>("completed") ? "Completed" : "In progress")}\nPlayer: {task["userId"]}";

                if (roomServerManager.HasPlayers)
                {
                    Mst.Server.Notifications.NotifyRoom(roomServerManager.RoomController.RoomId, message, null);
                    StartTaskNotices();
                }
            });
        }
    }
}
#endif