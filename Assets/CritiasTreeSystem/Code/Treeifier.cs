// Copyright Ioan-Bogdan Lazu. All Rights Reserved.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR

using System.Xml;
using UnityEditor;

[System.Serializable]
public enum EGroup
{
    GROUP_DEFAULT,
    GROUP_1,
    GROUP_2,
    GROUP_COUNT
}

[System.Serializable]
public class TerrainExtract
{
    [Tooltip("Managed terrain that will have their trees extracted")]
    public Terrain m_ManagedTerrain;
    [Tooltip("It's cell size")]
    public int m_CellSize;
}

[System.Serializable]
public class ItemExtract
{
    [Tooltip("Item to extract and add to system")]
    public GameObject m_ItemPrefab;
    [Tooltip("If it is a SpeedTree object or just a game object")]
    public bool m_IsSpeedTree = true;
    [Tooltip("Only applyies if this is a SpeedTree object, else it's ignored. If it is a SpeedTree then it's billboard will be added to the batched billboard patch")]
    public bool m_ExtractBillboard = true;

    // [Tooltip("If we want to add this tree to a specific rendering group")]
    // public EGroup m_ItemGroup;
}

[CustomEditor(typeof(Treeifier))]
public class TreeifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Treeifier system = target as Treeifier;

        
        if(GUILayout.Button("Extract Tree Prototype Data"))
        {
            system.GenerateTreePrototypeData();
            system.ExtractXMLTreePrototypeData();
            system.ExtractTreeLODPrototypeData();
        }
        
        if (GUILayout.Button("Generate Trees"))
        {
            system.GenerateTrees();
        }

        if (GUILayout.Button("Cell Info"))
        {
            system.PrintPossibleCellCounts();
        }
        

        /*
        myTarget.experience = EditorGUILayout.IntField("Experience", myTarget.experience);
        EditorGUILayout.LabelField("Level", myTarget.Level.ToString());

        GUILayout.Space(20);
        foreach(Terrain terrain in system.m_ManagedTerrains)
        {
            if (GUILayout.Button("Store [" + terrain.name + "] Trees"))
            {

            }

            if (GUILayout.Button("Restore [" + terrain.name + "] Trees"))
            {

            }
        }
        */

        // EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MainManagedTerrain"));

        DrawDefaultInspector();
    }
}

#endif

[ExecuteInEditMode]
public class Treeifier : MonoBehaviour
{
#if UNITY_EDITOR
    // Generated at edit-time, don't touch!
    [HideInInspector] public TreeSystemPrototypeData[] m_ManagedPrototypes;

    [Header("System Required Data")]
    public Mesh m_SystemQuad;

    // Billboard used shader
    public Shader m_BillboardShaderBatch;    
    public Shader m_TreeShaderMaster;

    [Header("User Data")]
    // Managed terrains that will have their trees extracted
    public TerrainExtract[] m_ManagedTerrains;
    public ItemExtract[] m_ItemsToExtract;
    
    [Header("Optimization Data")]
    public bool m_AddLODGroupToBillboards = true;
    public float m_BillboardScreenSize = 0.05f;

    public GameObject m_CellHolder;
    public bool m_DeleteCellsAfterGridify = true;

    // Where we'll store the data
    public string m_DataStorePath = "Assets/Atlantis Quest/System/Tree";    

    public void Start()
    {
        m_BillboardShaderBatch = Shader.Find("Critias/Nature/SpeedTree Bilboard Batch");
        m_TreeShaderMaster = Shader.Find("Critias/Nature/SpeedTree Master");

        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
        m_SystemQuad = primitive.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(primitive);
    }

    public void PrintPossibleCellCounts()
    {
        Debug.Log("------------------------------------");
        
        foreach (TerrainExtract t in m_ManagedTerrains)
        {
            Debug.Log("---- BEGIN TERRAIN INFO ----");
            TerrainUtils.CellInfo(t.m_ManagedTerrain);
            Debug.Log("---- END TERRAIN INFO ----");
        }

        Debug.Log("------------------------------------");                        
    }
    
