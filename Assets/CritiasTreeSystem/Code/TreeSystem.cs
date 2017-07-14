// Copyright Ioan-Bogdan Lazu. All Rights Reserved.

using System;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct TreeSystemStoredInstance
{
    public int m_TreeHash;
    public Matrix4x4 m_PositionMtx;
    public Vector3 m_WorldPosition;
    public Vector3 m_WorldScale;
    // World Y rotation stored in radians, for the billboards
    public float m_WorldRotation;
    public Bounds m_WorldBounds;
}

[System.Serializable]
public struct TreeSystemBoundingSphere
{
    public Vector4 m_CenterRadius;

    public TreeSystemBoundingSphere(Vector3 center, float radius)
    {
        m_CenterRadius.x = center.x;
        m_CenterRadius.y = center.y;
        m_CenterRadius.z = center.z;
        m_CenterRadius.w = radius;
    }
}

[System.Serializable]
public class TreeSystemStructuredTrees
{
    public Bounds m_BoundsBox;
    // Used for the 'CullingGroup' API
    public TreeSystemBoundingSphere m_BoundsSphere;

    public RowCol m_Position;

    public TreeSystemStoredInstance[] m_Instances;
    // TODO: hold extra LOD data to see LOD level, fade level etc...
    // TODO: maybe instantiate this data only if the cell is visible in order to save memory
    public TreeSystemLODInstance[] m_InstanceData;
}

[System.Serializable]
public class TreeSystemLODData
{    
    public float m_StartDistance;
    public float m_EndDistance;

    public Material[] m_Materials;
    public Mesh m_Mesh;

    // BEGIN RUNTIME UPDATED DATA
    public Renderer m_TreeRenderer;
    public MaterialPropertyBlock m_Block;
    // END RUNTIME UPDATED DATA

    public void CopyBlock()
    {        
        m_TreeRenderer.GetPropertyBlock(m_Block);
    }

    public bool IsInRange(float distance)
    {
        if (distance < m_StartDistance || distance > m_EndDistance)
            return false;

        return true;
    }
}

[System.Serializable]
public class TreeSystemPrototypeData
{
    // BEGIN DATA MANUALLY SET
    public TextAsset m_TreeBillboardData;
    // END DATA MANUALLY SET

    // BEGIN DATA GENERATED
    public GameObject m_TreePrototype;
    // Not using index any more, but hash so that we are sure that it is not lost upon any reorder
    public int m_TreePrototypeHash;
    public Vector3 m_Size; // Width, height, bottom. To be used by the tree system for each instance
    public Material m_BillboardBatchMaterial; // Billboard material to be used for tree billboards    
    public Vector2[] m_VertBillboardUVs;
    public Vector2[] m_HorzBillboardUVs;

    // Maximum LOD level
    public int m_MaxLod3DIndex;
    // END DATA GENERATED

    // BEGIN UPDATED AT RUNTIME
    public TreeSystemLODData[] m_LODData;
    // END UPDATED AT RUNTIME
}

/**
 * This guy is going to be stored at the cell's tree index, so that we don't need to hold that data internally
 */
[System.Serializable]
public struct TreeSystemLODInstance
{
    public int m_LODLevel;
    public float m_LODTransition;
    public float m_LODFullFade;
}

// public class TreeSystem
[System.Serializable]
public class TreeSystemTerrain
{
    public Terrain m_ManagedTerrain;    

    public Bounds m_ManagedTerrainBounds;
    public Matrix4x4 m_ManagedTerrainLocalToWorld;
    public Matrix4x4 m_ManagedTerrainWorldToLocal;
    public Vector3 m_ManagedTerrainSizes;

    public int m_CellSize;
    public int m_CellCount;

    // If we need to get the closes 1 neighbor, or 2 neighbors etc...
    public int m_NeighborRetrievalDepth = 1;

    // Trees for that terrain
    public TreeSystemStructuredTrees[] m_Cells;

    // API for culling groups
    public BoundingSphere[] m_CullingGroupSpheres;
    public CullingGroup m_CullingGroup;

    // TODO: not used at the moment, modify after
    // Build at runtime, for very quick neighbor retrieval
    public TreeSystemStructuredTrees[,] m_CellsStructured;
}

[System.Serializable]
public class TreeSystemSettings
{
    [Header("Optimization")]

    [Tooltip("Must be between range [0...CELL_SIZE]")]
    [Range(0, 2000f)]
    public float m_MaxTreeDistance = 300;
    
    [Space(20)]
    [Header("Shadow")]

    [Tooltip("If we should apply shadow popping correction. Will make sure that trees that are within the shadow distance are drawn as shadow-only even if they are invisible")]
    public bool m_ApplyShadowPoppingCorrection = true;
    // Even if invisible, trees that are at a distance smaller than this one will be drawn 'ShadowsOnly'
    // Used to mitigate the shadow popping, until we'll have a decent shadow-caster culling algorithm
    [Range(0, 2000f)]
    public float m_ShadowDrawDistance = 50;

    [Space(20)]
    [Header("Collsion")]

    [Tooltip("If we should apply tree collision and not use the default terrain one")]
    public bool m_ApplyTreeColliders = true;
    // After how many meters the 'TreeColliders' transform moves we refresh the colliders
    [Range(0, 2000f)]
    public float m_ColliderRefreshDistance = 5;
    // Trees at what distance around us will have collisions
    [Range(0, 2000f)]
    public float m_ColliderSetDistance = 20;

    [Space(20)]
    [Header("Rendering")]    

    [Tooltip("Defaults to 'Default'")]
    public string m_UsedLayer = "Default";
    [Tooltip("Defaults to 'Camera.main' for occlsion culling")]
    public Camera m_UsedCamera;

    [Tooltip("If the trees dissapear disable this")]
    public bool m_UseOcclusion = false;

