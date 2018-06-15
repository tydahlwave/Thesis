using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour {

    public GameObject listener;

    void OnSelect() {
        listener.SendMessage("TakeSnapshot");
    }
}
