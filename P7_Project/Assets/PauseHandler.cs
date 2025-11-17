using UnityEngine;

public class PauseHandler : MonoBehaviour
{
    private InputHandler input;
    private bool paused = false;

    void Start()
    {
        input = GetComponent<InputHandler>();
    }

    void Update()
    {
        if (input.aim)   
        {
            TogglePause();
        }
    }

    private void TogglePause()
    {
        paused = !paused;

        if (paused)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
