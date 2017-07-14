using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// You can add manually placed trees to the scene, and if in the full hierarchy of these objects
// any trees matching the used prefabs are found, then those are also added to the system
// if the are withing a terrain's bounds and within the cell subdivisions of a terrain. Very usefull
// for manually placed trees and for vines on houses etc...

// This is just a simple placeholder if we want to 'tell' the system to extract all the trees held by this object
[ExecuteInEditMode]
public class ExtraTrees : MonoBehaviour
{
#if UNITY_EDITOR
    public void Start()
    {
        if (transform.parent != null)
        {
            Debug.LogError("Extra tree holder must be 'root' in the scene!");
            DestroyImmediate(this);
        }
    }
#endif
}
