using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsSliderListener : MonoBehaviour
{
    public Slider slider;
    public Text valueText;
    public bool TrueForViewDistFalseForTerrainHeight;

    private void Awake()
    {
        if (TrueForViewDistFalseForTerrainHeight) {
            slider.value = Preferences.viewDist;
            valueText.text = ((int)slider.value).ToString();
        } else
        {
            slider.value = Preferences.maxTerrainHeight;
            valueText.text = ((int)slider.value).ToString();
        }
    }

    public void SetViewDist()
    {
        Preferences.viewDist = (int)slider.value;
        valueText.text = ((int)slider.value).ToString();
    }

    public void SetMaxTerrainHeight()
    {
        Preferences.maxTerrainHeight = (int)slider.value;
        valueText.text = ((int)slider.value).ToString();
    }
}
