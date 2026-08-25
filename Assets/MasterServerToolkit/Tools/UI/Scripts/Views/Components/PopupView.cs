using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public enum DialogBoxMessageType
    {
        Info, Warning, Error
    }

    public class DialogBoxEventMessage
    {
        public string Message { get; set; } = string.Empty;
        public DialogBoxMessageType MessageType { get; set; } = DialogBoxMessageType.Info;

        public DialogBoxEventMessage() { }

        public DialogBoxEventMessage(string message) : this(message, DialogBoxMessageType.Info) { }

        public DialogBoxEventMessage(string message, DialogBoxMessageType messageType) : this()
        {
            Message = message;
            MessageType = messageType;
        }
    }

    /// <summary>
    /// Popup view that displays a set of labels and buttons.
    /// Button clicks are forwarded to user-provided callbacks by index.
    /// </summary>
    [DisallowMultipleComponent]
    public class PopupView : UIView
    {
        // -------------------- Inspector --------------------

        [Header("Labels Settings")]
        [Tooltip("UI text fields used to show popup messages. Order matters and maps to SetLabels arguments.")]
        [SerializeField]
        protected TextMeshProUGUI[] labels;

        [Header("Buttons Settings")]
        [Tooltip("Buttons that trigger callbacks by index. Order matters and maps to SetButtonsClick arguments.")]
        [SerializeField]
        protected Button[] buttons;

        [Header("Shared")]
        [Tooltip("Objects that can be used when opening a pop-up window. These can be images of notification icons or something else.")]
        [SerializeField]
        protected GameObject[] helpers;

        // -------------------- Runtime --------------------

        /// <summary>
        /// Stores user-provided callbacks mapped by button index.
        /// </summary>
        protected readonly List<UnityAction> callbacks = new();

        /// <summary>
        /// Keeps wrapped delegates we add to Button.onClick so we can remove exactly ours.
        /// </summary>
        private UnityAction[] _wrappedButtonHandlers;

        // -------------------- Unity lifecycle --------------------

        /// <summary>
        /// Initializes internal arrays. Do not bind listeners here to keep re-enable behavior clean.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _wrappedButtonHandlers = buttons != null ? new UnityAction[buttons.Length] : new UnityAction[0];
        }

        /// <summary>
        /// Binds click listeners when the view becomes active.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                int index = i;

                // Create and cache a dedicated wrapper per button to safely remove later.
                void handler()
                {
                    if (index < callbacks.Count)
                    {
                        callbacks[index]?.Invoke();
                    }
                    else
                    {
                        logger?.Warn($"No callback for button index {index} on {name}");
                    }
                }

                _wrappedButtonHandlers[index] = handler;
                btn.onClick.AddListener(handler);
            }
        }

        /// <summary>
        /// Unbinds only those listeners that were added by this component.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable(); 
            
            if (buttons == null || _wrappedButtonHandlers == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                var handler = _wrappedButtonHandlers[i];
                if (handler != null)
                {
                    btn.onClick.RemoveListener(handler);
                    _wrappedButtonHandlers[i] = null;
                }
            }
        }

        // -------------------- API --------------------

        /// <summary>
        /// Sets label texts by index. Extra values are ignored; missing labels will emit a warning.
        /// </summary>
        public virtual void SetLabels(params string[] values)
        {
            if (labels == null || values == null) return;

            for (int i = 0; i < values.Length; i++)
            {
                if (i < labels.Length && labels[i] != null)
                {
                    labels[i].text = values[i] ?? string.Empty;
                }
                else
                {
                    logger?.Warn($"No label assigned in {name} for value at index {i}");
                }
            }
        }

        /// <summary>
        /// Assigns button click callbacks by index. 
        /// Buttons without a callback will still be clickable, but do nothing (and log a warning on click).
        /// </summary>
        public virtual void SetButtonsClick(params UnityAction[] actions)
        {
            callbacks.Clear();
            if (actions != null && actions.Length > 0)
            {
                callbacks.AddRange(actions);

                if (buttons != null && actions.Length > buttons.Length)
                {
                    logger?.Warn($"More actions ({actions.Length}) than buttons ({buttons.Length}) in {name}");
                }
            }
        }

        /// <summary>
        /// Optionally toggle button interactability based on the presence of callbacks.
        /// Useful if you prefer disabling buttons that have no assigned action.
        /// </summary>
        public virtual void SyncButtonsInteractableWithCallbacks(bool disableWhenNoCallback = false)
        {
            if (!disableWhenNoCallback || buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                bool hasCallback = i < callbacks.Count && callbacks[i] != null;
                btn.interactable = hasCallback;
            }
        }
    }
}
