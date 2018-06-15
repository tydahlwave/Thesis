using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InpaintPlaneScript : MonoBehaviour {

    bool toggle;

	// Use this for initialization
	void Start () {
        toggle = true;
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnSelect() {
        toggle = !toggle;
        if (toggle) {
            Camera.main.SendMessage("EnableDepthIllusion", SendMessageOptions.DontRequireReceiver);
        } else {
            Camera.main.SendMessage("DisableDepthIllusion", SendMessageOptions.DontRequireReceiver);
        }
    }
}
