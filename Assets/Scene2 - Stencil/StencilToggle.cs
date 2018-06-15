using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StencilToggle : MonoBehaviour {

    public Shader stencilShader;
    public Shader normalShader;
    bool isStencilEnabled = false;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnSelect() {
        isStencilEnabled = !isStencilEnabled;
        if (isStencilEnabled) {
            GetComponent<Renderer>().material.shader = stencilShader;
        } else {
            GetComponent<Renderer>().material.shader = normalShader;
        }
        GameObject.Find("SpatialMapping").SendMessage("OnStencilToggle");
    }
}
