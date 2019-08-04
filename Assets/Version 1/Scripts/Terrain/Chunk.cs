using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;

/// <summary>
/// Class <c>Chunk</c> creates and stores density points, as well as MeshData
/// representing a chunk of terrain. Chunks are recycled when they go out of
/// view, instead of being destroyed.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public Vector3Int coord;
    public Vector3 chunkPosition;

    int chunkSize;
    int voxelsPerChunkSqr;
    float voxelsPerChunkAlmostOne;

    public GameObject treePrefab;

    [HideInInspector]
    public Mesh mesh;

    [HideInInspector]
    public MeshFilter meshFilter;
    [HideInInspector]
    public MeshRenderer meshRenderer;
    [HideInInspector]
    public MeshCollider meshCollider;

    Dictionary<Vector3Int, Vector2> modifiedPoints = new Dictionary<Vector3Int, Vector2>();
    Vector2[] points;
    bool pointsHaveBeenGenerated;

    [HideInInspector]
    public MeshData meshData;
    public bool hasRequestMeshBuild;
    public bool isInitialGeneration;

    bool pointsHaveBeenModified = false;

    public bool surfaceChunk = false;

    private CancellationTokenSource cancellationToken;

    private List<GameObject> entities;

    /// <summary>
    /// Gets the components of a chunk when the GameObject is first created.
    /// </summary>
    private void Awake()
    {
        // Gets the MeshFilter, MeshRendered and MeshCollider attached to the Chunk.
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        // Caches some useful values
        chunkSize = ChunkManager.instance.chunkSize;
        voxelsPerChunkSqr = chunkSize * chunkSize; 
        voxelsPerChunkAlmostOne = (chunkSize-1)/(float)chunkSize;
    }


    /// <summary>
    /// The method <c>DestroyOrDisable</c> prepares a <c>Chunk</c> to be recycled by the
    /// <c>ChunkManager</c>
    /// </summary>
    public void DestroyOrDisable()
    {
        // Disable the GameObject
        gameObject.SetActive(false);

        // TODO Here is where we will save the chunk before unloading it.

        // Destroy all the entities attached to the Chunk
        for (int i=entities.Count-1; i >= 0; i--)
        {
            Destroy(entities[i]);
        }

        // Free up some memory
        mesh.Clear();
        modifiedPoints.Clear();
        points = null;
        meshData.vertices = null;
        meshData.colors = null;
        meshData.triangles = null;
        hasRequestMeshBuild = false;

        // If a GenerateChunk task is running, cancel it.
        cancellationToken.Cancel();
    }

    /// <summary>
    /// Called by <c>ChunkManager</c> to setup a chunk, either new or recycled.
    /// </summary>
    /// <param name="coord">The chunk coordinate</param>
    /// <param name="material">The material to be assigned to this <c>Chunk</c>'s MeshRenderer</param>
    public void Setup(Vector3Int coord, Material material)
    {
        this.coord = coord;
        this.chunkPosition = coord * (chunkSize-1);
        transform.position = chunkPosition;

        if (mesh == null)
            mesh = new Mesh();
        else
            mesh.Clear();

        meshRenderer.material = material;

        points = new Vector2[chunkSize * chunkSize * chunkSize];

        entities = new List<GameObject>();
        gameObject.SetActive(true);

        pointsHaveBeenGenerated = false;
        pointsHaveBeenModified = true;

        // Create a new CancellationTokenSource, to be used by the GenerateChunk task
        cancellationToken = new CancellationTokenSource();

        isInitialGeneration = true;
        surfaceChunk = false;

        // Begin the process of constructing a mesh
        GetMesh();
    }


    /// <summary>
    /// Begin a new Task to prepare the MeshData for the chunk,
    /// then call BuildMesh from the main thread upon completion.
    /// </summary>
    void GetMesh()
    {
        if (pointsHaveBeenModified)
        {
            pointsHaveBeenModified = false;
            CancellationToken token = cancellationToken.Token;
            Task<MeshData> task = Task.Run(() =>
            {
                return GenerateChunk();
            }, token);
            // Once the Task completes, if it has not been cancelled,
            // call BuildMesh from the main thread.
            task.ContinueWith(prevTask =>
            {
                if (!prevTask.IsCanceled)
                {
                    meshData = prevTask.Result;
                    hasRequestMeshBuild = true;
                    //prevTask.Dispose();
                    if (meshData.vertices.Length > 0)
                    {
                        ChunkManager.instance.EnqueueChunk(this);
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    /// <summary>
    /// Runs on a worker thread. This method applies the Marching Cubes algorithm to
    /// extract a surface with an ISO value of 0 (Values range between -1 and +1).
    /// </summary>
    /// <returns>Returns prepared MeshData.</returns>
    MeshData GenerateChunk()
    {
        // If points have not yet been generated, then generate them.
        if (!pointsHaveBeenGenerated)
        {
            GetPoints();
            pointsHaveBeenGenerated = true;
        }

        // A list to store the Triangles
        List<Triangle> triangles = new List<Triangle>();

        // March through the 3D density values grid.
        int voxelsPerAxis = chunkSize - 1;
        for (int z = 0; z < voxelsPerAxis; z++)
        {
            for (int y = 0; y < voxelsPerAxis; y++)
            {
                for (int x = 0; x < voxelsPerAxis; x++)
                {
                    // Get the vertices and values at the 8 corners of the cube.
                    CubeCorner[] cubeCorners =
                    {
                            GetCubeCorner(x, y, z),
                            GetCubeCorner(x+1, y, z),
                            GetCubeCorner(x+1, y, z+1),
                            GetCubeCorner(x, y, z+1),
                            GetCubeCorner(x, y+1, z),
                            GetCubeCorner(x+1, y+1, z),
                            GetCubeCorner(x+1, y+1, z+1),
                            GetCubeCorner(x, y+1, z+1)
                        };

                    // Calculate the configuration number (256 possibilities)
                    int configNumber = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (cubeCorners[i].value.x < 0)
                        {
                            configNumber |= (int)Mathf.Pow(2, i);
                        }
                    }

                    // Add triangles according to the predefined triangulation for the specified configuration
                    for (int i = 0; triangulation[configNumber][i] != -1; i += 3)
                    {
                        int a0 = cornerIndexAFromEdge[triangulation[configNumber][i]];
                        int b0 = cornerIndexBFromEdge[triangulation[configNumber][i]];

                        int a1 = cornerIndexAFromEdge[triangulation[configNumber][i + 1]];
                        int b1 = cornerIndexBFromEdge[triangulation[configNumber][i + 1]];

                        int a2 = cornerIndexAFromEdge[triangulation[configNumber][i + 2]];
                        int b2 = cornerIndexBFromEdge[triangulation[configNumber][i + 2]];

                        Triangle tri;
                        tri.a = InterpolateVerts(cubeCorners[a0], cubeCorners[b0]);
                        tri.b = InterpolateVerts(cubeCorners[a1], cubeCorners[b1]);
                        tri.c = InterpolateVerts(cubeCorners[a2], cubeCorners[b2]);
                        tri.color = Color.white;
                        triangles.Add(tri);
                    }
                }
            }
        }

        // Prepare MeshData from the triangles List

        int triangleCount = triangles.Count;


        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        int[] tris = new int[triangleCount * 3];

        //// Smooth shading
        //Dictionary<Vector3, int> vertexToIndex = new Dictionary<Vector3, int>();


        for (int i = 0; i < triangleCount; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                ////Smooth shading
                //Vector3 vertex = triangles[i][j];
                //Color color = triangles[i].color;
                //if (vertexToIndex.ContainsKey(vertex))
                //{
                //    tris[i * 3 + j] = vertexToIndex[vertex];
                //} else
                //{
                //    vertices.Add(vertex);
                //    colors.Add(color);
                //    int index = vertexToIndex.Count;
                //    vertexToIndex.Add(vertex, index);
                //    tris[i * 3 + j] = index;
                //}

                // Flat shading
                tris[i * 3 + j] = i * 3 + j;
                vertices.Add(triangles[i][j]);
                colors.Add(triangles[i].color);
            }
        }

        MeshData meshData;
        meshData.vertices = vertices.ToArray();
        meshData.colors = colors.ToArray();
        meshData.triangles = tris;

        return meshData;
    }

    /// <summary>
    /// Gets the initial points for this <c>Chunk</c>, if the point is in <c>modifiedPoints</c>
    /// then that value will be used, otherwise the value is generated with Perlin noise.
    /// </summary>
    void GetPoints()
    {
        for (int z = 0; z < chunkSize; z++)
        {
            float zCoord = ((float)z / (float)chunkSize) + coord.z * voxelsPerChunkAlmostOne;

            for (int y = 0; y < chunkSize; y++)
            {
                float yCoord = ((float)y / (float)chunkSize) + coord.y * voxelsPerChunkAlmostOne;
                float yGlobal = y + coord.y * (chunkSize - 1);

                for (int x = 0; x < chunkSize; x++)
                {
                    float xCoord = ((float)x / (float)chunkSize) + coord.x * voxelsPerChunkAlmostOne;

                    if (modifiedPoints.ContainsKey(new Vector3Int(x, y, z)))
                    {
                        points[z * voxelsPerChunkSqr + y * chunkSize + x] = modifiedPoints[new Vector3Int(x, y, z)];
                    }
                    else
                    {
                        points[z * voxelsPerChunkSqr + y * chunkSize + x] = GenerateValueAtPoint(new Vector4(xCoord, yCoord, zCoord, yGlobal));
                    }
                }
            }

        }
    }

    /// <summary>
    /// Uses layers Perlin noise to generate the density value at a specific point.
    /// </summary>
    /// <param name="point">
    /// point.x, point.y, point.z represent the position of the point in multiples of <c>chunkSize</c>.
    /// point.w represents the y value of the point in global coordinates.</param>
    /// <returns></returns>
    public static Vector2 GenerateValueAtPoint(Vector4 point)
    {
        // TODO Explain this code
        float localScaler = 0.75f * Preferences.terrainScaler + 2f * Preferences.terrainScaler * Perlin.Fbm(point.x, Preferences.seed * 1.25f, point.z, 1);
        point.x /= localScaler;
        point.y /= localScaler;
        point.z /= localScaler;

        float baseNoise = 1;
        float heightMap = GetHeightAtPoint(new Vector2(point.x, point.z));
        baseNoise *= 1 - Mathf.Clamp01(point.w - heightMap);
        baseNoise *= 1 - point.w / Preferences.maxTerrainHeight;
        baseNoise = Mathf.Clamp01(baseNoise);

        float overHang = Perlin.Fbm(point.x * 4f, point.y * 4f, point.z * 4f, 1);
        float caveBiomeMap = Mathf.Abs(1 - Mathf.PerlinNoise(point.x, point.z));
        float caveToSurfaceFalloff = Mathf.Pow(Mathf.Clamp(heightMap * 1.5f - (point.w - heightMap * 0.5f), 0, heightMap) / heightMap, 2);

        float final = Mathf.Lerp(baseNoise, overHang, caveBiomeMap * caveToSurfaceFalloff);
        return new Vector2((1 - Mathf.Clamp01(final)) * 2 - 1f, 0);
    }

    /// <summary>
    /// Get the base height of the terrain at a specified point. Not very accurate, because the base height is affected by caves.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public static float GetHeightAtPoint(Vector2 point)
    {
        float maxLocalHeight = Perlin.Fbm(point.x / 2f, Preferences.seed + 0.5f * Preferences.seed, point.x / 2f, 1) * Preferences.maxTerrainHeight;
        float localFadeExponent = Mathf.Max(1, Perlin.Fbm(point.x / 2f, Preferences.seed + 1.5f, point.x / 2f, 1) * 5f);
        float heightMap = Mathf.Clamp01(Mathf.Pow(Perlin.Fbm(point.x, Preferences.seed, point.x, 3), localFadeExponent) + Perlin.Fbm(point.x / 4f, Preferences.seed * 1.75f, point.x / 4f, 1) * 0.8f) * maxLocalHeight;
        return heightMap;
    }

    /// <summary>
    /// Method <c>GenerateTrees</c> is called after the initial chunk generation. Places procedurally generated trees
    /// at randomly chosen flat points on the terrain.
    /// </summary>
    public void GenerateTrees()
    {
        TestIsSurfaceChunk();
        if (surfaceChunk)
        {
            // We enable this so that we can place trees only on parts of the chunk that are open to the sky (In theory)
            Physics.queriesHitBackfaces = true;
            float v = Mathf.PerlinNoise(coord.x * voxelsPerChunkAlmostOne, coord.z * voxelsPerChunkAlmostOne);
            if (v > 0.3f)
            {
                int treeCount = (int)(Random.value * 8 * v);
                for (int i = 0; i < treeCount; i++)
                {
                    Vector3 coords = new Vector3(Random.value * chunkSize, chunkSize, Random.value * chunkSize);
                    RaycastHit hit;
                    if (Physics.Raycast(new Ray(transform.TransformPoint(coords), Vector3.down), out hit, chunkSize) && hit.transform.gameObject.layer.Equals(LayerMask.NameToLayer("Terrain")))
                    {
                        // If point is flat, and facing upwards, add a tree.
                        if (Vector3.Dot(Vector3.down, hit.normal) < -0.75f)
                        {
                            GameObject newTree = GameObject.Instantiate(treePrefab, transform);
                            newTree.transform.position = hit.point + Vector3.down * 0.075f;
                        }
                    }
                }
                // Disable for normal operation
                Physics.queriesHitBackfaces = false;
            }
        }
    }

    /// <summary>
    /// Raycasts downwards from the maximum terrain height, to each integer point on the xz-plane
    /// of the chunk. If the ray hits this chunk, then it is a surface chunk. The loop breaks after
    /// one Raycast hits the chunk.
    /// </summary>
    public void TestIsSurfaceChunk()
    {
        if (mesh.vertexCount == 0)
        {
            return;
        }
        for (int i = 0; i < chunkSize; i++)
        {
            for (int j = 0; j < chunkSize; j++)
            {
                RaycastHit hit;
                Vector3 origin = new Vector3(i, 0, j);
                origin = transform.TransformPoint(origin);
                origin.y = Preferences.maxTerrainHeight + 10;
                if (Physics.Raycast(new Ray(origin, Vector3.down), out hit, Preferences.maxTerrainHeight + 10) && hit.transform.gameObject.Equals(gameObject))
                {
                    surfaceChunk = true;
                    break;
                }
            }
            if (surfaceChunk) break;
        }
    }

    /// <summary>
    /// Check's to see if a point is in this <c>Chunk</c>.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public bool ContainsPoint(Vector3 point)
    {
        return chunkPosition.x <= point.x && point.x < chunkPosition.x + chunkSize
            && chunkPosition.y <= point.y && point.y < chunkPosition.y +  chunkSize
            && chunkPosition.z <= point.z && point.z < chunkPosition.z + chunkSize;
    }

    /// <summary>
    /// Returns the density value at a given point.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public Vector2 GetValueAtPoint(Vector3Int point)
    {
        point.Clamp(Vector3Int.zero, Vector3Int.one * chunkSize);
        return points[point.z * voxelsPerChunkSqr + point.y * chunkSize + point.x];
    }

    /// <summary>
    /// Modifies the density value at the specified point, and notifies the <c>Chunk</c>
    /// that its points have been updated.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="value"></param>
    public void ModifyChunkAtPoint(Vector3Int point, Vector2 value)
    {
        point.Clamp(Vector3Int.zero, Vector3Int.one * chunkSize);
        if (modifiedPoints.ContainsKey(point))
        {
            modifiedPoints[point] = value;
        } else
        {
            modifiedPoints.Add(point, value);
        }
        points[point.z * chunkSize * chunkSize + point.y * chunkSize + point.x] = value;
        pointsHaveBeenModified = true;
        NotifyPointsUpdate();
    }

    /// <summary>
    /// Requests the mesh be regenerated.
    /// </summary>
    void NotifyPointsUpdate()
    {
        GetMesh();
    }

    /// <summary>
    /// Cancel running tasks when the application quits.
    /// </summary>
    private void OnApplicationQuit()
    {
        cancellationToken.Cancel();
    }

    /// <summary>
    /// <c>MeshData</c> stores the data need to create a mesh: vertices, triangles,
    /// and colors.
    /// </summary>
    public struct MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Color[] colors;
    }

    /// <summary>
    /// <c>Triangle</c> stores the three vertices of a triangle, and its color.
    /// </summary>
    struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Color color;

        /// <summary>
        /// Allows the vertices a, b, c to be accesed with indices 0, 1, 2.
        /// </summary>
        /// <param name="index">The indice of the vertex to be referenced.</param>
        /// <returns></returns>
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

    /// <summary>
    /// A "constructor" function for the <c>struct CubeCorner</c>.
    /// </summary>
    /// <returns>An initialized <c>CubeCorner</c></returns>
    CubeCorner GetCubeCorner(int x, int y, int z)
    {
        CubeCorner corner;
        corner.position = new Vector3(x, y, z);
        corner.value = points[z * chunkSize * chunkSize + y * chunkSize + x];
        return corner;
    }

    /// <summary>
    /// Uses linear interpolation to find the point between two <c>CubeCorner</c> 
    /// where the value change is equal to <c>isoValue</c> 
    /// </summary>
    /// <param name="a">The first <c>CubeCorner</c></param>
    /// <param name="b">The second <c>CubeCorner</c></param>
    /// <param name="isoValue">The value which represents a surface. (Default=0f)</param>
    /// <returns>The linearly interpolated <c>Vector3</c></returns>
    Vector3 InterpolateVerts(CubeCorner a, CubeCorner b, float isoValue=0f)
    {
        float t = (isoValue-a.value.x) / (b.value.x - a.value.x);
        t = Mathf.Clamp01(t);
        return a.position + t * (b.position - a.position);
    }

    /// <summary>
    /// The struct <c>CubeCorner</c> stores the position of a corner of a voxel
    /// and its density value.
    /// </summary>
    struct CubeCorner
    {
        public Vector3 position;
        public Vector2 value;
    }

    /// <summary>
    /// A lookup table for all the 256 configurations of Marching Cubes.
    /// </summary>
    static readonly int[][] triangulation = {
            new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
            new int[] { 8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1 },
            new int[] { 3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1 },
            new int[] { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
            new int[] { 4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1 },
            new int[] { 9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1 },
            new int[] { 10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1 },
            new int[] { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
            new int[] { 5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1 },
            new int[] { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1 },
            new int[] { 2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
            new int[] { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1 },
            new int[] { 11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1 },
            new int[] { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1 },
            new int[] { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1 },
            new int[] { 11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1 },
            new int[] { 2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1 },
            new int[] { 6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
            new int[] { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1 },
            new int[] { 6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
            new int[] { 6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1 },
            new int[] { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1 },
            new int[] { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1 },
            new int[] { 3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
            new int[] { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1 },
            new int[] { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1 },
            new int[] { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
            new int[] { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1 },
            new int[] { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1 },
            new int[] { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1 },
            new int[] { 10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
            new int[] { 1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1 },
            new int[] { 0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1 },
            new int[] { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
            new int[] { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1 },
            new int[] { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1 },
            new int[] { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1 },
            new int[] { 3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1 },
            new int[] { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1 },
            new int[] { 10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
            new int[] { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1 },
            new int[] { 7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
            new int[] { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1 },
            new int[] { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1 },
            new int[] { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1 },
            new int[] { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1 },
            new int[] { 0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1 },
            new int[] { 7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1 },
            new int[] { 7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1 },
            new int[] { 10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1 },
            new int[] { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1 },
            new int[] { 7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1 },
            new int[] { 6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1 },
            new int[] { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1 },
            new int[] { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1 },
            new int[] { 8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1 },
            new int[] { 1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1 },
            new int[] { 10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1 },
            new int[] { 10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1 },
            new int[] { 9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1 },
            new int[] { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1 },
            new int[] { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1 },
            new int[] { 7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1 },
            new int[] { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1 },
            new int[] { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1 },
            new int[] { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1 },
            new int[] { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1 },
            new int[] { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1 },
            new int[] { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1 },
            new int[] { 6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1 },
            new int[] { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1 },
            new int[] { 6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1 },
            new int[] { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1 },
            new int[] { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1 },
            new int[] { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1 },
            new int[] { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1 },
            new int[] { 9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1 },
            new int[] { 1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1 },
            new int[] { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1 },
            new int[] { 0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1 },
            new int[] { 11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1 },
            new int[] { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1 },
            new int[] { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1 },
            new int[] { 2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1 },
            new int[] { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1 },
            new int[] { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1 },
            new int[] { 1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1 },
            new int[] { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1 },
            new int[] { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1 },
            new int[] { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1 },
            new int[] { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1 },
            new int[] { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1 },
            new int[] { 9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1 },
            new int[] { 5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1 },
            new int[] { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1 },
            new int[] { 8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1 },
            new int[] { 9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1 },
            new int[] { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1 },
            new int[] { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1 },
            new int[] { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1 },
            new int[] { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1 },
            new int[] { 11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1 },
            new int[] { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1 },
            new int[] { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1 },
            new int[] { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1 },
            new int[] { 1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1 },
            new int[] { 4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1 },
            new int[] { 0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1 },
            new int[] { 1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] { 0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
        };

    /// <summary>
    /// A lookup table to get the first corner from an edge on a cube.
    /// </summary>
    static readonly int[] cornerIndexAFromEdge = {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            0,
            1,
            2,
            3
        };

    /// <summary>
    /// A lookup table to get the second corner from an edge on a cube.
    /// </summary>
    static readonly int[] cornerIndexBFromEdge = {
            1,
            2,
            3,
            0,
            5,
            6,
            7,
            4,
            4,
            5,
            6,
            7
        };
}
