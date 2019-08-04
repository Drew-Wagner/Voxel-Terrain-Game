using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButtonOnClick : MonoBehaviour
{
    public void Exit() {
        Application.Quit();
    }

    public void StartButton()
    {
        SceneManager.LoadScene("Gameplay");
    }
}
