using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingOverlay : MonoBehaviour
{
    public Text text;
    float startTime;
    int i = 0;
    // Start is called before the first frame update
    void Awake()
    {
        startTime = Time.time;
        StartCoroutine("DoLoadingScreen");
    }

    // Update is called once per frame
    IEnumerator DoLoadingScreen()
    {
        while (Time.time - startTime < 5f)
        {
            switch (i)
            {
                case 0:
                    text.text = "LOADING.";
                    break;
                case 1:
                    text.text = "LOADING..";
                    break;
                case 2:
                    text.text = "LOADING...";
                    break;
                case 3:
                    text.text = "LOADING....";
                    break;
            }
            i = (i + 1) % 4;
            yield return new WaitForSeconds(0.5f);
        }
        gameObject.SetActive(false);
    }
}
