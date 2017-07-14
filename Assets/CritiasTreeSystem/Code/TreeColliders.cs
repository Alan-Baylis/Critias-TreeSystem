using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeCollisionCache
{
    private GameObject m_CacheInstanceOwner;
    private GameObject m_CachePrototype;

    private List<GameObject> m_ActiveInstances = new List<GameObject>();
    private List<GameObject> m_InactiveInstances = new List<GameObject>();

    private int m_ExpansionSize;

    public TreeCollisionCache(GameObject collisionPrototype, GameObject instanceOwner = null, int expansionSize = 3)
    {
        m_CacheInstanceOwner = instanceOwner;
        m_CachePrototype = collisionPrototype;
        m_ExpansionSize = expansionSize;
    }

    public GameObject RetrieveInstance()
    {        
        if (m_InactiveInstances.Count == 0)
        {
            for (int i = 0; i < m_ExpansionSize; i++)
            {
                GameObject cached = Object.Instantiate(m_CachePrototype, m_CacheInstanceOwner.transform);
                cached.SetActive(false);

                m_InactiveInstances.Add(cached);
            }
        }

        // Get the instance item
        GameObject inst = m_InactiveInstances[0];
        m_InactiveInstances.RemoveAt(0);

        // Add it to the active list
        m_ActiveInstances.Add(inst);

        // Activate and return it
        inst.SetActive(true);

        return inst;
    }

    public void RecycleInstance(GameObject instance)
    {
        if (m_ActiveInstances.Remove(instance))
        {
            instance.SetActive(false);
            m_InactiveInstances.Add(instance);
        }
    }

    /*
     * Marks all the active instances inactive.
     */
    public void Reset()
    {
        // Add all the instances to the inactive
        for (int i = 0; i < m_ActiveInstances.Count; i++)
        {
            GameObject cached = m_ActiveInstances[i];
            cached.SetActive(false);

            m_InactiveInstances.Add(cached);
        }

        // Clear the active instances
        m_ActiveInstances.Clear();
    }
}

public class TreeColliders : MonoBehaviour
{
    // Wait time for the coroutine execution
    static readonly float WAIT_TIME = 1f;

    [Tooltip("Defaults to 'FindObjectOfType'")]
    public TreeSystem m_OwnerSystem;
    [Tooltip("Defaults to 'Camera.main.transform'")]
    public Transform m_WatchedTransform;

    private Vector3 m_LastPosition;

    private float m_CollisionDistance;
    private float m_CollisionRefreshDistance;

    private GameObject m_ColliderHolder;

    void Start ()
    {
        if (!m_OwnerSystem) m_OwnerSystem = FindObjectOfType<TreeSystem>();
        if (!m_WatchedTransform) m_WatchedTransform = Camera.main.transform;

        m_CollisionDistance = m_OwnerSystem.m_Settings.m_ColliderSetDistance;
        m_CollisionRefreshDistance = m_OwnerSystem.m_Settings.m_ColliderRefreshDistance;

        m_LastPosition = m_WatchedTransform.position;

        m_ColliderHolder = new GameObject("TreeSystemColliderHolder");        
    }
	
    public void UpdateSettings(TreeSystemSettings settings)
    {
        m_CollisionDistance = settings.m_ColliderSetDistance;
        m_CollisionRefreshDistance = settings.m_ColliderRefreshDistance;
    }
    	
    public void StartCollisionUpdates()
    {
        StopCoroutine("CollisionUpdate");
        StartCoroutine("CollisionUpdate");
    }

    public void StopCollisionUpdates()
    {
        StopCoroutine("CollisionUpdate");
    }

    private Vector3 m_CameraPosTemp;

    private int m_DataIssuedActiveColliders;

    IEnumerator CollisionUpdate()
    {
        for (;;)
        {
            if (!m_WatchedTransform)
            {
                yield return new WaitForSeconds(WAIT_TIME);
                continue;
            }

            m_CameraPosTemp = m_WatchedTransform.position;

            float x = m_CameraPosTemp.x - m_LastPosition.x;
            float y = m_CameraPosTemp.y - m_LastPosition.y;
            float z = m_CameraPosTemp.z - m_LastPosition.z;

            float distWalked = x * x + y * y + z * z;

            // If we didn't walked enough, return
            if (distWalked > m_CollisionRefreshDistance * m_CollisionRefreshDistance)
            {
                // Update last position
                m_LastPosition = m_CameraPosTemp;

                // Reset counter
                m_DataIssuedActiveColliders = 0;

                // Reset all the cache's data
                foreach (TreeCollisionCache cache in m_Cache.Values)
                {
                    if(cache != null) cache.Reset();
                }

                // Refresh eveything        
                float collDistSqr = m_CollisionDistance * m_CollisionDistance;

                for (int i = 0; i < m_OwnerSystem.m_ManagedTerrains.Length; i++)
                {
                    TreeSystemTerrain terrain = m_OwnerSystem.m_ManagedTerrains[i];

                    // Get closest point
                    Vector3 pt = terrain.m_ManagedTerrainBounds.ClosestPoint(m_CameraPosTemp);

                    // Check if terrain is within reach range
                    x = pt.x - m_CameraPosTemp.x;
                    y = pt.y - m_CameraPosTemp.y;
                    z = pt.z - m_CameraPosTemp.z;

                    float distToTerrain = x * x + y * y + z * z;

                    // Enable/disable culling group execution based on terrain distance, since we don't want all of them running around
                    if (distToTerrain < collDistSqr)
                        ProcessTerrain(terrain, ref collDistSqr);
                }

                // Update the stats for active/cached colliders
                m_OwnerSystem.m_DataIssuedActiveColliders = m_DataIssuedActiveColliders;
            }

            yield return new WaitForSeconds(WAIT_TIME);
        }
    }

