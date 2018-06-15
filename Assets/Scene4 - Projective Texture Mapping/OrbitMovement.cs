using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitMovement : MonoBehaviour {

    [Tooltip("Movement speed.")]
    public float speed = 100.0f;

    [Tooltip("The object to orbit around.")]
    public GameObject anchorObject;
    
	// Update is called once per frame
	void Update () {
        float horizontalValue = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float verticalValue = Input.GetAxis("Vertical") * speed * Time.deltaTime;

        // Rotate around anchor object if it exists, else move up/down and right/left
        if (anchorObject != null) {
            this.transform.RotateAround(anchorObject.transform.position, Vector3.up, -horizontalValue);
            this.transform.RotateAround(anchorObject.transform.position, this.transform.right, verticalValue);
        } else {
            this.transform.Translate(new Vector3(horizontalValue, verticalValue, 0.0f));
        }
    }
}
