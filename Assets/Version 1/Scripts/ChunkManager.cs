using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public static ChunkManager instance;

    public Transform viewer;
    public GameObject chunkPrefab;
    public Material mat;
    public bool generateColliders;
    public int chunkSize = 8;
    public int maxMeshBuildsPerFrame = 1;
    [HideInInspector]
    public int viewDist = 48;
    public float scaling = 64;
    [HideInInspector]
    public float scaler;

    private int chunksVisibleInViewDist;
    private int sqrViewDist;
    private int minSqrViewDist;

    List<Chunk> chunks;
    Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> recyclableChunks;
    Queue<Chunk> chunkMeshesToBuild;

    Camera viewCamera;

    private void Start()
    {
        //Texture2D[] textures = Resources.LoadAll<Texture2D>("Textures/Terrain Textures");
        //Texture2DArray texture2DArray = new Texture2DArray(textures[0].width, textures[0].height, textures.Length, TextureFormat.RGB24, false, true);
        //for (int i=0; i < textures.Length; i++)
        //{
        //    texture2DArray.SetPixels(textures[i].GetPixels(), i);
        //    texture2DArray.Apply();
        //}
        //mat.SetTexture("_TerrainTextures", texture2DArray);
        //mat.SetInt("_TextureCount", textures.Length);
        //textures = null;
    }
    void Awake()
    {
        viewCamera = Camera.main;

        viewDist = Preferences.viewDist;
        instance = this;
        existingChunks = new Dictionary<Vector3Int, Chunk>();
        recyclableChunks = new Queue<Chunk>();
        chunks = new List<Chunk>();
        chunkMeshesToBuild = new Queue<Chunk>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        chunksVisibleInViewDist = Mathf.RoundToInt(viewDist / (float)chunkSize);
        sqrViewDist = viewDist * viewDist;
        minSqrViewDist = (chunkSize+1)*(chunkSize+1);
        scaler = scaling / (chunkSize / 16f);
        RenderSettings.fogEndDistance = viewDist * 0.5f;

    }

    private void Update()
    {
        UpdateVisibleChunks();
    }

    public void EnqueueChunk(Chunk chunk)
    {
        chunkMeshesToBuild.Enqueue(chunk);
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
        Vector3 viewerPosition = viewer.position;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(viewerPosition.x / chunkSize), Mathf.RoundToInt(viewerPosition.y / chunkSize), Mathf.RoundToInt(viewerPosition.z / chunkSize));

        int i = 0;
        if (existingChunks[viewerCoord].hasRequestMeshBuild)
        {
            i = 1;
            Chunk chunk = existingChunks[viewerCoord];
            UpdateMeshOfChunk(chunk);
        }
        for (; chunkMeshesToBuild.Count > 0 && i < maxMeshBuildsPerFrame; i++) {
            Chunk chunk = chunkMeshesToBuild.Dequeue();
            if (chunk.hasRequestMeshBuild)
                UpdateMeshOfChunk(chunk);
            else
                i--;
        }
        //System.GC.Collect();
    }

    void UpdateVisibleChunks()
    {
        Vector3 viewerPosition = viewer.position;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(viewerPosition.x / chunkSize), Mathf.RoundToInt(viewerPosition.y / chunkSize), Mathf.RoundToInt(viewerPosition.z / chunkSize));

        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            Chunk chunk = chunks[i];
            Vector3 centre = chunk.chunkPosition;
            Vector3 offsetFromViewer = viewerPosition - centre;
            Vector3 o = offsetFromViewer;//new Vector3(Mathf.Abs(offsetFromViewer.x), Mathf.Abs(offsetFromViewer.y), Mathf.Abs(offsetFromViewer.z)) - Vector3.one * chunkSize / 2;
            float sqrDist = o.sqrMagnitude;//new Vector3(Mathf.Max(0, o.x), Mathf.Max(0, o.y), Mathf.Max(0, o.z)).sqrMagnitude;

            if (sqrDist > sqrViewDist)
            {
                chunk.DestroyOrDisable();
                existingChunks.Remove(chunk.coord);
                recyclableChunks.Enqueue(chunk);
                chunks.RemoveAt(i);
            }
        }



        for (int i = 0; i <= chunksVisibleInViewDist * 2; i++)
        {
            int y = i / 2;
            if (i % 2 == 1) y *= -1;

            for (int j = 0; j <= chunksVisibleInViewDist * 2; j++)
            {
                int x = j / 2;
                if (j % 2 == 1) x *= -1;

                for (int k = 0; k <= chunksVisibleInViewDist * 2; k++)
                {
                    int z = k / 2;
                    if (k % 2 == 1) z *= -1;

                    Vector3Int coord = new Vector3Int(x, y, z) + viewerCoord;
                    
                    if (existingChunks.ContainsKey(coord))
                    {
                        continue;
                    }

                    Vector3 centre = coord * chunkSize;
                    Vector3 offsetFromViewer = viewerPosition - centre;
                    Vector3 o = offsetFromViewer;// new Vector3(Mathf.Abs(offsetFromViewer.x), Mathf.Abs(offsetFromViewer.y), Mathf.Abs(offsetFromViewer.z)) - Vector3.one * chunkSize / 2;
                    float sqrDist = o.sqrMagnitude;// new Vector3(Mathf.Max(0, o.x), Mathf.Max(0, o.y), Mathf.Max(0, o.z)).sqrMagnitude;

                    if (sqrDist < sqrViewDist)
                    {
                        Bounds bounds = new Bounds(centre, Vector3.one * chunkSize);
                        if (IsVisibleFrom(bounds, viewCamera) || sqrDist < minSqrViewDist)
                        {
                            if (recyclableChunks.Count > 0)
                            {
                                Chunk chunk = recyclableChunks.Dequeue();
                                chunk.Setup(coord, generateColliders, mat);
                                existingChunks.Add(coord, chunk);
                                chunks.Add(chunk);
                                //if (sqrDist <= minSqrViewDist) chunk.SetColliderEnabled(true);
                                //else chunk.SetColliderEnabled(false);
                            }
                            else
                            {
                                Chunk chunk = CreateChunk();
                                chunk.Setup(coord, generateColliders, mat);
                                existingChunks.Add(coord, chunk);
                                chunks.Add(chunk);
                                //if (sqrDist <= minSqrViewDist) chunk.SetColliderEnabled(true);
                                //else chunk.SetColliderEnabled(false);
                            }
                        }
                    }
                }
            }
        }
        DequeueChunksAndHandleMeshData();
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
