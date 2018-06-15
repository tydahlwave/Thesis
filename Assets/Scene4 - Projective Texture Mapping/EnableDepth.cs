using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableDepth : MonoBehaviour {

    public Material material;

	// Use this for initialization
	void Start () {
        Camera camera = this.GetComponent<Camera>();
        camera.depthTextureMode = DepthTextureMode.Depth;
        camera.targetTexture.depth = 32;
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, material);
    }
}
