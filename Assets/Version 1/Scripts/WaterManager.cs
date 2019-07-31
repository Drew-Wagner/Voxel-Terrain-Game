using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterManager : MonoBehaviour
{
    public Color shallowColor;
    public Color deepColor;

    bool normal_fog;
    Color normal_fogColor;
    float normal_fogDensity;
    float normal_fogStartDistance;
    float normal_fogEndDistance;
    FogMode normal_fogMode;

    Color normal_ambientColor;

    Material normal_sky;

    float normal_sun_intensity;
    Color normal_sun_color;

    Color underwater_color;
    Material underwater_sky;

    // Start is called before the first frame update
    void Start()
    {
        normal_fog = RenderSettings.fog;
        normal_fogColor = RenderSettings.fogColor;
        normal_fogDensity = RenderSettings.fogDensity;
        normal_fogStartDistance = RenderSettings.fogStartDistance;
        normal_fogEndDistance = RenderSettings.fogEndDistance;
        normal_fogMode = RenderSettings.fogMode;

        normal_sky = RenderSettings.skybox;
        normal_ambientColor = RenderSettings.ambientLight;

        normal_sun_intensity = GameManager.instance.sun.intensity;
        normal_sun_color = GameManager.instance.sun.color;

        underwater_sky = new Material(Shader.Find("Unlit/Color"));
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(Camera.main.transform.position.x, GameManager.instance.waterLevel, Camera.main.transform.position.z);

        if (Camera.main.transform.position.y < GameManager.instance.waterLevel)
        {
            float depth = Mathf.Abs(Camera.main.transform.position.y - GameManager.instance.waterLevel);

            underwater_color = Color.Lerp(shallowColor, deepColor, depth / 100f);

            RenderSettings.fog = true;
            RenderSettings.fogColor = underwater_color;
            RenderSettings.fogDensity = Mathf.Clamp(depth / 500f, 0.02f, 0.1f);

            underwater_sky.color = underwater_color;
            RenderSettings.skybox = underwater_sky;
            RenderSettings.ambientLight = underwater_color;

            GameManager.instance.sun.intensity = normal_sun_intensity * Mathf.Min(1, 1 / depth);
            GameManager.instance.sun.color = underwater_color;
        } else
        {
            RenderSettings.fog = normal_fog;
            RenderSettings.fogColor = normal_fogColor;
            RenderSettings.fogDensity = normal_fogDensity;
            RenderSettings.fogStartDistance = normal_fogStartDistance;
            RenderSettings.fogEndDistance = normal_fogEndDistance;
            RenderSettings.fogMode = normal_fogMode;
            RenderSettings.skybox = normal_sky;
            RenderSettings.ambientLight = normal_ambientColor;
            GameManager.instance.sun.intensity = normal_sun_intensity;
            GameManager.instance.sun.color = normal_sun_color;
        }
    }
}