    // Hard-coded, no reason to clutter people with extra data
    public readonly float m_LODTranzitionThreshold = 5.0f;
    public readonly float m_LODFadeSpeed = 5.0f;
}

#if UNITY_EDITOR

[CustomEditor(typeof(TreeSystem))]
public class TreeSystemEditor : Editor
{    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TreeSystem system = target as TreeSystem;
               
        GUILayout.Space(20);
       
        if (GUILayout.Button("Apply Settings"))
        {
            system.SetTreeDistance(system.m_Settings.m_MaxTreeDistance);
            system.SetMandatoryShadowDistance(system.m_Settings.m_ShadowDrawDistance);

            system.SetApplyColliders(system.m_Settings.m_ApplyTreeColliders);
            system.SetCollisionDistance(system.m_Settings.m_ColliderSetDistance);
            system.SetCollisionRefreshDistance(system.m_Settings.m_ColliderRefreshDistance);
        }        
    }
}

#endif

public class TreeSystem : MonoBehaviour
{
    private static readonly int MAX_BATCH = 1000;

    public TreeColliders m_TreeColliders;

    // Data to add
    public TreeSystemSettings m_Settings = new TreeSystemSettings();
        
    // Shader used data
    private int m_ShaderIDFadeBillboard;
    private int m_ShaderIDFadeLODFull;
    private int m_ShaderIDFadeLODDetail;
    private int m_ShaderIDBillboardScaleRotation;

    public static void SetMaterialBillProps(TreeSystemPrototypeData data, Material m)
    {
        Vector2[] uv = data.m_VertBillboardUVs;

        Vector4[] UV_U = new Vector4[8];
        Vector4[] UV_V = new Vector4[8];

        for (int i = 0; i < 8; i++)
        {
            // 4 by 4 elements
            UV_U[i].x = uv[4 * i + 0].x;
            UV_U[i].y = uv[4 * i + 1].x;
            UV_U[i].z = uv[4 * i + 2].x;
            UV_U[i].w = uv[4 * i + 3].x;

            UV_V[i].x = uv[4 * i + 0].y;
            UV_V[i].y = uv[4 * i + 1].y;
            UV_V[i].z = uv[4 * i + 2].y;
            UV_V[i].w = uv[4 * i + 3].y;
        }

        m.SetVectorArray("_UVVert_U", UV_U);
        m.SetVectorArray("_UVVert_V", UV_V);

        uv = data.m_HorzBillboardUVs;

        for (int i = 0; i < 1; i++)
        {
            // 4 by 4 elements
            UV_U[i].x = uv[4 * i + 0].x;
            UV_U[i].y = uv[4 * i + 1].x;
            UV_U[i].z = uv[4 * i + 2].x;
            UV_U[i].w = uv[4 * i + 3].x;

            UV_V[i].x = uv[4 * i + 0].y;
            UV_V[i].y = uv[4 * i + 1].y;
            UV_V[i].z = uv[4 * i + 2].y;
            UV_V[i].w = uv[4 * i + 3].y;
        }

        m.SetVector("_UVHorz_U", UV_U[0]);
        m.SetVector("_UVHorz_V", UV_V[0]);        
    }

    private void UpdateLODDataDistances(TreeSystemPrototypeData data)
    {
        float lodMultiplier = 1;

        LOD[] lods = data.m_TreePrototype.GetComponent<LODGroup>().GetLODs();
        TreeSystemLODData[] lodData = data.m_LODData;

        for (int i = 0; i < lodData.Length; i++)
        {
            // If it's the billboard move on
            if (i == lods.Length - 1)
                continue;

            TreeSystemLODData d = lodData[i];

            LOD lodCrt = new LOD();
            LOD lodPrv = new LOD();

            lodCrt = lods[i];
            lodCrt.screenRelativeTransitionHeight *= lodMultiplier;

            if (i > 0)
            {
                lodPrv = lods[i - 1];
                lodPrv.screenRelativeTransitionHeight *= lodMultiplier;
            }

            if (i == 0)
            {
                // If it's first 3D LOD
                d.m_StartDistance = 0;
                d.m_EndDistance = ((1.0f - lodCrt.screenRelativeTransitionHeight) * m_Settings.m_MaxTreeDistance);
            }
            else if (i == lodData.Length - 2)
            {
                // If it's last 3D LOD
                d.m_StartDistance = ((1.0f - lodPrv.screenRelativeTransitionHeight) * m_Settings.m_MaxTreeDistance);
                d.m_EndDistance = m_Settings.m_MaxTreeDistance;
            }
            else
            {
                // If it's a LOD in between
                d.m_StartDistance = ((1.0f - lodPrv.screenRelativeTransitionHeight) * m_Settings.m_MaxTreeDistance);
                d.m_EndDistance = ((1.0f - lodCrt.screenRelativeTransitionHeight) * m_Settings.m_MaxTreeDistance);
            }
        }
    }

