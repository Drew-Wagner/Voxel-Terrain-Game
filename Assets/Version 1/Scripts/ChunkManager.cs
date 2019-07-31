using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public static ChunkManager instance;

    public Transform viewer;
    public GameObject chunkPrefab;
    public Material mat;
    public int chunkSize = 8;
    public int maxMeshBuildsPerFrame = 1;
    [HideInInspector]
    public int viewDist = 48;
    public float scaling = 64;
    [HideInInspector]
    public float scaler;

    bool firstLoad = true;

    private int chunksVisibleInViewDist;
    private int sqrViewDist;
    private int minSqrViewDist;

    List<Chunk> chunks;
    Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> recyclableChunks;
    Stack<Chunk> chunkMeshesToBuild;

    Camera viewCamera;

    float chunkSizeMinusOne;

    private void Start()
    {
    }
    void Awake()
    {
        viewCamera = Camera.main;

        chunkSizeMinusOne = chunkSize - 1;
        viewDist = Preferences.viewDist;
        instance = this;
        existingChunks = new Dictionary<Vector3Int, Chunk>();
        recyclableChunks = new Queue<Chunk>();
        chunks = new List<Chunk>();
        chunkMeshesToBuild = new Stack<Chunk>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        chunksVisibleInViewDist = Mathf.RoundToInt(viewDist / (float)chunkSize);
        sqrViewDist = viewDist * viewDist;
        minSqrViewDist = (chunkSize+1)*(chunkSize+1);
        scaler = scaling / (chunkSize / 16f);
        RenderSettings.fogEndDistance = viewDist * 0.6f;
        RenderSettings.fogStartDistance = chunkSize;
        firstLoad = true;
    }

    private void Update()
    {
        UpdateVisibleChunks();
    }

    public void EnqueueChunk(Chunk chunk)
    {
        chunkMeshesToBuild.Push(chunk);
    }

    void UpdateMeshOfChunk(Chunk chunk)
    {
        chunk.mesh.Clear();
        chunk.mesh.vertices = chunk.meshData.vertices;
        chunk.mesh.triangles = chunk.meshData.triangles;
        chunk.mesh.colors = chunk.meshData.colors;

        chunk.mesh.Optimize();
        chunk.mesh.RecalculateNormals();

        chunk.meshFilter.mesh = chunk.mesh;
        chunk.meshCollider.sharedMesh = chunk.mesh;
        chunk.hasRequestMeshBuild = false;
    }

    void DequeueChunksAndHandleMeshData()
    {
        Vector3 viewerPosition = viewer.transform.position;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(viewerPosition.x / chunkSizeMinusOne), Mathf.RoundToInt(viewerPosition.y / chunkSizeMinusOne), Mathf.RoundToInt(viewerPosition.z / chunkSizeMinusOne));
        int i = 0;
        if (existingChunks[viewerCoord].hasRequestMeshBuild)
        {
            UpdateMeshOfChunk(existingChunks[viewerCoord]);
            i++;
        }
        for (; chunkMeshesToBuild.Count > 0 && i < maxMeshBuildsPerFrame; i++) {
            Chunk chunk = chunkMeshesToBuild.Pop();
            if (chunk.hasRequestMeshBuild)
                UpdateMeshOfChunk(chunk);
            else
                i--;
        }
        System.GC.Collect();
    }

    void RecycleChunk(int i, Chunk chunk)
    {
        existingChunks.Remove(chunk.coord);
        recyclableChunks.Enqueue(chunk);
        chunks.RemoveAt(i);

        chunk.DestroyOrDisable();
    }

    void UpdateVisibleChunks()
    {
        Vector3 viewerPosition = viewer.transform.position;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(viewerPosition.x / chunkSizeMinusOne), Mathf.RoundToInt(viewerPosition.y / chunkSizeMinusOne), Mathf.RoundToInt(viewerPosition.z / chunkSizeMinusOne));


        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            Chunk chunk = chunks[i];
            Vector3 centre = chunk.chunkPosition;
            Vector3 offsetFromViewer = centre-viewerPosition;
            Vector3 o = new Vector3(Mathf.Abs(offsetFromViewer.x), Mathf.Abs(offsetFromViewer.y), Mathf.Abs(offsetFromViewer.z)) - Vector3.one * chunkSizeMinusOne / 2;
            float sqrDist = new Vector3(Mathf.Max(0, o.x), Mathf.Max(0, o.y), Mathf.Max(0, o.z)).sqrMagnitude;

            if (sqrDist > sqrViewDist)
            {
                RecycleChunk(i, chunk);
            }
        }

        int chunksVisibleInViewDistDoubled = chunksVisibleInViewDist * 2;
        for (int i = chunksVisibleInViewDistDoubled; i >= 0; i--)
        {
            int y = i / 2;
            if (i % 2 == 1) { if (y == 0) continue; y *= -1; }

            for (int j = chunksVisibleInViewDistDoubled; j >= 0; j--)
            {
                int x = j / 2;
                if (j % 2 == 1) { if (x == 0) continue; x *= -1; }

                for (int k = chunksVisibleInViewDistDoubled; k >= 0; k--)
                {
                    int z = k / 2;
                    if (k % 2 == 1) { if (z == 0) continue; z *= -1; }

                    Vector3Int coord = new Vector3Int(x, y, z) + viewerCoord;
                    
                    if (existingChunks.ContainsKey(coord))
                    {
                        continue;
                    }

                    Vector3 centre = new Vector3(coord.x, coord.y, coord.z) * chunkSizeMinusOne;
                    Vector3 offsetFromViewer = centre-viewerPosition;
                    Vector3 o = new Vector3(Mathf.Abs(offsetFromViewer.x), Mathf.Abs(offsetFromViewer.y), Mathf.Abs(offsetFromViewer.z)) - Vector3.one * chunkSizeMinusOne / 2;
                    float sqrDist = new Vector3(Mathf.Max(0, o.x), Mathf.Max(0, o.y), Mathf.Max(0, o.z)).sqrMagnitude;

                    if (sqrDist < sqrViewDist)
                    {
                        Bounds bounds = new Bounds(centre, Vector3.one * chunkSizeMinusOne);
                        if (firstLoad || IsVisibleFrom(bounds, viewCamera) || sqrDist < minSqrViewDist)
                        {
                            if (recyclableChunks.Count > 0)
                            {
                                Chunk chunk = recyclableChunks.Dequeue();
                                chunk.Setup(coord, mat);
                                existingChunks.Add(coord, chunk);
                                chunks.Add(chunk);
                            }
                            else
                            {
                                Chunk chunk = CreateChunk();
                                chunk.Setup(coord, mat);
                                existingChunks.Add(coord, chunk);
                                chunks.Add(chunk);
                            }
                        }
                    }
                }
            }
        }
        DequeueChunksAndHandleMeshData();
        firstLoad = false;
    }

    bool IsVisibleFrom(Bounds bounds, Camera camera)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    Chunk CreateChunk()
    {
        GameObject chunk = Instantiate(chunkPrefab);
        chunk.transform.parent = transform;
        Chunk newChunk = chunk.GetComponent<Chunk>();
        return newChunk;
    }

    public List<Chunk> GetVisibleChunks()
    {
        return chunks;
    }

    public struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }
}
