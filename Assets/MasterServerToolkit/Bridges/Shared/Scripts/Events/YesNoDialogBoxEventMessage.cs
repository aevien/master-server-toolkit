using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class YesNoDialogBoxEventMessage
    {
        public YesNoDialogBoxEventMessage() { }

        public YesNoDialogBoxEventMessage(string message)
        {
            Message = message;
            NoCallback = null;
        }

        public YesNoDialogBoxEventMessage(string message, UnityAction yesCallback, UnityAction noCallback)
        {
            Message = message;
            YesCallback = yesCallback;
            NoCallback = noCallback;
        }

        public string Message { get; set; }
        public UnityAction YesCallback { get; set; }
        public UnityAction NoCallback { get; set; }
    }
}