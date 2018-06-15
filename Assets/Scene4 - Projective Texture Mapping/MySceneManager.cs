using Academy.HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class MySceneManager : Singleton<MySceneManager>
{
    public enum State
    {
        takingPictures,
        choosingObject,
        viewingStencil
    };

    public State state = State.takingPictures;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void HandleTap(TappedEventArgs args) {
        GameObject.Find("PickupAudio").GetComponent<AudioSource>().Play();
        switch (state) {
            case State.takingPictures:
                // Take a picture from the current location
                ProjectiveTextureMapping.Instance.SendMessage("TakeSnapshot");
                break;
            case State.choosingObject:
                // If currently gazing at an object, remove that object
                if (GazeManager.Instance.Hit && GazeManager.Instance.HitInfo.collider != null) {
                    GameObject selectionSphere = GameObject.Find("SelectionSphere");
                    selectionSphere.transform.position = GazeManager.Instance.HitInfo.point;
                    selectionSphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                    // Remove all vertices within the selection sphere
                    ProjectiveTextureMapping.Instance.SendMessage("RemoveSelection");

                    // Auto-advance to the next state
                    AdvanceState();
                }
                break;
            case State.viewingStencil:
                // Toggle stencil shader
                ToggleStencil();
                break;
        }
    }

    public void AdvanceState() {
        switch (state) {
            case State.takingPictures:
                state = State.choosingObject;
                break;
            case State.choosingObject:
                state = State.viewingStencil;
                break;
            case State.viewingStencil:
                break;
        }
    }

    void ToggleStencil() {
        GameObject removedObject = GameObject.Find("RemovedObject");
        GameObject removedObject2 = GameObject.Find("RemovedObject2");
        GameObject stencilSphere = GameObject.Find("StencilSphere");

        if (removedObject == null || removedObject2 == null || stencilSphere == null) return;

        removedObject.GetComponent<Renderer>().enabled = !removedObject.GetComponent<Renderer>().enabled;
        removedObject2.GetComponent<Renderer>().enabled = !removedObject2.GetComponent<Renderer>().enabled;
        stencilSphere.GetComponent<Renderer>().enabled = !stencilSphere.GetComponent<Renderer>().enabled;
    }
}
