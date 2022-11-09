using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class PasswordInputDialoxBoxEventMessage
    {
        public PasswordInputDialoxBoxEventMessage() { }

        public PasswordInputDialoxBoxEventMessage(string message)
        {
            Message = message;
            OkCallback = null;
        }

        public PasswordInputDialoxBoxEventMessage(string message, UnityAction submitCallback)
        {
            Message = message;
            OkCallback = submitCallback;
        }

        public string Message { get; set; }
        public UnityAction OkCallback { get; set; }
    }
}