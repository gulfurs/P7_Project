using UnityEngine;

public class PauseHandler : MonoBehaviour
{
    private InputHandler input;
    private bool paused = false;
    private bool prevAim = false;

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

        prevAim = input.aim;
    }

    private void TogglePause()
    {
        paused = !paused;

        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
