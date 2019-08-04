using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The <c>ChunkManager</c> class manages the creation and recycling of Chunks, as well
/// as the creation of meshes.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    /// <summary>
    /// A static reference to the ChunkManager instance in the scene.
    /// </summary>
    public static ChunkManager instance;

    /// <summary>
    /// The transform that the terrain should be created around.
    /// </summary>
    public Transform viewer;

    /// <summary>
    /// The prefab to be used to instantiate a new Chunk gameobject.
    /// </summary>
    public GameObject chunkPrefab;

    /// <summary>
    /// The material to be assigned to chunks.
    /// </summary>
    public Material mat;
    
    /// <summary>
    /// The size of chunks to be used.
    /// </summary>
    public int chunkSize = 8;

    /// <summary>
    /// Maximum number of Meshs that may be created per frame.
    /// 
    /// A higher number will have a big impact on performance.
    /// </summary>
    public int maxMeshBuildsPerFrame = 8;

    [HideInInspector]
    public int viewDist = 48;

    public float scaling = 64;

    /// <summary>
    /// Used to generate the entire radius of terrain on the first
    /// load, rather than just the portion visible by the camera.
    /// </summary>
    bool firstLoad = true;

    private int chunksVisibleInViewDist;
    private int sqrViewDist;
    private int minSqrViewDist;

    /// <summary>
    /// Data structures to store Chunks
    /// </summary>
    List<Chunk> chunks;
    Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> recyclableChunks;
    Stack<Chunk> chunkMeshesToBuild;

    /// <summary>
    /// The camera viewing the scene, should be close to the tracked transform
    /// </summary>
    Camera viewCamera;

    float chunkSizeMinusOne;

    private void Start()
    {
        Preferences.terrainScaler = scaling / (chunkSize / 16f);
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

        RenderSettings.fogEndDistance = viewDist * 0.6f;
        RenderSettings.fogStartDistance = chunkSize * 2f;
        firstLoad = true;
    }
 
    private void Update()
    {
        UpdateVisibleChunks();
        DequeueChunksAndHandleMeshData();
    }

    /// <summary>
    /// Destroys Chunks that are no longer visible, and creates Chunks that have come into range.
    /// </summary>
    void UpdateVisibleChunks()
    {
        Vector3 viewerPosition = viewer.transform.position;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(viewerPosition.x / chunkSizeMinusOne), Mathf.RoundToInt(viewerPosition.y / chunkSizeMinusOne), Mathf.RoundToInt(viewerPosition.z / chunkSizeMinusOne));


        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            Chunk chunk = chunks[i];
            Vector3 centre = chunk.chunkPosition;
            Vector3 offsetFromViewer = centre - viewerPosition;
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
                    Vector3 offsetFromViewer = centre - viewerPosition;
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
        firstLoad = false;
    }

    /// <summary>
    /// Marks a <c>Chunk</c> to be recycled.
    /// </summary>
    /// <param name="i"></param>
    /// <param name="chunk"></param>
    void RecycleChunk(int i, Chunk chunk)
    {
        existingChunks.Remove(chunk.coord);
        recyclableChunks.Enqueue(chunk);
        chunks.RemoveAt(i);

        chunk.DestroyOrDisable();
    }

    /// <summary>
    /// Creates meshs for a maximum of <c>maxMeshBuildsPerFrame</c> Chunks each frame, by poping
    /// them from the stack <c>chunkMeshesToBuild</c>.
    /// </summary>
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
        for (; chunkMeshesToBuild.Count > 0 && i < maxMeshBuildsPerFrame; i++)
        {
            Chunk chunk = chunkMeshesToBuild.Pop();
            if (chunk.hasRequestMeshBuild)
                UpdateMeshOfChunk(chunk);
            else
                i--;
        }
        System.GC.Collect();
    }

    /// <summary>
    /// Builds a <c>Mesh</c> from the <c>MeshData</c> stored in the <c>Chunk</c>
    /// </summary>
    /// <param name="chunk"></param>
    void UpdateMeshOfChunk(Chunk chunk)
    {
        // Clears the old mesh
        chunk.mesh.Clear();

        // Sets the vertices, triangles, and colors.
        chunk.mesh.vertices = chunk.meshData.vertices;
        chunk.mesh.triangles = chunk.meshData.triangles;
        chunk.mesh.colors = chunk.meshData.colors;

        // Optimizes the mesh for faster rendering and CollisionMesh baking
        chunk.mesh.Optimize();

        // Calculates the normals of the mesh
        chunk.mesh.RecalculateNormals();

        // Assigns the mesh to the meshFilter and meshCollider to update it.
        chunk.meshFilter.mesh = chunk.mesh;
        chunk.meshCollider.sharedMesh = chunk.mesh;

        // Tells the Chunk to generate trees, if it has been created for the first time.
        if (chunk.isInitialGeneration)
            chunk.GenerateTrees();
        chunk.isInitialGeneration = false;
        chunk.hasRequestMeshBuild = false;
    }

    /// <summary>
    /// Add a Chunk to the stack of chunks waiting to have a mesh created.
    /// </summary>
    /// <param name="chunk"></param>
    public void EnqueueChunk(Chunk chunk)
    {
        chunkMeshesToBuild.Push(chunk);
    }

    /// <summary>
    /// Determines if a particular <c>Bounds</c> object is visible by the camera.
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="camera"></param>
    /// <returns></returns>
    bool IsVisibleFrom(Bounds bounds, Camera camera)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    /// <summary>
    /// Instantiates a new chunk, used only when there are no existing chunks
    /// available for recycling.
    /// </summary>
    /// <returns></returns>
    Chunk CreateChunk()
    {
        GameObject chunk = Instantiate(chunkPrefab);
        chunk.transform.parent = transform;
        Chunk newChunk = chunk.GetComponent<Chunk>();
        return newChunk;
    }

    /// <summary>
    /// Returns a list of all visible chunks.
    /// </summary>
    /// <returns></returns>
    public List<Chunk> GetVisibleChunks()
    {
        return chunks;
    }
}
