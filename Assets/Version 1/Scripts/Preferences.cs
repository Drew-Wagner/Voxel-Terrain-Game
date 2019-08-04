using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static class <c>Preferences</c> stores configuration values to be passed between scenes.
/// </summary>
public static class Preferences
{
    /// <summary>
    /// Maximum view distance
    /// </summary>
    public static int viewDist = 48;

    /// <summary>
    /// Maximum terrain height
    /// </summary>
    public static float maxTerrainHeight = 256;

    /// <summary>
    /// The seed used for randomly generating the terrain.
    /// </summary>
    public static float seed = "123".GetHashCode();

    /// <summary>
    /// Determines how much the noise used to generate the terrain is scaled horizontally.
    /// </summary>
    public static float terrainScaler = 4;
}
