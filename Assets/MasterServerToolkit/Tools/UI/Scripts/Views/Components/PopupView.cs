using System.Collections.Generic;
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

        protected readonly List<UnityAction> callbacks = new List<UnityAction>();

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;

                if (buttons[index] != null)
                {
                    buttons[index].onClick.AddListener(() =>
                    {
                        if (index < callbacks.Count)
                            callbacks[index]?.Invoke();
                    });
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].onClick.RemoveAllListeners();
                }
            }
        }

        public virtual void SetLables(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i < lables.Length && lables[i] != null)
                {
                    lables[i].text = values[i];
                }
                else
                {
                    logger.Warn($"No label assigned to {name} for value at index {i}");
                }
            }
        }

        public virtual void SetButtonsClick(params UnityAction[] actions)
        {
            if (actions.Length > buttons.Length)
            {
                logger.Warn($"More actions than buttons in {name}");
            }

            callbacks.Clear();
            callbacks.AddRange(actions);
        }
    }
}