    private void ProcessTerrain(TreeSystemTerrain terrain, ref float colliderDistSqr)
    {
        TreeSystemStructuredTrees[] cells = terrain.m_Cells;        
        float x, y, z;
        
        for (int cellIdx = 0; cellIdx < cells.Length; cellIdx++)
        {
            TreeSystemStructuredTrees cell = cells[cellIdx];
            
            // Get closest point to cell
            Vector3 pt = cell.m_BoundsBox.ClosestPoint(m_CameraPosTemp);

            x = pt.x - m_CameraPosTemp.x;
            y = pt.y - m_CameraPosTemp.y;
            z = pt.z - m_CameraPosTemp.z;

            float distToCell = x * x + y * y + z * z;

            if (distToCell < colliderDistSqr)
                ProcessTerrainCell(cell, ref colliderDistSqr);
        }
    }

    private void ProcessTerrainCell(TreeSystemStructuredTrees cell, ref float colliderDistSqr)
    {                
        TreeSystemStoredInstance[] treeInstances = cell.m_Instances;
        float x, y, z;

        for (int treeIndex = 0; treeIndex < treeInstances.Length; treeIndex++)
        {
            x = treeInstances[treeIndex].m_WorldPosition.x - m_CameraPosTemp.x;
            y = treeInstances[treeIndex].m_WorldPosition.y - m_CameraPosTemp.y;
            z = treeInstances[treeIndex].m_WorldPosition.z - m_CameraPosTemp.z;

            float distToTree = x * x + y * y + z * z;

            if (distToTree <= colliderDistSqr)
            {
                int hash = treeInstances[treeIndex].m_TreeHash;
                
                // Get a collider for the hash
                GameObject collider = GetColliderForPrototype(hash);

                if (collider != null)
                {
                    // Update it's transform values
                    collider.transform.position = treeInstances[treeIndex].m_WorldPosition;
                    collider.transform.rotation = Quaternion.Euler(0, treeInstances[treeIndex].m_WorldRotation * Mathf.Rad2Deg, 0);
                    collider.transform.localScale = treeInstances[treeIndex].m_WorldScale;

                    // Increment the active collider count
                    m_DataIssuedActiveColliders++;
                }
            }
        }        
    }

    private Dictionary<int, TreeCollisionCache> m_Cache = new Dictionary<int, TreeCollisionCache>();

    private GameObject GetColliderForPrototype(int hash)
    {
        TreeSystemPrototypeData data = m_OwnerSystem.m_ManagedPrototypesIndexed[hash];
        
        if(m_Cache.ContainsKey(hash) == false)
        {
            // If we don't contain the key create and add it
            
            // If we don't have a tree with a collider, like a bush or something, just add a null mapping
            if(data.m_TreePrototype.GetComponentInChildren<Collider>() == null)
            {
                m_Cache.Add(hash, null);
                return null;
            }
            else
            {
                // Create the collider prototype and remove all it's mesh renderers and stuff
                GameObject colliderPrototype = Instantiate(data.m_TreePrototype, m_ColliderHolder.transform);
                colliderPrototype.name = "ColliderPrototype_" + data.m_TreePrototype.name;

                // Clear the lod group
                LODGroup lod = colliderPrototype.GetComponent<LODGroup>();
                if (lod) DestroyImmediate(lod);

                // Clear any owned GObjects that don't have colliders
                for(int i = colliderPrototype.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject owned = colliderPrototype.transform.GetChild(i).gameObject;
                    
                    if(owned.GetComponent<Collider>() == null)
                        DestroyImmediate(owned);
                }

                // Deactivate it
                colliderPrototype.SetActive(false);

                // Create the cache entry
                TreeCollisionCache cache = new TreeCollisionCache(colliderPrototype, m_ColliderHolder);

                // Add the collision cache to our dictionary
                m_Cache.Add(hash, cache);
                return cache.RetrieveInstance();
            }
        }
        else if(m_Cache[hash] != null)
        {
            // We contain the cache, just retrieve an object
            return m_Cache[hash].RetrieveInstance();
        }
        else
        {
            // If we contain the hash but it doesn't have anything in it, it means than it's a tree without collisions, like a bush or something
            return null;
        }
    }
}