    public void GenerateTreePrototypeData()
    {
        List<string> prefabNames = new List<string>();
        List<GameObject> prefabs = new List<GameObject>();

        System.Array.ForEach(m_ItemsToExtract, (x) => { prefabNames.Add(x.m_ItemPrefab.name); prefabs.Add(x.m_ItemPrefab); });

        if (TerrainUtils.TreeHashCheck(prefabNames.ToArray()))
        {
            Debug.LogError("Tree name hash collision, fix!");
            return;
        }

        GameObject[] proto = prefabs.ToArray();        
        List<TreeSystemPrototypeData> managed = new List<TreeSystemPrototypeData>();

        for (int i = 0; i < proto.Length; i++)
        {
            GameObject prefab = proto[i];

            if (PrefabUtility.GetPrefabType(prefab) != PrefabType.ModelPrefab ||
                prefab.GetComponent<LODGroup>() == null ||
                prefab.GetComponentInChildren<BillboardRenderer>() == null)
            {
                Debug.LogError("Invalid prefab: " + prefab.name + ". Make sure that it is a SpeedTree, that it contains a 'LODGroup' and that it has a 'BillboardRenderer' component.");
                continue;
            }

            TreeSystemPrototypeData data = new TreeSystemPrototypeData();
            data.m_TreePrototype = prefab;
            // Use hash here instead of the old index
            data.m_TreePrototypeHash = TUtils.GetStableHashCode(prefab.name);
                        
            // Instantiate LOD data that is going to be populated at runtime
            LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
            TreeSystemLODData[] lodData = new TreeSystemLODData[lods.Length];
            // Generate some partial LOD data that doesn't have to be calculated at runtime
            data.m_LODData = lodData;

            for (int lod = 0; lod < lodData.Length; lod++)
            {
                TreeSystemLODData d = new TreeSystemLODData();
                lodData[lod] = d;
            }
            
            data.m_MaxLod3DIndex = lodData.Length - 2;

            managed.Add(data);
        }

        m_ManagedPrototypes = managed.ToArray();

        // Try and set the prototypes to our tree system
        TreeSystem t = FindObjectOfType<TreeSystem>();
        if (t) t.m_ManagedPrototypes = m_ManagedPrototypes;            
    }
    