    private void GenerateRuntimePrototypeData(TreeSystemPrototypeData data)
    {
        // All stuff that we draw must have billboard renderers, that's why it's trees
        LOD[] lods = data.m_TreePrototype.GetComponent<LODGroup>().GetLODs();
        TreeSystemLODData[] lodData = data.m_LODData;        

        // Set material's UV at runtime
        SetMaterialBillProps(data, data.m_BillboardBatchMaterial);        
        
        // Instantiate the last 3D lod
        GameObject weirdTree = Instantiate(lods[lods.Length - 2].renderers[0].gameObject);
        // weirdTree.hideFlags = HideFlags.HideAndDontSave;

        // Stick it to the camera
        weirdTree.transform.SetParent(Camera.main.transform);
        weirdTree.transform.localPosition = new Vector3(0, 0, -2);
        weirdTree.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Expand the bounds
        MeshRenderer mr = weirdTree.GetComponent<MeshRenderer>();
        MeshFilter mf = weirdTree.GetComponent<MeshFilter>();

        // Get an instance mesh from the weird tree
        Mesh m = mf.mesh;

        // Set the new bounds so that it's always drawn
        Bounds b = m.bounds; b.Expand(50.0f); m.bounds = b;
        mf.mesh = m;
        
        // Don't cast shadows
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;        

        // Get the same tree stuff as the grass
        for (int i = 0; i < lodData.Length; i++)
        {
            if (lods[i].renderers.Length != 1)
                Debug.LogError("Renderer length != 1! Problem!");

            // Assume only one renderer            
            TreeSystemLODData d = lodData[i];

            // Add a new MBP and set the renderer from which we are going to copy the wind data from
            d.m_Block = new MaterialPropertyBlock();
            d.m_TreeRenderer = mr;

            // Set the full array here because based on the docs, i'm not sure when is this going to be refreshed
            // Quote:
            // "The array length can't be changed once it has been added to the block. If you subsequently try 
            // to set a longer array into the same property, the length will be capped to the original length and 
            // the extra items you tried to assign will be ignored."
            // So, we're making sure that we have those MAX_BATCH count of floats
            d.m_Block.SetFloatArray(m_ShaderIDFadeLODDetail, new float[MAX_BATCH]);
            d.m_Block.SetFloatArray(m_ShaderIDFadeLODFull, new float[MAX_BATCH]);
        }
        
        // Update the LOD distances
        UpdateLODDataDistances(data);
    }
    
    public static TreeSystem Instance;
    
    [HideInInspector] public TreeSystemPrototypeData[] m_ManagedPrototypes;        
    public Dictionary<int, TreeSystemPrototypeData> m_ManagedPrototypesIndexed;

    // Managed terrain systems
    [HideInInspector] public TreeSystemTerrain[] m_ManagedTerrains;       
    
    public void SetTreeDistance(float distance)
    {
        if (distance != m_Settings.m_MaxTreeDistance)
        {
            // So that we're sure that we don't have more shadow than the distance
            if (distance < m_Settings.m_ShadowDrawDistance)
                m_Settings.m_ShadowDrawDistance = distance;

            Shader.SetGlobalFloat("_TreeSystemDistance", distance);

            m_Settings.m_MaxTreeDistance = distance;

            for (int i = 0; i < m_ManagedPrototypes.Length; i++)
                UpdateLODDataDistances(m_ManagedPrototypes[i]);
        }
    }

    /**
     * Set the distance at which, even if invisible, the trees will have their shadow drawn.
     * 
     * Used to mitigate the shadow popping effect since we don't have a built-in Unity mechanism that
     * allows us to see if a object is casting shadows inside the frutum.
     */
    public void SetMandatoryShadowDistance(float distance)
    {
        m_Settings.m_ShadowDrawDistance = distance;
    }

    public void SetApplyColliders(bool applyColliders)
    {
        if (applyColliders)
            m_TreeColliders.StartCollisionUpdates();
        else
            m_TreeColliders.StopCollisionUpdates();
    }

    public void SetCollisionDistance(float distance)
    {
        m_Settings.m_ColliderSetDistance = distance;
        m_TreeColliders.UpdateSettings(m_Settings);
    }

    public void SetCollisionRefreshDistance(float distance)
    {
        m_Settings.m_ColliderRefreshDistance = distance;
        m_TreeColliders.UpdateSettings(m_Settings);
    }
    

    public float GetTreeDistance()
    {
        return m_Settings.m_MaxTreeDistance;
    }    

    public float GetTreeTanzitionThreshold()
    {
        return m_Settings.m_LODTranzitionThreshold;
    }

    private int m_UsedLayerId;

    void Awake()
    {        
        Instance = this;

        m_UsedLayerId = LayerMask.NameToLayer(m_Settings.m_UsedLayer);

        // Get the non-alloc version of the plane extraction
        MethodInfo info = typeof(GeometryUtility).GetMethod("Internal_ExtractPlanes", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(Plane[]), typeof(Matrix4x4) }, null);
        ExtractPlanes = Delegate.CreateDelegate(typeof(Action<Plane[], Matrix4x4>), info) as Action<Plane[], Matrix4x4>;

        // Set maximum tree distance
        Shader.SetGlobalFloat("_TreeSystemDistance", m_Settings.m_MaxTreeDistance);

        m_ShaderIDFadeLODFull = Shader.PropertyToID("master_LODFadeFull");
        m_ShaderIDFadeLODDetail = Shader.PropertyToID("master_LODFadeDetail");
        m_ShaderIDFadeBillboard = Shader.PropertyToID("master_LODFade");

        m_ShaderIDBillboardScaleRotation = Shader.PropertyToID("_InstanceScaleRotation");

        // We need matrices as 'indentity'
        m_MtxLODTemp_0 = new Matrix4x4[MAX_BATCH];
        m_MtxLODTemp_1 = new Matrix4x4[MAX_BATCH];
        m_MtxLODTemp_2 = new Matrix4x4[MAX_BATCH];
        m_MtxLODTemp_3 = new Matrix4x4[MAX_BATCH];
        m_MtxLODTemp_4 = new Matrix4x4[MAX_BATCH];

        for (int i = 0; i < MAX_BATCH; i++)
        {
            m_MtxLODTemp_0[i] = Matrix4x4.identity;
            m_MtxLODTemp_1[i] = Matrix4x4.identity;
            m_MtxLODTemp_2[i] = Matrix4x4.identity;
            m_MtxLODTemp_3[i] = Matrix4x4.identity;
            m_MtxLODTemp_4[i] = Matrix4x4.identity;
        }

        // Generate runtime data
        for (int i = 0; i < m_ManagedPrototypes.Length; i++)
            GenerateRuntimePrototypeData(m_ManagedPrototypes[i]);
                
