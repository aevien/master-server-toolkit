using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class OkDialogBoxEventMessage
    {
        public OkDialogBoxEventMessage() { }

        public OkDialogBoxEventMessage(string message)
        {
            Message = message;
            OkCallback = null;
        }

        public OkDialogBoxEventMessage(string message, UnityAction okCallback)
        {
            Message = message;
            OkCallback = okCallback;
        }

        public string Message { get; set; }
        public UnityAction OkCallback { get; set; }
    }
}
