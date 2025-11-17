using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

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
            sendButton.onClick.AddListener(Send);
            inputField.onSubmit.AddListener(Send);
        }
        
        private void Send(string message)
        {
            Send();
        }
        
        private void Send()
        {
            if (inputField.text.Length > 0)
            {
                OnSendButtonClicked.Invoke(inputField.text);
                inputField.text = "";
            }
        }
    }
}
