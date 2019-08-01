using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TreeGenerator : MonoBehaviour
{
    public Transform trunk;
    public Transform leaves;
    Rigidbody rigidbody;

    bool isShaking;

    int health;

    bool upRooted;

    // Start is called before the first frame update
    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        Vector2 point = new Vector3(transform.position.x, transform.position.z) / ChunkManager.instance.scaler;
        float trunkHeight = 1f + Mathf.PerlinNoise(point.x, point.y) * 5.5f;
        float leavesRadius = 1f + Mathf.PerlinNoise(point.x + 1.5f, point.y) * 5f;
        float leavesHeight = 0.3f + Mathf.PerlinNoise(point.x, point.y + 1.5f) * 4.7f;
        float trunkRadius = 0.3f + Mathf.PerlinNoise(point.x + 1.5f, point.y + 1.5f) * Mathf.Max(0, 0.5f*leavesRadius-0.3f);
        trunk.localScale = new Vector3(trunkRadius, trunkHeight, trunkRadius);
        trunk.localPosition = new Vector3(0, trunkHeight / 2f, 0);
        leaves.localScale = new Vector3(leavesRadius, leavesHeight, leavesRadius);
        leaves.localPosition = new Vector3(0, trunkHeight + leavesHeight / 2f, 0);

        health = (int)(trunkHeight * trunkRadius / 2f + 1f);
    }

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
            upRooted = true;
            rigidbody.isKinematic = false;
            rigidbody.AddForce(Vector3.up*2);
            rigidbody.AddTorque(Random.onUnitSphere*2);
            Destroy(leaves.gameObject);
        }
        isShaking = false;
        yield break;
    }
}
