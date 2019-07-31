using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Text debugInfo;
    public Light sun;
    public float waterLevel = 0;

    Camera mainCamera;

    private void Awake()
    {
        instance = this;
        mainCamera = Camera.main;
    }
    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        //if (Time.frameCount % 30 == 0) System.GC.Collect();
        if (!Application.isEditor && Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            debugInfo.gameObject.SetActive(!debugInfo.gameObject.activeSelf);
        }
        if (debugInfo.gameObject.activeSelf)
        {
            int fps = Mathf.RoundToInt(1f / Time.deltaTime);
            debugInfo.text = $"FPS: {fps}\nPOS: {mainCamera.transform.position.ToString()}\nFOR: {mainCamera.transform.forward.ToString()}";
        }
        
    }
}
