using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem; // <-- for Enter key detection

namespace Neocortex
{
    [SelectionBase]
    [AddComponentMenu("Neocortex/Text Chat Input", 0)]
    public class TextChatInput : UIBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;

        [Space]
        public UnityEvent<string> OnSendButtonClicked = new UnityEvent<string>();

        protected override void Start()
        {
            base.Start();

            // Button click listener
            sendButton.onClick.AddListener(Send);

            // TMP end edit (for mobile / UI submit buttons)
            inputField.onEndEdit.AddListener(OnEndEdit);

            // Make sure the input field is ready
            inputField.ActivateInputField();
        }

        private void Update()
        {
            // ENTER KEY detection while input is focused
            if (inputField.isFocused &&
                !string.IsNullOrEmpty(inputField.text) &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                Send();
            }
        }

        private void OnEndEdit(string _)
        {
            // Handles UI-based submit (e.g. mobile "Done" button)
            if (!string.IsNullOrEmpty(inputField.text))
            {
                Send();
            }
        }

        private void Send()
        {
            string text = inputField.text;

            if (string.IsNullOrWhiteSpace(text))
                return;

            OnSendButtonClicked.Invoke(text);

            inputField.text = "";
            inputField.ActivateInputField();  // refocus automatically
        }

        private void Send(string message)
        {
            Send();
        }
    }
}
