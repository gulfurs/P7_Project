using UnityEngine;
using TMPro;

public class PauseHandler : MonoBehaviour
{
    private InputHandler input;
    private bool paused = false;
    private bool prevAim = false;

    [SerializeField] private TMP_InputField defaultInputField;

    void Start()
    {
        input = GetComponent<InputHandler>();
    }

    void Update()
    {
        if (input.aim && !prevAim)
        {
            TogglePause();
        }

        if (paused && Input.GetKeyDown(KeyCode.Return))
        {
            SubmitInput();
        }

        prevAim = input.aim;
    }

    private void TogglePause()
    {
        paused = !paused;

        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (defaultInputField != null)
            {
                defaultInputField.Select();
                defaultInputField.ActivateInputField();
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SubmitInput()
    {
        if (defaultInputField == null) return;

        string text = defaultInputField.text;

        Debug.Log("Submitted: " + text);

        defaultInputField.text = "";
        defaultInputField.ActivateInputField();
    }
}
