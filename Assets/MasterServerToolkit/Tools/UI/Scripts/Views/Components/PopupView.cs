using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class PopupView : UIView
    {
        [Header("Lables Settings"), SerializeField]
        protected TMP_Text[] lables;

        [Header("Buttons Settings"), SerializeField]
        protected Button[] buttons;

        public virtual void SetLables(params string[] values)
        {
            if (values.Length == 0)
            {
                logger.Warn($"There is no need to use SetLables method of {name} because of you do not pass any values as its parameters");
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                try
                {
                    lables[i].text = values[i];
                }
                catch
                {
                    logger.Warn($"There is no lable assigned to {name} for value at index {i}");
                }
            }
        }

        public virtual void SetButtonsClick(params UnityAction[] actions)
        {
            if (actions.Length == 0)
            {
                logger.Warn($"There is no need to use SetButtonsClick method of {name} because of you do not pass any action as its parameters");
                return;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                try
                {
                    buttons[i].onClick.RemoveAllListeners();
                    buttons[i].onClick.AddListener(actions[i]);
                }
                catch
                {
                    logger.Warn($"There is no button assigned to {name} for action at index {i}");
                }
            }
        }
    }
}