        // Build the dictionary based on the index
        m_ManagedPrototypesIndexed = new Dictionary<int, TreeSystemPrototypeData>();
        for (int i = 0; i < m_ManagedPrototypes.Length; i++)
            m_ManagedPrototypesIndexed.Add(m_ManagedPrototypes[i].m_TreePrototypeHash, m_ManagedPrototypes[i]);

        // If we don't have tree colliders initialize them
        if (!m_TreeColliders)
        {
            m_TreeColliders = gameObject.AddComponent<TreeColliders>();
            m_TreeColliders.m_OwnerSystem = this;
        }
    }
    
    // Temp indices for meshes
    private int[] m_IdxTempMesh = new int[MAX_BATCH];
    private float[] m_DstTempMesh = new float[MAX_BATCH];
    // Temp indices for shadows
    private int[] m_IdxTempShadow = new int[MAX_BATCH];
    private float[] m_DstTempShadow = new float[MAX_BATCH];

    private Action<Plane[], Matrix4x4> ExtractPlanes;
    private Plane[] m_PlanesTemp = new Plane[6];
    private Vector3 m_CameraPosTemp;

    void Start()
    {
        /*
        if (!m_UsedCamera)
            m_UsedCamera = Camera.main;
        */

        for (int i = 0; i < m_ManagedTerrains.Length; i++)
        {
            TreeSystemTerrain terrain = m_ManagedTerrains[i];

            if(terrain.m_ManagedTerrain != null)
                terrain.m_ManagedTerrain.drawTreesAndFoliage = false;

            terrain.m_CellsStructured = new TreeSystemStructuredTrees[terrain.m_CellCount, terrain.m_CellCount];

            CullingGroup cullingGroup = new CullingGroup();

            BoundingSphere[] bounds = new BoundingSphere[terrain.m_Cells.Length];
            
            for (int j = 0; j < terrain.m_Cells.Length; j++)
            {
                // TODO: maybe allocate dinamically at runtime based on cell visibility to save memory
                terrain.m_Cells[j].m_InstanceData = new TreeSystemLODInstance[terrain.m_Cells[j].m_Instances.Length];

                // TODO: structure cell data

                // Create the culling group data
                bounds[j] = new BoundingSphere(terrain.m_Cells[j].m_BoundsSphere.m_CenterRadius);

                // Structure cell data
                RowCol pos = terrain.m_Cells[j].m_Position;
                terrain.m_CellsStructured[pos.m_Row, pos.m_Col] = terrain.m_Cells[j];
            }

            if (!m_Settings.m_UsedCamera)
                cullingGroup.targetCamera = Camera.main;
            else
                cullingGroup.targetCamera = m_Settings.m_UsedCamera;

            cullingGroup.SetBoundingSpheres(bounds);
            cullingGroup.SetBoundingSphereCount(bounds.Length);            

            // Save the bounds just in case we might need them
            terrain.m_CullingGroupSpheres = bounds;
            terrain.m_CullingGroup = cullingGroup;
        }

        if (m_Settings.m_ApplyTreeColliders)
            SetApplyColliders(true);
    }

    void OnDestroy()
    {
        // Dispose all culling groups
        for (int i = 0; i < m_ManagedTerrains.Length; i++)
            m_ManagedTerrains[i].m_CullingGroup.Dispose();
    }

    private Vector3[] m_TempCorners = new Vector3[8];

    private int m_DataIssuedMeshTrees = 0;
    private int m_DataIssuedShadows = 0;
    private int m_DataIssuedTerrainCells = 0;
    private int m_DataIssuedTerrains = 0;
    private int m_DataIssuesDrawCalls = 0;
    private int m_DataIssuedTerrainCellsFull = 0;
    [HideInInspector] public int m_DataIssuedActiveColliders = 0;

    void Update()
    {        
        m_DataIssuedMeshTrees = 0;
        m_DataIssuedShadows = 0;
        m_DataIssuedTerrainCells = 0;
        m_DataIssuedTerrainCellsFull = 0;
        m_DataIssuedTerrains = 0;
        m_DataIssuesDrawCalls = 0;

        Camera camera = m_Settings.m_UsedCamera ? m_Settings.m_UsedCamera : Camera.main;

        // Calculate planes and camera position
        ExtractPlanes(m_PlanesTemp, camera.projectionMatrix * camera.worldToCameraMatrix);
        m_CameraPosTemp = camera.transform.position;        

        // Update the wind block data
        for (int proto = 0; proto < m_ManagedPrototypes.Length; proto++)
        {
            TreeSystemLODData[] lod = m_ManagedPrototypes[proto].m_LODData;
            for (int i = 0; i < lod.Length; i++)
                lod[i].CopyBlock();
        }
        
        float treeDistSqr = m_Settings.m_MaxTreeDistance * m_Settings.m_MaxTreeDistance;
        float shadowDistSqr = m_Settings.m_ShadowDrawDistance * m_Settings.m_ShadowDrawDistance;

        for (int i = 0; i < m_ManagedTerrains.Length; i++)
        {
            TreeSystemTerrain terrain = m_ManagedTerrains[i];

            // Get closest point

            /*
            Vector3 pt = terrain.m_ManagedTerrainBounds.ClosestPoint(m_CameraPosTemp);

            // Check if terrain is within reach range
            x = pt.x - m_CameraPosTemp.x;
            y = pt.y - m_CameraPosTemp.y;
            z = pt.z - m_CameraPosTemp.z;

            float distToTerrain = x * x + y * y + z * z;
            */

            float distToTerrain = terrain.m_ManagedTerrainBounds.SqrDistance(m_CameraPosTemp);

            // Enable/disable culling group execution based on terrain distance, since we don't want all of them running around
            if (distToTerrain < treeDistSqr)
            {
                if (terrain.m_CullingGroup.enabled == false)
                    terrain.m_CullingGroup.enabled = true;

                // If the terrain is within tree range
                ProcessTerrain(terrain, ref treeDistSqr, ref shadowDistSqr);
                m_DataIssuedTerrains++;
            }
            else
            {
                if(terrain.m_CullingGroup.enabled)
                    terrain.m_CullingGroup.enabled = false;
            }
        }

#if UNITY_EDITOR
        
#endif
    }

    private List<TreeSystemStructuredTrees> m_TempNeighbors = new List<TreeSystemStructuredTrees>();

    private void ProcessTerrain(TreeSystemTerrain terrain, ref float treeDistSqr, ref float shadowDistSqr)
    {
        // Get only the visible cells around the player
        int cellsCount = terrain.m_CellCount;
        Vector3 terrainLocal = terrain.m_ManagedTerrainWorldToLocal.MultiplyPoint3x4(m_CameraPosTemp);
        Vector3 terrSize = terrain.m_ManagedTerrainSizes;

        RowCol gridPos;

        GetTerrainGridIndex(out gridPos, terrainLocal, cellsCount, terrSize);

        // Get neighbors
        GetNeightbors(m_TempNeighbors, gridPos.m_Row, gridPos.m_Col, terrain.m_CellsStructured, ref cellsCount);

        if (m_TempNeighbors.Count <= 0)
            return;
        
        CullingGroup culling = terrain.m_CullingGroup;
        bool useOcclusion = m_Settings.m_UseOcclusion;

        // float x, y, z;

        // TODO: calculate based on bounds index the cells that we'll iterate like the grass system
        // We won't require to iterate all the cells but the neighbors of the current cell only

        // TODO: only get the data from the culling group API at the moment

        // Go bounds by bounds
        for (int cellIdx = 0; cellIdx < m_TempNeighbors.Count; cellIdx++)
        {
            TreeSystemStructuredTrees cell = m_TempNeighbors[cellIdx];

            // If we don't have any tree skip this cell
            if (cell.m_Instances.Length <= 0) continue;

            // And also check if the bounds are visible
            if (useOcclusion && culling.IsVisible(cellIdx) == false) continue;

            // Get closest point to cell

            /*
            Vector3 pt = cell.m_BoundsBox.ClosestPoint(m_CameraPosTemp);

            x = pt.x - m_CameraPosTemp.x;
            y = pt.y - m_CameraPosTemp.y;
            z = pt.z - m_CameraPosTemp.z;

            float distToCell = x * x + y * y + z * z;
            */

            float distToCell = cell.m_BoundsBox.SqrDistance(m_CameraPosTemp);

            if (distToCell < treeDistSqr && GeometryUtility.TestPlanesAABB(m_PlanesTemp, cell.m_BoundsBox))
            {
                // TODO: the same process when we are going to have terrain sub-cells                               
                ProcessTerrainCell(cell, ref treeDistSqr, ref shadowDistSqr);

                // If it's visible
                m_DataIssuedTerrainCells++;
            }
            // TODO: See for the case in which we are at the intersection of 4 cells and the cells are not visible to the frustum
            // BUT they have trees that 'might' cast shadows inside the frustum. Do that with a special thing that only checks the trees
            // for the shadow distance, and that's all
            // else if (m_ApplyShadowPoppingCorrection && distToCell < shadowDistSqr) { }
        }
    }

    private void ProcessTerrainCell(TreeSystemStructuredTrees cell, ref float treeDistSqr, ref float shadowDistSqr)
    {
        // Draw all trees instanced in MAX_BATCH chunks
        int tempIndexTree = 0;
        int tempIndexShadow = 0;
        float x, y, z;

        // If we are completely inside frustum we don't need to AABB test each tree
        bool insideFrustum = TUtils.IsCompletelyInsideFrustum(m_PlanesTemp, TUtils.BoundsCorners(ref cell.m_BoundsBox, ref m_TempCorners));

        // If it is completely inside the frustum notify us
        if (insideFrustum) m_DataIssuedTerrainCellsFull++;

        // Tree instances
        TreeSystemStoredInstance[] treeInstances = cell.m_Instances;
        
        // TODO: Hm... if we take that hash it doesn't mean that it's the first visible one...
        int treeHash = treeInstances[0].m_TreeHash;
        int currentTreeHash = treeHash;

        int shadowHash = treeHash;
        int currentShadowHash = shadowHash;

        for (int treeIndex = 0; treeIndex < treeInstances.Length; treeIndex++)
        {            
            // This is an order of magnitude faster than (treeInstances[treeIndex].m_WorldPosition - pos).sqrMagnitude
            // since it does not initiate with a ctor an extra vector during the computation
            x = treeInstances[treeIndex].m_WorldPosition.x - m_CameraPosTemp.x;
            y = treeInstances[treeIndex].m_WorldPosition.y - m_CameraPosTemp.y;
            z = treeInstances[treeIndex].m_WorldPosition.z - m_CameraPosTemp.z;

            float distToTree = x * x + y * y + z * z;
            
            if (insideFrustum)
            {
                // If we are completely inside the frustum we don't need to check each individual tree's bounds
                if (distToTree <= treeDistSqr)
                {
                    currentTreeHash = treeInstances[treeIndex].m_TreeHash;

                    if (tempIndexTree >= MAX_BATCH || treeHash != currentTreeHash)
                    {
                        // We're sure that they will not be shadows only
                        IssueDrawTrees(m_ManagedPrototypesIndexed[treeHash], cell, m_IdxTempMesh, m_DstTempMesh, tempIndexTree, false);
                        tempIndexTree = 0;

                        // Update the hash
                        treeHash = currentTreeHash;
                    }

                    m_IdxTempMesh[tempIndexTree] = treeIndex;
                    m_DstTempMesh[tempIndexTree] = Mathf.Sqrt(distToTree);
                    tempIndexTree++;

                    m_DataIssuedMeshTrees++;
                }
            }
            else
            {
                // TODO: In the future instead of 'GeometryUtility.TestPlanesAABB' have our own function that
                // checks if the object is within the shadow volume, that is if it casts shadow inside our frustum
                // Just generate our own set of planes that we'll use...

                // If we are not completely inside the frustum we need to check the bounds of each individual tree
                if (distToTree <= treeDistSqr && GeometryUtility.TestPlanesAABB(m_PlanesTemp, treeInstances[treeIndex].m_WorldBounds))
                {
                    currentTreeHash = treeInstances[treeIndex].m_TreeHash;

                    if (tempIndexTree >= MAX_BATCH || treeHash != currentTreeHash)
                    {
                        IssueDrawTrees(m_ManagedPrototypesIndexed[treeHash], cell, m_IdxTempMesh, m_DstTempMesh, tempIndexTree, false);
                        tempIndexTree = 0;

                        // Update the hash
                        treeHash = currentTreeHash;
                    }

                    m_IdxTempMesh[tempIndexTree] = treeIndex;
                    m_DstTempMesh[tempIndexTree] = Mathf.Sqrt(distToTree);
                    tempIndexTree++;

                    m_DataIssuedMeshTrees++;
                }
                // Same operation as above but with different matrices
                else if(m_Settings.m_ApplyShadowPoppingCorrection && distToTree < shadowDistSqr)
                {
                    // Even if we are invisible but within the shadow sitance still cast shadows... 
                    // (not the most efficient method though, waiting for the complete one)

                    currentShadowHash = treeInstances[treeIndex].m_TreeHash;

                    if(tempIndexShadow >= MAX_BATCH || shadowHash != currentShadowHash)
                    {
                        IssueDrawTrees(m_ManagedPrototypesIndexed[shadowHash], cell, m_IdxTempShadow, m_DstTempShadow, tempIndexShadow, true);
                        tempIndexShadow = 0;

                        // Update the shadow hash
                        shadowHash = currentShadowHash;
                    }

                    m_IdxTempShadow[tempIndexShadow] = treeIndex;
                    m_DstTempShadow[tempIndexShadow] = Mathf.Sqrt(distToTree);
                    tempIndexShadow++;

                    m_DataIssuedShadows++;
                }
            }
        } // End cell tree iteration

        if (tempIndexTree > 0)
        {
            // Get a tree hash from the first element of the array so that we know for sure that we use the correct prototype data
            IssueDrawTrees(m_ManagedPrototypesIndexed[treeInstances[m_IdxTempMesh[0]].m_TreeHash], cell,
                m_IdxTempMesh, m_DstTempMesh, tempIndexTree, false);
            
            tempIndexTree = 0;
        }

        if(tempIndexShadow > 0)
        {
            IssueDrawTrees(m_ManagedPrototypesIndexed[treeInstances[m_IdxTempShadow[0]].m_TreeHash], cell,
                m_IdxTempShadow, m_DstTempShadow, tempIndexShadow, true);

            tempIndexShadow = 0;
        }
    }

    private static readonly int MAX_LOD_COUNT = 5;

    // 'MAX_LOD_COUNT' is the maximum possible count of LOD levels. Modify based on the tree with the highest LOD value.
    // If we have a tree with 8 LODS manually add values until 7 (m_MtxLODTemp_5, m_MtxLODTemp_6, ... m_MtxLODTemp_7) and follow the example
    private Matrix4x4[] m_MtxLODTemp_0;
    private Matrix4x4[] m_MtxLODTemp_1;
    private Matrix4x4[] m_MtxLODTemp_2;
    private Matrix4x4[] m_MtxLODTemp_3;
    private Matrix4x4[] m_MtxLODTemp_4;

    private float[] m_MtxLODTranzDetail_0 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzDetail_1 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzDetail_2 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzDetail_3 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzDetail_4 = new float[MAX_BATCH];

    private float[] m_MtxLODTranzFull_0 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzFull_1 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzFull_2 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzFull_3 = new float[MAX_BATCH];
    private float[] m_MtxLODTranzFull_4 = new float[MAX_BATCH];

    // How many of that certain lod level we have
    private int[] m_MtxLODTempCount = new int[MAX_LOD_COUNT];

    private void IssueDrawTrees(TreeSystemPrototypeData data, TreeSystemStructuredTrees trees, int[] indices, float[] dist, int count, bool shadowsOnly)
    {
        for(int i = 0; i < MAX_LOD_COUNT; i++)
            m_MtxLODTempCount[i] = 0;        

        TreeSystemLODData[] lodData = data.m_LODData;

        TreeSystemStoredInstance[] treeInstances = trees.m_Instances;
        TreeSystemLODInstance[] lodInstanceData = trees.m_InstanceData;

        int maxLod3D = data.m_MaxLod3DIndex;

        // Build the batched stuff. We want to batch the same LOD and draw them using instancing
        for (int i = 0; i < count; i++)
        {
            ProcessLOD(ref lodData, ref maxLod3D, ref lodInstanceData[indices[i]], ref dist[i]);

            int idx = indices[i];
            int currentLODLevel = lodInstanceData[idx].m_LODLevel;

            // Collect that LOD level
            if (currentLODLevel == 0)
            {
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m00 = treeInstances[idx].m_PositionMtx.m00;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m01 = treeInstances[idx].m_PositionMtx.m01;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m02 = treeInstances[idx].m_PositionMtx.m02;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m03 = treeInstances[idx].m_PositionMtx.m03;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m10 = treeInstances[idx].m_PositionMtx.m10;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m11 = treeInstances[idx].m_PositionMtx.m11;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m12 = treeInstances[idx].m_PositionMtx.m12;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m13 = treeInstances[idx].m_PositionMtx.m13;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m20 = treeInstances[idx].m_PositionMtx.m20;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m21 = treeInstances[idx].m_PositionMtx.m21;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m22 = treeInstances[idx].m_PositionMtx.m22;
                m_MtxLODTemp_0[m_MtxLODTempCount[currentLODLevel]].m23 = treeInstances[idx].m_PositionMtx.m23;                

                m_MtxLODTranzDetail_0[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODTransition;
                m_MtxLODTranzFull_0[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODFullFade;
            }
            else if (currentLODLevel == 1)
            {
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m00 = treeInstances[idx].m_PositionMtx.m00;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m01 = treeInstances[idx].m_PositionMtx.m01;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m02 = treeInstances[idx].m_PositionMtx.m02;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m03 = treeInstances[idx].m_PositionMtx.m03;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m10 = treeInstances[idx].m_PositionMtx.m10;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m11 = treeInstances[idx].m_PositionMtx.m11;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m12 = treeInstances[idx].m_PositionMtx.m12;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m13 = treeInstances[idx].m_PositionMtx.m13;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m20 = treeInstances[idx].m_PositionMtx.m20;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m21 = treeInstances[idx].m_PositionMtx.m21;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m22 = treeInstances[idx].m_PositionMtx.m22;
                m_MtxLODTemp_1[m_MtxLODTempCount[currentLODLevel]].m23 = treeInstances[idx].m_PositionMtx.m23;                

                m_MtxLODTranzDetail_1[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODTransition;
                m_MtxLODTranzFull_1[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODFullFade;
            }
            else if (currentLODLevel == 2)
            {
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m00 = treeInstances[idx].m_PositionMtx.m00;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m01 = treeInstances[idx].m_PositionMtx.m01;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m02 = treeInstances[idx].m_PositionMtx.m02;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m03 = treeInstances[idx].m_PositionMtx.m03;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m10 = treeInstances[idx].m_PositionMtx.m10;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m11 = treeInstances[idx].m_PositionMtx.m11;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m12 = treeInstances[idx].m_PositionMtx.m12;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m13 = treeInstances[idx].m_PositionMtx.m13;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m20 = treeInstances[idx].m_PositionMtx.m20;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m21 = treeInstances[idx].m_PositionMtx.m21;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m22 = treeInstances[idx].m_PositionMtx.m22;
                m_MtxLODTemp_2[m_MtxLODTempCount[currentLODLevel]].m23 = treeInstances[idx].m_PositionMtx.m23;
                
                m_MtxLODTranzDetail_2[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODTransition;
                m_MtxLODTranzFull_2[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODFullFade;

            }
            else if (currentLODLevel == 3)
            {
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m00 = treeInstances[idx].m_PositionMtx.m00;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m01 = treeInstances[idx].m_PositionMtx.m01;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m02 = treeInstances[idx].m_PositionMtx.m02;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m03 = treeInstances[idx].m_PositionMtx.m03;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m10 = treeInstances[idx].m_PositionMtx.m10;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m11 = treeInstances[idx].m_PositionMtx.m11;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m12 = treeInstances[idx].m_PositionMtx.m12;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m13 = treeInstances[idx].m_PositionMtx.m13;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m20 = treeInstances[idx].m_PositionMtx.m20;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m21 = treeInstances[idx].m_PositionMtx.m21;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m22 = treeInstances[idx].m_PositionMtx.m22;
                m_MtxLODTemp_3[m_MtxLODTempCount[currentLODLevel]].m23 = treeInstances[idx].m_PositionMtx.m23;
                
                m_MtxLODTranzDetail_3[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODTransition;
                m_MtxLODTranzFull_3[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODFullFade;

            }
            else if (currentLODLevel == 4)
            {
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m00 = treeInstances[idx].m_PositionMtx.m00;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m01 = treeInstances[idx].m_PositionMtx.m01;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m02 = treeInstances[idx].m_PositionMtx.m02;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m03 = treeInstances[idx].m_PositionMtx.m03;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m10 = treeInstances[idx].m_PositionMtx.m10;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m11 = treeInstances[idx].m_PositionMtx.m11;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m12 = treeInstances[idx].m_PositionMtx.m12;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m13 = treeInstances[idx].m_PositionMtx.m13;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m20 = treeInstances[idx].m_PositionMtx.m20;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m21 = treeInstances[idx].m_PositionMtx.m21;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m22 = treeInstances[idx].m_PositionMtx.m22;
                m_MtxLODTemp_4[m_MtxLODTempCount[currentLODLevel]].m23 = treeInstances[idx].m_PositionMtx.m23;
                
                m_MtxLODTranzDetail_4[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODTransition;
                m_MtxLODTranzFull_4[m_MtxLODTempCount[currentLODLevel]] = lodInstanceData[idx].m_LODFullFade;
            }

            m_MtxLODTempCount[currentLODLevel]++;
        }

        // Now that the data is built, issue it
        for (int i = 0; i <= maxLod3D; i++)
        {
            if (i == 0)
                Draw3DLODInstanced(ref lodData[i], ref m_MtxLODTemp_0, ref m_MtxLODTranzDetail_0, ref m_MtxLODTranzFull_0, ref m_MtxLODTempCount[i], ref shadowsOnly);
            else if (i == 1)
                Draw3DLODInstanced(ref lodData[i], ref m_MtxLODTemp_1, ref m_MtxLODTranzDetail_1, ref m_MtxLODTranzFull_1, ref m_MtxLODTempCount[i], ref shadowsOnly);
            else if (i == 2)
                Draw3DLODInstanced(ref lodData[i], ref m_MtxLODTemp_2, ref m_MtxLODTranzDetail_2, ref m_MtxLODTranzFull_2, ref m_MtxLODTempCount[i], ref shadowsOnly);
            else if (i == 3)
                Draw3DLODInstanced(ref lodData[i], ref m_MtxLODTemp_3, ref m_MtxLODTranzDetail_3, ref m_MtxLODTranzFull_3, ref m_MtxLODTempCount[i], ref shadowsOnly);
            else if (i == 4)
                Draw3DLODInstanced(ref lodData[i], ref m_MtxLODTemp_4, ref m_MtxLODTranzDetail_4, ref m_MtxLODTranzFull_4, ref m_MtxLODTempCount[i], ref shadowsOnly);
        }
    }

    /**
     * Must be called only if the camera distance is smaller than the maximum tree view distance.
     */
    private void ProcessLOD(ref TreeSystemLODData[] data, ref int max3DLOD, ref TreeSystemLODInstance inst, ref float cameraDistance)
    {        
        if (cameraDistance <= m_Settings.m_MaxTreeDistance + m_Settings.m_LODTranzitionThreshold)
        {
            for (int i = 0; i < data.Length - 1; i++)
            {
                if (data[i].IsInRange(cameraDistance))
                {
                    // Calculate lod tranzition value
                    if (cameraDistance > data[i].m_EndDistance - m_Settings.m_LODTranzitionThreshold)
                        inst.m_LODTransition = 1.0f - (data[i].m_EndDistance - cameraDistance) / m_Settings.m_LODTranzitionThreshold;
                    else
                        inst.m_LODTransition = 0.0f;

                    inst.m_LODLevel = i;
                    break;
                }
            }
            
            
            if (inst.m_LODLevel == max3DLOD && inst.m_LODFullFade < 1)
            {
                // If we are the last 3D lod then we animate nicely
                inst.m_LODFullFade += Time.deltaTime * m_Settings.m_LODFadeSpeed;
            }
            else if (inst.m_LODFullFade < 1)
            {
                // If we are not the lost LOD level simply set the value to 1
                inst.m_LODFullFade = 1f;
            } 
                    
        }

        
        else
        {
            inst.m_LODLevel = max3DLOD;
            if (inst.m_LODFullFade > 0) inst.m_LODFullFade -= Time.deltaTime * m_Settings.m_LODFadeSpeed;
        }
        
    }

    // Draw and process routines
    private void DrawProcessedLOD(ref TreeSystemLODData[] data, ref int maxLod3D, ref TreeSystemLODInstance lodInst, ref TreeSystemStoredInstance inst, ref bool shadowsOnly)
    {
        int lod = lodInst.m_LODLevel;

        // Draw the stuff with the material specific for each LOD
        Draw3DLOD(ref data[lod], ref lodInst, ref inst, ref shadowsOnly);        
    }
    
    public void Draw3DLODInstanced(ref TreeSystemLODData data, ref Matrix4x4[] positions, ref float[] lodDetail, ref float[] lodFull, ref int count, ref bool shadowsOnly)
    {                
        if (count > 0)
        {
            data.m_Block.SetFloatArray(m_ShaderIDFadeLODDetail, lodDetail);

            // Removed at the moment
            // data.m_Block.SetFloatArray(m_ShaderIDFadeLODFull, lodFull);

            for (int mat = 0; mat < data.m_Materials.Length; mat++)
            {
                Graphics.DrawMeshInstanced(data.m_Mesh, mat, data.m_Materials[mat], positions, count, data.m_Block,
                    shadowsOnly == false ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,
                    true, m_UsedLayerId, m_Settings.m_UsedCamera);

                m_DataIssuesDrawCalls++;
            }
        }
    }

    float[] m_TempLOD = new float[1];

    public void Draw3DLOD(ref TreeSystemLODData data, ref TreeSystemLODInstance lodInst, ref TreeSystemStoredInstance inst, ref bool shadowsOnly)
    {
        m_TempLOD[0] = lodInst.m_LODTransition;
        data.m_Block.SetFloatArray(m_ShaderIDFadeLODDetail, m_TempLOD);
        m_TempLOD[0] = lodInst.m_LODFullFade;
        data.m_Block.SetFloatArray(m_ShaderIDFadeLODFull, m_TempLOD);        

        for (int mat = 0; mat < data.m_Materials.Length; mat++)
        {
            Graphics.DrawMesh(data.m_Mesh, inst.m_PositionMtx, data.m_Materials[mat], m_UsedLayerId, m_Settings.m_UsedCamera, mat, data.m_Block,
                shadowsOnly == false ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,
                true);

            m_DataIssuesDrawCalls++;
        }
    }    

    public static void GetTerrainGridIndex(out RowCol position, Vector3 localTerrain, int cellCount, Vector3 terrainSizes)
    {
        position.m_Row = Mathf.FloorToInt((localTerrain.x / terrainSizes.x) * cellCount);
        position.m_Col = Mathf.FloorToInt((localTerrain.z / terrainSizes.z) * cellCount);
    }

    void GetNeightbors(List<TreeSystemStructuredTrees> neighbors, int row, int column, TreeSystemStructuredTrees[,] cells, ref int cellCount)
    {
        if (neighbors.Count > 0)
            neighbors.Clear();

        // Depth of 1, that is maximum 1 cell around us
        const int depth = 1;

        for (int r = -depth; r <= depth; r++)
        {
            for (int c = -depth; c <= depth; c++)
            {
                if (ValidIndex(row + r, column + c, ref cellCount))
                    neighbors.Add(cells[row + r, column + c]);
            }
        }
    }

    bool ValidIndex(int row, int column, ref int cells)
    {
        return row >= 0 && row < cells && column >= 0 && column < cells;
    }

    public string GetDrawInfo()
    {
        return "Issued terrains: " + m_DataIssuedTerrains + 
            "\nIssued cells: " + m_DataIssuedTerrainCells +
            "\nIssued full cells: " + m_DataIssuedTerrainCellsFull +
            "\nIssued mesh trees: " + m_DataIssuedMeshTrees + 
            "\nIssued shadow trees: " + m_DataIssuedShadows + 
            "\nIssued draw calls: " + m_DataIssuesDrawCalls + 
            "\nIssued active colliders: " + m_DataIssuedActiveColliders +
            "\nUse colliders: " + m_Settings.m_ApplyTreeColliders +
            "\nUse shadow correction: " + m_Settings.m_ApplyShadowPoppingCorrection;
    }
}