    public void ExtractXMLTreePrototypeData()
    {
        TreeSystemPrototypeData[] data = m_ManagedPrototypes;

        for(int i = 0; i < data.Length; i++)
        {
            TreeSystemPrototypeData d = data[i];

            if (d.m_TreePrototype == null)
            {
                Debug.LogError("Nothing set for data at index: " + i);
                continue;
            }            

            // Get the protorype's billboard asset
            BillboardRenderer bill = d.m_TreePrototype.GetComponentInChildren<BillboardRenderer>();

            if(bill == null)
            {
                Debug.LogError("Prototype: " + d.m_TreePrototype.name + " does not contain a billboard renderer! Items without billboard renderers are not supported at the moment!");
                continue;
            }

            BillboardAsset billAsset = bill.billboard;

            // Set sizes
            d.m_Size = new Vector3(billAsset.width, billAsset.height, billAsset.bottom);
            
            
            Vector4[] uvs = billAsset.GetImageTexCoords();

            // Ussualy 16
            d.m_VertBillboardUVs = new Vector2[uvs.Length * 4];
            // Just set the first UV's just to have something
            d.m_HorzBillboardUVs = new Vector2[4];

            for (int uvIdx = 0, billUv = 0; uvIdx < uvs.Length; uvIdx++, billUv += 4)
            {
                Vector4 extract = uvs[uvIdx];

                if (uvIdx == 0)
                {
                    d.m_HorzBillboardUVs[0] = new Vector2(extract.x, extract.y);
                    d.m_HorzBillboardUVs[1] = new Vector2(extract.x, extract.y) + new Vector2(0, Mathf.Abs(extract.w));
                    d.m_HorzBillboardUVs[2] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, Mathf.Abs(extract.w));
                    d.m_HorzBillboardUVs[3] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, 0);
                }

                // We are rotated
                if (extract.w < 0)
                {
                    d.m_VertBillboardUVs[billUv + 0] = new Vector2(extract.x, extract.y);
                    d.m_VertBillboardUVs[billUv + 1] = new Vector2(extract.x, extract.y) + new Vector2(0, Mathf.Abs(extract.w));
                    d.m_VertBillboardUVs[billUv + 2] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, Mathf.Abs(extract.w));
                    d.m_VertBillboardUVs[billUv + 3] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, 0);
                }
                else
                {
                    d.m_VertBillboardUVs[billUv + 0] = new Vector2(extract.x, extract.y);
                    d.m_VertBillboardUVs[billUv + 1] = new Vector2(extract.x, extract.y) + new Vector2(extract.z, 0);
                    d.m_VertBillboardUVs[billUv + 2] = new Vector2(extract.x, extract.y) + new Vector2(extract.z, extract.w);
                    d.m_VertBillboardUVs[billUv + 3] = new Vector2(extract.x, extract.y) + new Vector2(0, extract.w);
                }
            }

            Vector4 size = d.m_Size;
            size.w = 1;

            // Create the material with the texture references
            Material billboardMaterialBatch = new Material(m_BillboardShaderBatch);
            billboardMaterialBatch.SetTexture("_MainTex", bill.billboard.material.GetTexture("_MainTex"));
            billboardMaterialBatch.SetTexture("_BumpMap", bill.billboard.material.GetTexture("_BumpMap"));
            billboardMaterialBatch.SetVector("_Size", size);

            // Replace, don't delete
            // AssetDatabase.DeleteAsset(m_DataStorePath + "/" + d.m_TreePrototype.name + "_Mat.mat");
            AssetDatabase.CreateAsset(billboardMaterialBatch,
                m_DataStorePath + "/" + d.m_TreePrototype.name + "_Bill_Batch_Mat.mat");

            // Set the material
            d.m_BillboardBatchMaterial = billboardMaterialBatch;

            // Set billboard data
            TreeSystem.SetMaterialBillProps(d, d.m_BillboardBatchMaterial);
        }

        AssetDatabase.Refresh();
    }
    
    public void ExtractTreeLODPrototypeData()
    {
        TreeSystemPrototypeData[] proto = m_ManagedPrototypes;

        for (int i = 0; i < proto.Length; i++)
        {
            GameObject prefab = proto[i].m_TreePrototype;
            TreeSystemPrototypeData data = proto[i];

            if(prefab.GetComponent<LODGroup>() == null)
            {
                Debug.LogError("Prototype: " + prefab.name + " does not have a LOD group! Please fix and regenerate, or the system will break!");
                continue;
            }

            // Instantiate LOD data that is going to be populated at runtime
            LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
            TreeSystemLODData[] lodData = new TreeSystemLODData[lods.Length];
            // Generate some partial LOD data that doesn't have to be calculated at runtime
            data.m_LODData = lodData;

            for (int lod = 0; lod < lodData.Length; lod++)
            {
                TreeSystemLODData d = new TreeSystemLODData();
                lodData[lod] = d;

                if(lods[lod].renderers[0].gameObject.GetComponent<BillboardRenderer>())
                {
                    continue;
                }

                MeshRenderer rend = lods[lod].renderers[0].gameObject.GetComponent<MeshRenderer>();
                MeshFilter filter = lods[lod].renderers[0].gameObject.GetComponent<MeshFilter>();

                // Set the mesh we are drawing to the shared mesh                    
                d.m_Mesh = Instantiate(filter.sharedMesh);
                AssetDatabase.CreateAsset(d.m_Mesh,
                        m_DataStorePath + "/" + prefab.name + "_Master_Tree_Mesh_LOD_" + lod + ".asset");

                // Get the materials, create instances and set our SpeedTree master shader
                d.m_Materials = rend.sharedMaterials;

                for (int mat = 0; mat < d.m_Materials.Length; mat++)
                {
                    d.m_Materials[mat] = new Material(d.m_Materials[mat]);
                    d.m_Materials[mat].shader = m_TreeShaderMaster;

                    AssetDatabase.CreateAsset(d.m_Materials[mat],
                        m_DataStorePath + "/" + prefab.name + "_Master_Tree_Material_LOD" + lod + "_" + mat + ".mat");
                }
            }
            
            data.m_MaxLod3DIndex = lodData.Length - 2;
        }
    }

    public static void ReplaceAsset()
    {
        // AssetDatabase.
        // EditorUtility.CopySerialized
    }

    public void GenerateTrees()
    {        
        foreach(TerrainExtract t in m_ManagedTerrains)
        {
            if (t.m_ManagedTerrain.transform.rotation != Quaternion.identity)
            {
                Debug.LogError("Terrains must not be rotated, sorry!");
                return;
            }

            if (TerrainUtils.CanGridify(t.m_ManagedTerrain, t.m_CellSize) == false)
            {
                Debug.LogError("Can't gridify terrain [" + t.m_ManagedTerrain + "] with cell size: " + t.m_CellSize);
                return;
            }
        }
        
        List<TreeSystemTerrain> systemTerrains = new List<TreeSystemTerrain>();

        // Set the cell holder in the origin and mark it static      
        if(!m_CellHolder)
        {
            m_CellHolder = new GameObject("TreeSystemCellHolder");
        }

        // Use the extracted trees and add them to the terrain that it fits in
        List<GameObject> extraTrees = ExtractTreesExtra();

        m_CellHolder.transform.position = Vector3.zero;
        m_CellHolder.transform.localScale = Vector3.one;
        m_CellHolder.transform.rotation = Quaternion.identity;
        m_CellHolder.isStatic = true;

        for(int i = 0; i < m_ManagedTerrains.Length; i++)
        {
            TerrainExtract ter = m_ManagedTerrains[i];

            GameObject cellHolder = new GameObject();
            cellHolder.isStatic = true;
            cellHolder.name = ter.m_ManagedTerrain.name + "_TreeSystem_Managed_" + i;

            // Destroy any previous data
            if (m_CellHolder.transform.Find(cellHolder.name))
                DestroyImmediate(m_CellHolder.transform.Find(cellHolder.name).gameObject);

            // Set parent
            cellHolder.transform.parent = m_CellHolder.transform;            

            systemTerrains.Add(ProcessTerrain(ter.m_ManagedTerrain, ter.m_CellSize, cellHolder, extraTrees));
        }        

        // TODO: implement a scriptable object maybe? But it works fine with 400k+ trees so...
        FindObjectOfType<TreeSystem>().m_ManagedTerrains = systemTerrains.ToArray();
                
        for(int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            foreach(GameObject obj in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                if (obj.GetComponent<ExtraTrees>() != null)
                    obj.SetActive(false);
            }
        }        

        if(extraTrees.Count > 0)
        {
            Debug.LogError(extraTrees.Count + " extra trees did not find terrain location.");

            string names = "";
            foreach (GameObject tree in extraTrees)
                names += tree.name + ", ";

            Debug.LogError("Names:[ " + names + " ]");
        }

        Debug.Log("Finished extracting, extracted " + systemTerrains.Count + " terrains");

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // TODO: see from the list which trees fit in here
    private TreeSystemTerrain ProcessTerrain(Terrain terrain, int cellSize, GameObject cellHolder, List<GameObject> extraTrees)
    {
        TreeSystemTerrain systemTerrain = new TreeSystemTerrain();

        // Set system terrain data
        systemTerrain.m_ManagedTerrain = terrain;
        systemTerrain.m_ManagedTerrainBounds = terrain.GetComponent<TerrainCollider>().bounds;
        systemTerrain.m_ManagedTerrainLocalToWorld = terrain.transform.localToWorldMatrix;
        systemTerrain.m_ManagedTerrainWorldToLocal = terrain.transform.worldToLocalMatrix;
        systemTerrain.m_ManagedTerrainSizes = terrain.terrainData.size;

        systemTerrain.m_CellCount = TerrainUtils.GetCellCount(terrain, cellSize);
        systemTerrain.m_CellSize = cellSize;
        
        int cellCount;
        BoxCollider[,] collidersBox;
        SphereCollider[,] collidersSphere;

        // Gridify terrain
        TerrainUtils.Gridify(terrain, cellSize, out cellCount, out collidersBox, out collidersSphere, cellHolder, null);

        // Temporary structured data
        TreeSystemStructuredTrees[,] str = new TreeSystemStructuredTrees[cellCount, cellCount];
        List<TreeSystemStoredInstance>[,] strInst = new List<TreeSystemStoredInstance>[cellCount, cellCount];       
        List<TreeSystemStructuredTrees> list = new List<TreeSystemStructuredTrees>();

        // Insantiate the required data
        for (int r = 0; r < cellCount; r++)
        {
            for (int c = 0; c < cellCount; c++)
            {
                TreeSystemStructuredTrees s = new TreeSystemStructuredTrees();

                // Set the bounds, all in world space
                s.m_BoundsBox = collidersBox[r, c].bounds;
                s.m_BoundsSphere = new TreeSystemBoundingSphere(s.m_BoundsBox.center, collidersSphere[r,c].radius);

                // Set it's new position
                s.m_Position = new RowCol(r, c);

                str[r, c] = s;
                strInst[r, c] = new List<TreeSystemStoredInstance>();

                list.Add(s);
            }
        }

        // Delete cells since they might cause physics problems
        if (m_DeleteCellsAfterGridify)
        {
            for (int i = 0; i < cellCount; i++)
            {
                for (int j = 0; j < cellCount; j++)
                    DestroyImmediate(collidersBox[i, j].gameObject);
            }
        }

        int treeInstancesCount = 0, treeExtraCount = 0;

        TreeInstance[] terrainTreeInstances = terrain.terrainData.treeInstances;
        TreePrototype[] terrainTreeProto = terrain.terrainData.treePrototypes;

        Vector3 sizes = terrain.terrainData.size;

        for (int i = 0; i < terrainTreeInstances.Length; i++)
        {
            GameObject proto = terrainTreeProto[terrainTreeInstances[i].prototypeIndex].prefab;

            if (ShouldUsePrefab(proto) < 0)
                continue;

            treeInstancesCount++;

            // Get bounds for that mesh
            Bounds b = proto.transform.Find(proto.name + "_LOD0").gameObject.GetComponent<MeshFilter>().sharedMesh.bounds;

            // Calculate this from normalized terrain space to terrain's local space so that our row/col info are correct.
            // Do the same when testing for cell row/col in which the player is, transform to terrain local space
            Vector3 pos = TerrainUtils.TerrainToTerrainPos(terrainTreeInstances[i].position, terrain);
            int row = Mathf.Clamp(Mathf.FloorToInt(pos.x / sizes.x * cellCount), 0, cellCount - 1);
            int col = Mathf.Clamp(Mathf.FloorToInt(pos.z / sizes.z * cellCount), 0, cellCount - 1);

            pos = TerrainUtils.TerrainToWorldPos(terrainTreeInstances[i].position, terrain);
            Vector3 scale = new Vector3(terrainTreeInstances[i].widthScale, terrainTreeInstances[i].heightScale, terrainTreeInstances[i].widthScale);
            float rot = terrainTreeInstances[i].rotation;
            int hash = TUtils.GetStableHashCode(proto.name);

            Matrix4x4 mtx = Matrix4x4.TRS(pos, Quaternion.Euler(0, rot * Mathf.Rad2Deg, 0), scale);            

            TreeSystemStoredInstance inst = new TreeSystemStoredInstance();

            inst.m_TreeHash = hash;
            inst.m_PositionMtx = mtx;
            inst.m_WorldPosition = pos;
            inst.m_WorldScale = scale;
            inst.m_WorldRotation = rot;
            inst.m_WorldBounds = TUtils.LocalToWorld(ref b, ref mtx);

            strInst[row, col].Add(inst);
        }

        List<GameObject> containedTrees = new List<GameObject>();

        // Change if we're going to use something diff than 50 for max extent
        Bounds terrainExtendedBounds = systemTerrain.m_ManagedTerrainBounds;
        terrainExtendedBounds.Expand(new Vector3(0, 50, 0));

        // Same as a instance with minor diferences
        for (int i = 0; i < extraTrees.Count; i++)
        {
            GameObject treeInstance = extraTrees[i];
            
            // If the terrain contains the stuff
            if (terrainExtendedBounds.Contains(treeInstance.transform.position) == false)
                continue;

            treeExtraCount++;

            // Add the tree to the list of trees for removal
            containedTrees.Add(treeInstance);

            // Owner
            GameObject proto = GetPrefabOwner(treeInstance);

            // Get bounds for that mesh
            Bounds b = proto.transform.Find(proto.name + "_LOD0").gameObject.GetComponent<MeshFilter>().sharedMesh.bounds;

            // Calculate this from normalized terrain space to terrain's local space so that our row/col info are correct.
            // Do the same when testing for cell row/col in which the player is, transform to terrain local space
            Vector3 pos = TerrainUtils.TerrainToTerrainPos(TerrainUtils.WorldPosToTerrain(treeInstance.transform.position, terrain), terrain);
            
            int row = Mathf.Clamp(Mathf.FloorToInt(pos.x / sizes.x * cellCount), 0, cellCount - 1);
            int col = Mathf.Clamp(Mathf.FloorToInt(pos.z / sizes.z * cellCount), 0, cellCount - 1);

            pos = treeInstance.transform.position;
            Vector3 scale = treeInstance.transform.localScale;
            float rot = treeInstance.transform.rotation.eulerAngles.y * Mathf.Deg2Rad;

            // Set the hash
            int hash = TUtils.GetStableHashCode(proto.name);

            // Set the mtx
            Matrix4x4 mtx = Matrix4x4.TRS(pos, Quaternion.Euler(0, rot * Mathf.Rad2Deg, 0), scale);

            TreeSystemStoredInstance inst = new TreeSystemStoredInstance();

            inst.m_TreeHash = hash;
            inst.m_PositionMtx = mtx;
            inst.m_WorldPosition = pos;
            inst.m_WorldScale = scale;
            inst.m_WorldRotation = rot;
            inst.m_WorldBounds = TUtils.LocalToWorld(ref b, ref mtx);

            strInst[row, col].Add(inst);
        }

        // Remove the items from the extra trees
        foreach (GameObject tree in containedTrees)
            extraTrees.Remove(tree);

        // Generate the mesh that contain all the billboards
        for (int r = 0; r < cellCount; r++)
        {
            for (int c = 0; c < cellCount; c++)
            {
                if (strInst[r, c].Count <= 0)
                    continue;

                // Sort based on the tree hash so that we don't have to do many dictionary look-ups
                strInst[r, c].Sort((x, y) => x.m_TreeHash.CompareTo(y.m_TreeHash));

                // Set the new instances
                str[r, c].m_Instances = strInst[r, c].ToArray();

                // Build the meshes for each cell based on tree type
                List<TreeSystemStoredInstance> singleType = new List<TreeSystemStoredInstance>();
                int lastHash = strInst[r, c][0].m_TreeHash;

                foreach (TreeSystemStoredInstance inst in strInst[r, c])
                {
                    // If we have a new hash, consume all the existing instances
                    if (inst.m_TreeHash != lastHash)
                    {
                        TreeSystemPrototypeData data = GetPrototypeWithHash(lastHash);

                        if(ShouldBuildBillboardBatch(data.m_TreePrototype))
                            BuildTreeTypeCellMesh(cellHolder, str[r, c], singleType, data);

                        singleType.Clear();

                        // Update the hash
                        lastHash = inst.m_TreeHash;
                    }

                    // Add them to a list and when the hash changes begin the next generation
                    singleType.Add(inst);
                }

                if (singleType.Count > 0)
                {
                    TreeSystemPrototypeData data = GetPrototypeWithHash(singleType[0].m_TreeHash);

                    if (ShouldBuildBillboardBatch(data.m_TreePrototype))
                        BuildTreeTypeCellMesh(cellHolder, str[r, c], singleType, data);

                    singleType.Clear();
                }
            }
        }
        
        // Set the cells that contain the trees to the system terrain
        systemTerrain.m_Cells = list.ToArray();

        // Print extraction data
        Debug.Log("Extracted for terrain: " + terrain.name + " instance trees: " + treeInstancesCount + " extra trees: " + treeExtraCount);

        // Return it
        return systemTerrain;
    }    

    private List<Vector2> ExtractBillboards(XmlElement bills, bool vertical)
    {
        XmlElement verticalBills = bills;

        List<Vector2> allUv = new List<Vector2>();

        if (vertical)
        {
            foreach (XmlElement node in verticalBills.ChildNodes)
            {
                XmlElement elem = node;

                bool rotated = bool.Parse(elem.GetAttribute("Rotated"));

                Debug.Log("Rotated: " + rotated);

                string[] u = node["TexcoordU"].InnerText.Trim().Split(' ');
                string[] v = node["TexcoordV"].InnerText.Trim().Split(' ');

                Debug.Log("UV data: " + TUtils.ToString(u) + " " + TUtils.ToString(v));

                if (v.Length != u.Length || v.Length != 4)
                {
                    Debug.LogError("Something bad went parsing: " + u + " " + v);
                    continue;
                }

                List<Vector2> uv = new List<Vector2>();

                for (int j = 0; j < u.Length; j++)
                {
                    uv.Add(new Vector2(float.Parse(u[j]), float.Parse(v[j])));
                }

                Debug.Log("Extracted uv: " + TUtils.ToString(uv));
                allUv.AddRange(uv);
            }
        }
        else
        {
            string[] u = bills["TexcoordU"].InnerText.Trim().Split(' ');
            string[] v = bills["TexcoordV"].InnerText.Trim().Split(' ');

            Debug.Log("UV data: " + TUtils.ToString(u) + " " + TUtils.ToString(v));

            if (v.Length != u.Length || v.Length != 4)
            {
                Debug.LogError("Something bad went parsing: " + u + " " + v);
            }

            List<Vector2> uv = new List<Vector2>();

            for (int j = 0; j < u.Length; j++)
            {
                uv.Add(new Vector2(float.Parse(u[j]), float.Parse(v[j])));
            }

            Debug.Log("Extracted uv: " + TUtils.ToString(uv));
            allUv.AddRange(uv);
        }

        return allUv;
    }

    private void BuildTreeTypeCellMesh(GameObject owner, TreeSystemStructuredTrees cell, List<TreeSystemStoredInstance> trees, TreeSystemPrototypeData data)
    {        
        int[] originalTriangles = m_SystemQuad.triangles;
        
        RowCol pos = cell.m_Position;

        GameObject mesh = new GameObject();

        // Mark object as static
        GameObjectUtility.SetStaticEditorFlags(mesh, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

        mesh.transform.SetParent(owner.transform);
        mesh.name = "MeshCell[" + pos.m_Row + "_" + pos.m_Col + "_" + data.m_TreePrototype.name + "]";

        Vector3 worldScale = new Vector3(data.m_Size.x, data.m_Size.y, data.m_Size.x);

        // Set material
        MeshRenderer rend = mesh.AddComponent<MeshRenderer>();
        rend.sharedMaterial = data.m_BillboardBatchMaterial;

        MeshFilter filter = mesh.AddComponent<MeshFilter>();

        Mesh treeMesh = new Mesh();
        treeMesh.name = "TreeCell[" + pos.m_Row + "_" + pos.m_Col + "_" + data.m_TreePrototype.name + "]";

        List<Vector4> m_TempWorldPositions = new List<Vector4>();
        List<Vector3> m_TempWorldScales = new List<Vector3>();        
        List<Vector3> m_TempQuadVertices = new List<Vector3>();
        List<Vector4> m_TempQuadTangents = new List<Vector4>();
        List<Vector3> m_TempQuadNormals = new List<Vector3>();
        List<int> m_TempQuadIndices = new List<int>();

        Bounds newBounds = new Bounds();
        newBounds.center = cell.m_BoundsBox.center;

        // TODO: populate mesh data
        for (int treeIndex = 0; treeIndex < trees.Count; treeIndex++)
        {
            Vector3 position = trees[treeIndex].m_WorldPosition;
            Vector3 scale = trees[treeIndex].m_WorldScale;
            float rot = trees[treeIndex].m_WorldRotation;

            // Offset world position, by the grounding factor
            Vector3 instancePos = position;
            instancePos.y += data.m_Size.z;

            // Scale by the world scale too so that we don't have to do an extra multip
            Vector3 instanceScale = scale;
            instanceScale.Scale(worldScale);

            // Encapsulate bottom and top also
            newBounds.Encapsulate(instancePos);
            newBounds.Encapsulate(instancePos + new Vector3(0, data.m_Size.y, 0));

            // Add the world and scale data
            for (int index = 0; index < 4; index++)
            {
                Vector4 posAndRot = instancePos;
                posAndRot.w = rot;

                m_TempWorldPositions.Add(posAndRot);
                m_TempWorldScales.Add(instanceScale);
            }

            // Add stanard quad data            
            m_TempQuadVertices.AddRange(m_SystemQuad.vertices);
            m_TempQuadTangents.AddRange(m_SystemQuad.tangents);
            m_TempQuadNormals.AddRange(m_SystemQuad.normals);

            // Calculate triangle indixes
            m_TempQuadIndices.AddRange(originalTriangles);
            for (int triIndex = 0; triIndex < 6; triIndex++)
            {
                // Just add to the triangles the existing triangles + the new indices
                m_TempQuadIndices[triIndex + 6 * treeIndex] = originalTriangles[triIndex] + 4 * treeIndex;
            }
        }

        treeMesh.Clear();

        // Set standard data
        treeMesh.SetVertices(m_TempQuadVertices);
        treeMesh.SetNormals(m_TempQuadNormals);
        treeMesh.SetTangents(m_TempQuadTangents);        

        // Set the custom data
        treeMesh.SetUVs(1, m_TempWorldPositions);
        treeMesh.SetUVs(2, m_TempWorldScales);

        // Set triangles and do not calculate bounds
        treeMesh.SetTriangles(m_TempQuadIndices, 0, false);

        // Set the manually calculated bounds
        treeMesh.bounds = newBounds;

        treeMesh.UploadMeshData(true);

        // Set the mesh
        filter.mesh = treeMesh;

        // Add the mesh' lod group
        if(m_AddLODGroupToBillboards)
        {
            LODGroup group = mesh.AddComponent<LODGroup>();
            group.animateCrossFading = false;
            group.fadeMode = LODFadeMode.None;

            group.SetLODs(new LOD[] { new LOD(m_BillboardScreenSize, new Renderer[] { rend }) });
            group.RecalculateBounds();
        }
    }

    public void RecursivelyExtract(GameObject extract, List<GameObject> treeChildren)
    {
        // If it is a prefab instance and we have
        if (PrefabUtility.GetPrefabType(extract) == PrefabType.ModelPrefabInstance && ShouldUsePrefabInstance(extract))
        {
            treeChildren.Add(extract);
        }

        // Extract it's owned entities
        for (int i = 0; i < extract.transform.childCount; i++)
            RecursivelyExtract(extract.transform.GetChild(i).gameObject, treeChildren);
    }

    private List<GameObject> ExtractTreesExtra()
    {
        // Iterate the items recursively
        List<GameObject> treePrefabs = new List<GameObject>();

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            foreach (GameObject obj in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                if (obj.GetComponent<ExtraTrees>() != null)
                {
                    Debug.Log("Extracting extra trees from: " + obj.name);
                    RecursivelyExtract(obj, treePrefabs);
                }
            }
        }
        
        return treePrefabs;
    }

    private bool ShouldBuildBillboardBatch(GameObject prefab)
    {
        return System.Array.Find(m_ItemsToExtract, (x) => x.m_ItemPrefab.name == prefab.name && x.m_ExtractBillboard == true) != null;
    }

    private int ShouldUsePrefab(GameObject prefab)
    {
        return System.Array.FindIndex(m_ItemsToExtract, (x) => x.m_ItemPrefab.name == prefab.name);
    }
    
    private GameObject GetPrefabOwner(GameObject obj)
    {
        Debug.Assert(PrefabUtility.GetPrefabType(obj) == PrefabType.ModelPrefabInstance);

        Object owner = PrefabUtility.GetPrefabParent(obj);
        ItemExtract ex = System.Array.Find(m_ItemsToExtract, (x) => x.m_ItemPrefab == owner);

        return ex != null ? ex.m_ItemPrefab : null;
    }

    private bool ShouldUsePrefabInstance(GameObject obj)
    {
        return GetPrefabOwner(obj) != null;
    }    

    private TreeSystemPrototypeData GetPrototypeWithHash(int hash)
    {
        return System.Array.Find(m_ManagedPrototypes, (x) => x.m_TreePrototypeHash == hash);
    }

#endif
}
