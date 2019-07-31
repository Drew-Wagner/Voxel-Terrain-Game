using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SeedSelectListener : MonoBehaviour
{
    public InputField inputField;

    public void SetSeed()
    {
        Preferences.seed = inputField.text.GetHashCode();
    }
}
