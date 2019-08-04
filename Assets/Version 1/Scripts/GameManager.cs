using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages global game mechanics: UI, cursor locking, and eventually other things.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Text debugInfo;

    Camera mainCamera;

    private void Awake()
    {
        instance = this;
        mainCamera = Camera.main;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Returns to Main menu when Escape key is pressed.
        if (!Application.isEditor && Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }

        // Toggles display of debug info
        if (Input.GetKeyDown(KeyCode.F1))
        {
            debugInfo.gameObject.SetActive(!debugInfo.gameObject.activeSelf);
        }

        // Updates debug info information every 15 frames
        if (debugInfo.gameObject.activeSelf && Time.frameCount%15 == 0)
        {
            int fps = Mathf.RoundToInt(1f / Time.deltaTime);
            debugInfo.text = $"FPS: {fps}\nPOS: {mainCamera.transform.position.ToString()}\nFOR: {mainCamera.transform.forward.ToString()}";
        }
        
    }
}
