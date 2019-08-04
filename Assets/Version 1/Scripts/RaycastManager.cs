using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastManager : MonoBehaviour
{

    int currentMaterial = 0;

    Camera mainCamera;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            currentMaterial = 0;
        } else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentMaterial = 1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentMaterial = 2;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            currentMaterial = 3;
        }

        if (Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 8))
            {
                if (hit.transform.gameObject.layer.Equals(LayerMask.NameToLayer("Terrain")))
                {
                    // FloorToInt(x+1) == CeilToInt(x), using Floor for consitency with rest of marching cube code
                    Vector3Int[] points = new Vector3Int[] {
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z+1)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z+1)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z+1)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z+1)),
                    };

                    for (int i = 0; i < 8; i++)
                    {
                        List<Chunk> chunks = GetChunksFromPoint(points[i]);
                        foreach (Chunk chunk in chunks)
                        {
                            Vector3 localPoint = chunk.transform.InverseTransformPoint(points[i]);
                            Vector3Int chunkPoint = new Vector3Int((int)localPoint.x, (int)localPoint.y, (int)localPoint.z);

                            Vector2 v = chunk.GetValueAtPoint(chunkPoint);
                            chunk.ModifyChunkAtPoint(chunkPoint, new Vector2(Mathf.Clamp(v.x + 1f * Time.deltaTime * (1 - hit.distance / 8f), -1, 1), v.y));
                        }
                    }
                } else
                {
                    Debug.Log(hit.transform.gameObject.name);
                    TerrainTree tree = hit.transform.gameObject.GetComponentInParent<TerrainTree>();
                    tree.StartCoroutine("Shake");
                }
            }
        } else if (Input.GetMouseButton(1))
        {
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 8))
            {
                if (hit.transform.gameObject.layer.Equals(LayerMask.NameToLayer("Terrain")))
                {
                    // FloorToInt(x+1) == CeilToInt(x), using Floor for consitency with rest of marching cube code
                    Vector3Int[] points = new Vector3Int[] {
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z+1)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y), Mathf.FloorToInt(hit.point.z+1)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x+1), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z+1)),
                    new Vector3Int(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.y+1), Mathf.FloorToInt(hit.point.z+1)),
                    };

                    for (int i = 0; i < 8; i++)
                    {
                        List<Chunk> chunks = GetChunksFromPoint(points[i]);
                        foreach (Chunk chunk in chunks)
                        {
                            Vector3 localPoint = chunk.transform.InverseTransformPoint(points[i]);
                            Vector3Int chunkPoint = new Vector3Int((int)localPoint.x, (int)localPoint.y, (int)localPoint.z);

                            float v = chunk.GetValueAtPoint(chunkPoint).x;
                            chunk.ModifyChunkAtPoint(chunkPoint, new Vector2(Mathf.Clamp(v - 1f * Time.deltaTime * (1 - hit.distance / 8f), -1, 1), currentMaterial));
                        }
                    }
                }
            }
        }
    }

    public List<Chunk> GetChunksFromPoint(Vector3Int point)
    {
        List<Chunk> chunksContainingPoint = new List<Chunk>();
        foreach (Chunk chunk in ChunkManager.instance.GetVisibleChunks())
        {
            if (chunk.ContainsPoint(point))
            {
                chunksContainingPoint.Add(chunk);
            }
        }
        return chunksContainingPoint;
    }
}
