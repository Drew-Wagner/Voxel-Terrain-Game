using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a Tree on the terrain. Handles procedural generation of
/// a new tree, as well as interaction with the player.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TerrainTree : MonoBehaviour
{
    /// <summary>
    /// The transform representing the trunk of the tree.
    /// </summary>
    [SerializeField]
    private Transform trunk;
    /// <summary>
    /// The transform representing the leaves of the tree.
    /// </summary>
    [SerializeField]
    private Transform leaves;

    /// <summary>
    /// The rigidbody attached to the tree.
    /// </summary>
    Rigidbody rigidbody;

    /// <summary>
    /// Is the Shake coroutine running
    /// </summary>
    bool isShaking;

    /// <summary>
    /// How much more damage the tree can take before
    /// falling over.
    /// </summary>
    int health;

    /// <summary>
    /// Marks if the tree has been uprooted already.
    /// </summary>
    bool upRooted;

    private void Awake()
    {
        // Get the Rigidbody attached to the tree
        rigidbody = GetComponent<Rigidbody>();

        // Procedurally generate the sizes of trunk, and leaves using Perlin noise and the trees global position.
        Vector2 point = new Vector3(transform.position.x, transform.position.z) / Preferences.terrainScaler;
        float trunkHeight = 1f + Mathf.PerlinNoise(point.x, point.y) * 5.5f;
        float leavesRadius = 1f + Mathf.PerlinNoise(point.x + 1.5f, point.y) * 5f;
        float leavesHeight = 0.3f + Mathf.PerlinNoise(point.x, point.y + 1.5f) * 4.7f;
        float trunkRadius = 0.3f + Mathf.PerlinNoise(point.x + 1.5f, point.y + 1.5f) * Mathf.Max(0, 0.5f*leavesRadius-0.3f);
        trunk.localScale = new Vector3(trunkRadius, trunkHeight, trunkRadius);
        trunk.localPosition = new Vector3(0, trunkHeight / 2f, 0);
        leaves.localScale = new Vector3(leavesRadius, leavesHeight, leavesRadius);
        leaves.localPosition = new Vector3(0, trunkHeight + leavesHeight / 2f, 0);

        // Set the damage the tree can take according to its size
        health = (int)(trunkHeight * trunkRadius / 2f + 1f);
    }

    /// <summary>
    /// A coroutine to physically shake the tree when the player hits it.
    /// </summary>
    /// <returns></returns>
    IEnumerator Shake()
    {
        if (isShaking || upRooted)
        {
            yield break;
        }
        isShaking = true;
        float amplitude = 1f;
        while (amplitude >= 0.1f)
        {
            Vector3 rotationAmount = Random.insideUnitSphere * amplitude;
            transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(rotationAmount), Time.deltaTime*20f);
            amplitude -= Time.deltaTime;
            yield return null;
        }
        health--;
        transform.localRotation = Quaternion.identity;
        if (health <= 0)
        {
            UprootTree();
        }
        isShaking = false;
        yield break;
    }

    /// <summary>
    /// Causes the tree to fallover, and destroys the leaves.
    /// </summary>
    void UprootTree()
    {
        upRooted = true;
        rigidbody.isKinematic = false;
        rigidbody.AddForce(Vector3.up * 2);
        rigidbody.AddTorque(Random.onUnitSphere * 2);
        Destroy(leaves.gameObject);
    }
}
