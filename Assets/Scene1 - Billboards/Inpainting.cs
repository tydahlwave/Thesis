using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class Inpainting : MonoBehaviour {

    //KeywordRecognizer keywordRecognizer = null;
    //Dictionary<string, System.Action> keywords = new Dictionary<string, System.Action>();

    GameObject sphere;
    GameObject inpaintPlane;
    Renderer sphereRenderer;
    Renderer inpaintPlaneRenderer;

    Texture2D renderTextureLeftEye;
    Texture2D renderTextureRightEye;

    Rect objectScreenRect;
    Rect objectViewportRect;
    Rect cameraFrustumRect;

    int frameCount;

    // Distance from the camera that the billboard will be rendered at
    float billboardDistance = 1.0f;

    // Scale plane to 1x1 meter (plane default scale is really large for some reason)
    float planeRealisticScale = 0.1f;

    // Distance between your eyes
    float pupilDistance = 0.04f;

    // Use this for initialization
    void Start() {
        frameCount = 0;

        Debug.Log("Camera Aspect Ratio: " + Camera.main.aspect);

        inpaintPlane = GameObject.Find("InpaintPlane");
        sphere = GameObject.Find("InvisibleSphere");
        sphereRenderer = sphere.GetComponent<Renderer>();
        inpaintPlaneRenderer = inpaintPlane.GetComponent<Renderer>();
        renderTextureLeftEye = new Texture2D(100, 100);
        renderTextureRightEye = new Texture2D(100, 100);

        UpdateBillboard();
    }
    
    void EnableDepthIllusion() {
        Debug.Log("EnableDepthIllusion");
        pupilDistance = 0.04f;
        //UpdateBillboard();
    }

    void DisableDepthIllusion() {
        Debug.Log("DisableDepthIllusion");
        pupilDistance = 0.0f;
        //UpdateBillboard();
    }

    // Average distance between eyes is 63 mm.
    // Billboard distance from eyes is 1 m.
    // Angle from billboard to each eye from the center of the face is 3.612 degrees

    void UpdateBillboard() {
        // Get screen space rect of object
        ScreenSpaceObjectBounds();
        // Resize texture to fit the new bounding box
        renderTextureLeftEye.Resize((int)objectScreenRect.width, (int)objectScreenRect.height);
        renderTextureRightEye.Resize((int)objectScreenRect.width, (int)objectScreenRect.height);
        // Perform ray tracing
        RayTrace();
        
        // Move billboard to camera position
        inpaintPlane.transform.position = Camera.main.transform.position;
        inpaintPlane.transform.rotation = Camera.main.transform.rotation;
        // Rotate billboard to face camera and translate it forward
        inpaintPlane.transform.Translate(new Vector3(0, 0, billboardDistance), Camera.main.transform);
        inpaintPlane.transform.Rotate(new Vector3(90, 180, 0), Space.Self);
        
        // Calculate the camera frustum dimensions in world space
        var frustumHeight = 2.0f * billboardDistance * Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var frustumWidth = frustumHeight * Camera.main.aspect;
        cameraFrustumRect = new Rect(0, 0, frustumWidth, frustumHeight);
        
        // Account for HoloLens frustum only being half the width of the screen
        //var hololensAdjustedFrustumScale = 2.0f;
        //frustumWidth *= 2.0f;
        
        // Calculate the billboard scale and offset from the center of the viewport
        var billboardSize = new Vector3(cameraFrustumRect.width * objectViewportRect.width, 1.0f, 
                                        cameraFrustumRect.height * objectViewportRect.height);
        var billboardOffset = new Vector3((objectViewportRect.center.x - 0.5f) * cameraFrustumRect.width,
                                          (objectViewportRect.center.y - 0.5f) * cameraFrustumRect.height, 0.0f);

        // For some reason the HoloLens frustum is half the width of the screen
        Debug.Log("Screen Size (" + Screen.width + ", " + Screen.height + ")");
        Debug.Log("Frustum Size: " + cameraFrustumRect.size);
        Debug.Log("Camera Aspect Ratio: " + Camera.main.aspect);
        Debug.Log("Camera Position: " + Camera.main.transform.position);
        Debug.Log("Billboard Size: " + billboardSize);
        Debug.Log("Billboard Offset: " + billboardOffset);

        // Apply the billboard scale, offset, and texture
        inpaintPlane.transform.localScale = billboardSize * planeRealisticScale;
        inpaintPlane.transform.Translate(billboardOffset, Camera.main.transform);
        inpaintPlane.transform.Translate(new Vector3(pupilDistance / 2, 0.0f, 0.0f), Camera.main.transform);
        //inpaintPlaneRenderer.material.mainTexture = renderTexture;
	}
	
	// Update is called once per frame
	void Update () {
        frameCount += 1;
        if (frameCount % 30 == 0) {
            UpdateBillboard();
        }
    }

    private void OnPreRender() {
        // Translate billboard for left eye
        inpaintPlane.transform.Translate(new Vector3(-pupilDistance, 0, 0), Camera.main.transform);
        inpaintPlaneRenderer.material.mainTexture = renderTextureLeftEye;
    }

    private void OnPostRender() {
        // Translate billboard for right eye
        inpaintPlane.transform.Translate(new Vector3(pupilDistance, 0, 0), Camera.main.transform);
        inpaintPlaneRenderer.material.mainTexture = renderTextureRightEye;
    }

    private void OnGUI() {
        //GUI.DrawTexture(new Rect(0.1f * Screen.width, 0.1f * Screen.height, textureRect.width, textureRect.height), renderTexture);
        //GUI.DrawTexture(textureRect, renderTexture);
    }

    // Account for eye position when casting rays
    void RayTrace() {
        // Raytrace from left eye
        Camera.main.transform.Translate(new Vector3(pupilDistance / 2, 0, 0), Space.Self);
        for (int y = 0; y < renderTextureLeftEye.height; y++) {
            for (int x = 0; x < renderTextureLeftEye.width; x++) {
                var viewportX = (x / (float)renderTextureLeftEye.width) * objectViewportRect.width + objectViewportRect.xMin;
                var viewportY = (y / (float)renderTextureLeftEye.height) * objectViewportRect.height + objectViewportRect.yMin;
                var color = TraceRay(viewportX, viewportY);
                renderTextureLeftEye.SetPixel(x, y, color);
            }
        }
        renderTextureLeftEye.Apply();

        // Raytrace from right eye
        Camera.main.transform.Translate(new Vector3(-pupilDistance, 0, 0), Space.Self);
        for (int y = 0; y < renderTextureRightEye.height; y++) {
            for (int x = 0; x < renderTextureRightEye.width; x++) {
                var viewportX = (x / (float)renderTextureRightEye.width) * objectViewportRect.width + objectViewportRect.xMin;
                var viewportY = (y / (float)renderTextureRightEye.height) * objectViewportRect.height + objectViewportRect.yMin;
                var color = TraceRay(viewportX, viewportY);
                renderTextureRightEye.SetPixel(x, y, color);
            }
        }
        renderTextureRightEye.Apply();

        // Move camera back to normal location
        Camera.main.transform.Translate(new Vector3(pupilDistance / 2, 0, 0), Space.Self);
    }

    Color TraceRay(float x, float y) {
        Color color = new Color(0, 0, 0, 1);
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(x, y, billboardDistance));
        RaycastHit hit;
        int layermask = 1;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, layermask)) {

            Renderer rend = hit.transform.GetComponent<Renderer>();
            //MeshCollider meshCollider = hit.collider as MeshCollider;

            //if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
            //    return color;

            if (rend.sharedMaterial.mainTexture == null) {
                color = rend.material.color;
                color.a = 0.5f;
            } else {
                Texture2D tex = rend.material.mainTexture as Texture2D;
                Vector2 pixelUV = hit.textureCoord;
                pixelUV.x *= tex.width;
                pixelUV.y *= tex.height;
                color = tex.GetPixel((int)pixelUV.x, (int)pixelUV.y);
            }
        }
        return color;
    }

    void ScreenSpaceObjectBounds() {
        Vector3 center = sphereRenderer.bounds.center;
        Vector3 extents = sphereRenderer.bounds.extents;
        float scale = 1.1f;
        
        // Transform bounding box to screen coordinates
        Vector3[] extentPoints = new Vector3[8] {
            Camera.main.WorldToViewportPoint(center + new Vector3(-extents.x * scale, -extents.y * scale, -extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3( extents.x * scale, -extents.y * scale, -extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3(-extents.x * scale,  extents.y * scale, -extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3( extents.x * scale,  extents.y * scale, -extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3(-extents.x * scale, -extents.y * scale,  extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3( extents.x * scale, -extents.y * scale,  extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3(-extents.x * scale,  extents.y * scale,  extents.z * scale)),
            Camera.main.WorldToViewportPoint(center + new Vector3( extents.x * scale,  extents.y * scale,  extents.z * scale))
        };

        // Find min and max extents of the bounding box
        Vector3 min = extentPoints[0];
        Vector3 max = extentPoints[0];
        foreach (Vector3 v in extentPoints) {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        // Create rect from bounding box extents
        float width = max.x - min.x;
        float height = max.y - min.y;
        objectScreenRect = new Rect(min.x * Screen.width, min.y * Screen.height, width * Screen.width, height * Screen.height);
        objectViewportRect = new Rect(min.x, min.y, width, height);
        Debug.Log("Object Screen Rect: " + objectScreenRect);
        Debug.Log("Object Viewport Rect: " + objectViewportRect);
    }
}
