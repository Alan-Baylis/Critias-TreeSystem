using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugUtils : MonoBehaviour
{
    public TreeSystem m_TreeSystem;

    public UnityEngine.UI.Text m_Info;
    
    public float m_Distance = 300;

    public bool m_UseSpeedTree = false;
    public GameObject[] m_TreeRelated;

    void Start()
    {
        m_TreeSystem.SetTreeDistance(m_Distance);
    }

	void OnGUI()
    {
        float dist = GUI.HorizontalSlider(new Rect(20, 20, 100, 20), m_Distance, 0f, 2000f);
        GUI.Label(new Rect(20, 40, 200, 20), "Distance: " + m_Distance);

        if(dist != m_Distance)
        {
            m_Distance = Mathf.Floor(dist);
            m_TreeSystem.SetTreeDistance(m_Distance);
        }
        
        bool useSpeed = GUI.Toggle(new Rect(20, 80, 400, 20), m_UseSpeedTree, " Use SpeedTree (WARNING, might kill your PC!!)");

        if(useSpeed != m_UseSpeedTree)
        {
            m_UseSpeedTree = useSpeed;

            if(m_UseSpeedTree)
            {
                foreach (GameObject syst in m_TreeRelated)
                    syst.SetActive(false);
                foreach (TreeSystemTerrain t in m_TreeSystem.m_ManagedTerrains)
                    t.m_ManagedTerrain.drawTreesAndFoliage = true;
            }
            else
            {
                foreach (GameObject syst in m_TreeRelated)
                    syst.SetActive(true);
                foreach (TreeSystemTerrain t in m_TreeSystem.m_ManagedTerrains)
                    t.m_ManagedTerrain.drawTreesAndFoliage = false;
            }
        }

        if(m_Info)
            m_Info.text = m_TreeSystem.GetDrawInfo();
    }
}
