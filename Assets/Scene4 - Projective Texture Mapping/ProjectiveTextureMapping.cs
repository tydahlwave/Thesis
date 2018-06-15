using Academy.HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.WSA.WebCam;

public class ProjectiveTextureMapping : Singleton<ProjectiveTextureMapping> {

    public GameObject objectToTexture;
    public GameObject depthCamera;
    public RenderTexture colorRenderTexture;
    public RenderTexture depthRenderTexture;
    Texture2D depthTexture;
    public int textureWidth = 1280;
    public int textureHeight = 720;
    public int maxTextures = 2;
    public bool showBillboards = false;
    public Shader stencilShader;
    public Shader useStencilShader;
    public Material stencilWithColorAndDepth;

    public Matrix4x4 currentInverseVP;

    public Texture2DArray colorTexArray;
    public Texture2DArray depthTexArray;
    public List<Matrix4x4> vpArray;
    public List<Matrix4x4> invVPArray;
    public List<Matrix4x4> dvpArray;
    public List<Vector4> posArray;
    int numProjectors = 0;

    int frameCount;

    PhotoCapture photoCaptureObject = null;

    [Tooltip("The camera component responsible for rendering the scene to a render texture.")]
    new Camera camera;

    //[Tooltip("List of textures and camera transforms.")]
    //List<Snapshot> snapshots;

    //struct Snapshot {
    //    public Texture2D colorTexture;
    //    public Texture2D depthTexture;
    //    public Matrix4x4 vp; // Color camera view and perspective matrices
    //    public Matrix4x4 dvp; // Depth camera view and perspective matrices
    //    public Vector4 position;
    //};

    // Use this for initialization
    void Start() {
        //snapshots = new List<Snapshot>();
        camera = depthCamera.GetComponent<Camera>();
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
        colorTexArray = new Texture2DArray(textureWidth, textureHeight, maxTextures, TextureFormat.RGBA32, false);
        depthTexArray = new Texture2DArray(textureWidth, textureHeight, maxTextures, TextureFormat.RGBA32, false);
        depthTexture = new Texture2D(textureWidth, textureHeight);
        // Initialize arrays to their max size because sending arrays to GPU can't increase in size over time
        vpArray = new List<Matrix4x4>();
        invVPArray = new List<Matrix4x4>();
        dvpArray = new List<Matrix4x4>();
        posArray = new List<Vector4>();
        for (int i = 0; i < maxTextures; i++) vpArray.Add(Matrix4x4.identity);
        for (int i = 0; i < maxTextures; i++) invVPArray.Add(Matrix4x4.identity);
        for (int i = 0; i < maxTextures; i++) dvpArray.Add(Matrix4x4.identity);
        for (int i = 0; i < maxTextures; i++) posArray.Add(Vector4.zero);
        currentInverseVP = Matrix4x4.identity;
    }

    private void OnDestroy() {
        // Clean up
        if (photoCaptureObject != null) {
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
    }

    // Update is called once per frame
    void Update() {
        frameCount++;
        if (frameCount % 5 == 0) {
            UpdateCameraMatrices();
        }

        if (Input.GetKeyDown("space")) {
            TakeSnapshot();
        }
        if (Input.GetKeyDown("mouse 0")) {
            Debug.Log("Left mouse clicked");
            GameObject selectionSphere = GameObject.Find("SelectionSphere");
            selectionSphere.transform.localScale = new Vector3(1, 1, 1);
            selectionSphere.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2;
            RemoveSelection();
        }

        Renderer renderer = objectToTexture.GetComponent<Renderer>();
        renderer.sharedMaterial.SetVector("_MainCamPos", Camera.main.transform.position);
        renderer.sharedMaterial.SetInt("_Count", numProjectors);
        renderer.sharedMaterial.SetMatrixArray("_VPArray", vpArray);
        renderer.sharedMaterial.SetMatrixArray("_DVPArray", dvpArray);
        renderer.sharedMaterial.SetVectorArray("_PosArray", posArray);
    }

    void UpdateCameraMatrices() {
        if (photoCaptureObject != null)
        {
            photoCaptureObject.TakePhotoAsync(UpdateCameraMatricesWithCapturedPhoto);
        }
        else
        {
            Matrix4x4 V = camera.worldToCameraMatrix;
            Matrix4x4 P = camera.projectionMatrix;
            currentInverseVP = (P * V).inverse;
        }
    }

    void UpdateCameraMatricesWithCapturedPhoto(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame) {
        if (result.success) {
            // Get view and projection matrices from camera
            Matrix4x4 inverseV;
            Matrix4x4 P;
            Matrix4x4 VP;
            bool vSuccess = photoCaptureFrame.TryGetCameraToWorldMatrix(out inverseV);
            bool pSuccess = photoCaptureFrame.TryGetProjectionMatrix(out P);
            if (vSuccess && pSuccess) {
                currentInverseVP = P * inverseV;
            }
        }
    }

    void TakeScreenshot(Texture2D cameraImage) {
        // Store the current capture of the second camera's RenderTexture into a Texture2D image
        Texture2D colorTexture = cameraImage;
        if (cameraImage == null) {
            colorTexture = new Texture2D(textureWidth, textureHeight);
            RenderTexture.active = colorRenderTexture;
            colorTexture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            colorTexture.Apply();
        }
        RenderTexture.active = depthRenderTexture;
        depthTexture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
        depthTexture.Apply();
        //		Texture2D tex2 = ScreenCapture.CaptureScreenshotAsTexture();

        //Matrix4x4 M = objectToTexture.transform.localToWorldMatrix;
        Matrix4x4 V = camera.worldToCameraMatrix;
        Matrix4x4 P = camera.projectionMatrix;
        Vector4 position = new Vector4(depthCamera.transform.position.x, depthCamera.transform.position.y, depthCamera.transform.position.z, 1);

        //Snapshot snapshot = new Snapshot();
        //snapshot.colorTexture = colorTexture;
        //snapshot.depthTexture = depthTexture;
        ////snapshot.mvp = P * V * M;
        //snapshot.vp = P * V;
        //snapshot.dvp = P * V;
        //snapshot.position = position;
        //snapshots.Add(snapshot);
        Graphics.CopyTexture(colorTexture, 0, 0, colorTexArray, numProjectors, 0);
        Graphics.CopyTexture(depthTexture, 0, 0, depthTexArray, numProjectors, 0);
        vpArray[numProjectors] = P * V;
        invVPArray[numProjectors] = (P * V).inverse;
        dvpArray[numProjectors] = P * V;
        posArray[numProjectors] = position;

        if (showBillboards) {
            // Create billboard
            GameObject billboard = GameObject.CreatePrimitive(PrimitiveType.Plane);
            billboard.name = "Billboard" + numProjectors;
            billboard.transform.position = Camera.main.transform.position;
            billboard.transform.rotation = Camera.main.transform.rotation;
            billboard.transform.Rotate(new Vector3(-90.0f, 0.0f, 0.0f), Space.Self);
            Renderer renderer = billboard.GetComponent<Renderer>();

            // Apply the texture to the billboard
            renderer.material.mainTexture = colorTexture;
            renderer.material.shader = Shader.Find("Custom/CullOff");

            // Resize the billboard to fit the dimensions of the viewport
            billboard.transform.localScale = new Vector3(0.05f * Camera.main.aspect, 0.05f, 0.05f);

            // Create billboard
            GameObject billboard2 = GameObject.CreatePrimitive(PrimitiveType.Plane);
            billboard2.name = "Billboard" + numProjectors + "_2";
            billboard2.transform.position = Camera.main.transform.position;
            billboard2.transform.rotation = Camera.main.transform.rotation;
            billboard2.transform.Rotate(new Vector3(-90.0f, 0.0f, 0.0f), Space.Self);
            billboard2.transform.Translate(new Vector3(0, 0, 0.5f), Space.Self);
            Renderer renderer2 = billboard2.GetComponent<Renderer>();

            // Apply the texture to the billboard
            renderer2.material.mainTexture = depthTexture;
            renderer2.material.shader = Shader.Find("Custom/CullOff");

            // Resize the billboard to fit the dimensions of the viewport
            billboard2.transform.localScale = new Vector3(0.05f * Camera.main.aspect, 0.05f, 0.05f);
        }
    }

    void UpdateShader() {
        Renderer renderer = objectToTexture.GetComponent<Renderer>();
        renderer.sharedMaterial.SetMatrixArray("_VPArray", vpArray);
        renderer.sharedMaterial.SetMatrixArray("_DVPArray", dvpArray);
        renderer.sharedMaterial.SetVectorArray("_PosArray", posArray);
        renderer.sharedMaterial.SetTexture("_ColorTexArray", colorTexArray);
        renderer.sharedMaterial.SetTexture("_DepthTexArray", depthTexArray);

        numProjectors += 1;
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject) {
        photoCaptureObject = captureObject;

        //Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        foreach (Resolution resolution in PhotoCapture.SupportedResolutions) {
            Debug.Log("Resolution: (" + resolution.width + ", " + resolution.height + ") refresh=" + resolution.refreshRate);
        }

        CameraParameters c = new CameraParameters();
        c.hologramOpacity = 0.0f;
        c.cameraResolutionWidth = textureWidth;// cameraResolution.width;
        c.cameraResolutionHeight = textureHeight;// cameraResolution.height;
        c.pixelFormat = CapturePixelFormat.BGRA32;

        captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result) {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result) {
        if (result.success) {
            //photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        } else {
            Debug.LogError("Unable to start photo mode!");
        }
    }

    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame) {
        if (result.success) {
            // Create our Texture2D for use and set the correct resolution
            //Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
            Texture2D targetTexture = new Texture2D(textureWidth, textureHeight);//cameraResolution.width, cameraResolution.height);
            // Copy the raw image data into our target texture
            photoCaptureFrame.UploadImageDataToTexture(targetTexture);

            // Get view and projection matrices from camera, as well as position and lookVector
            Matrix4x4 inverseV;
            Matrix4x4 P;
            Matrix4x4 VP;
            bool vSuccess = photoCaptureFrame.TryGetCameraToWorldMatrix(out inverseV);
            bool pSuccess = photoCaptureFrame.TryGetProjectionMatrix(out P);
            Vector3 cameraWorldPos = inverseV.MultiplyPoint(Vector3.zero);
            Vector3 cameraLookVector = inverseV.MultiplyVector(Vector3.forward);
            Debug.Log("RGB Camera View Matrix: " + (vSuccess ? "Found" : "NULL"));
            Debug.Log("RGB Camera Projection Matrix: " + (pSuccess ? "Found" : "NULL"));
            Debug.Log("RGB Camera Position: " + cameraWorldPos);
            Debug.Log("RGB Camera LookVector: " + cameraLookVector);

            //depthCamera.transform.position = (cameraWorldPos - Camera.main.gameObject.transform.position);
            //depthCamera.transform.LookAt(cameraWorldPos + cameraLookVector);
            //Debug.Log("Applied position and lookvector to depth camera");
            //camera.projectionMatrix = P;
            //Debug.Log("Applied projection matrix to depth camera");
            ////camera.worldToCameraMatrix = inverseV.inverse;
            //camera.Render();
            ////camera.RenderWithShader(Shader.Find("DepthOnly"), "depth");
            //Debug.Log("Rendered without depth shader");

            TakeScreenshot(targetTexture);
            Debug.Log("Snapshot Taken");
            // Update snapshot VP matrix before updating the shader
            if (vSuccess && pSuccess) {
                VP = P * inverseV.inverse;
                //Snapshot snapshot = snapshots[snapshots.Count - 1];
                //snapshot.vp = VP;
                //snapshot.position = new Vector4(cameraWorldPos.x, cameraWorldPos.y, cameraWorldPos.z, 1);
                //snapshots[snapshots.Count - 1] = snapshot;
                vpArray[numProjectors] = VP;
                invVPArray[numProjectors] = (inverseV);
                posArray[numProjectors] = new Vector4(cameraWorldPos.x, cameraWorldPos.y, cameraWorldPos.z, 1);
            }
            Debug.Log("Snapshot Updated");
            UpdateShader();
            Debug.Log("Updated Shader");

            // Free memory
            photoCaptureFrame.Dispose();
        }
        // Clean up
        //photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    void TakeSnapshot() {
        //camera.RenderWithShader(Shader.Find("DepthOnly"), "depth");
        if (photoCaptureObject != null) {
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        } else {
            TakeScreenshot(null);
            UpdateShader();
        }
    }

    void RemoveSelection() {
        GameObject spatialProcessing = GameObject.Find("SpatialProcessing");
        GameObject selectionSphere = GameObject.Find("SelectionSphere");
        if (selectionSphere != null && spatialProcessing != null) {
            spatialProcessing.SendMessage("RemoveSurfaceVerticesWithinBoundsAndGenerateMesh", new List<GameObject>() { selectionSphere });

            // Stop updating room mesh
            GameObject spatialMapping = GameObject.Find("SpatialMapping");
            spatialMapping.SendMessage("StopObserver");

            EnableStencil();
        } else {
            Debug.Log("Either SelectionSphere or SpatialProcessing objects could not be found.");
        }
    }

    private void EnableStencil() {
        //GameObject spatialMapping = GameObject.Find("SpatialMapping");
        GameObject selectionSphere = GameObject.Find("SelectionSphere");

        // Toggle stencil buffer
        //spatialMapping.SendMessage("OnStencilToggle");
        //selectionSphere.GetComponent<Renderer>().material.shader = stencilShader;
        selectionSphere.GetComponent<Renderer>().enabled = false;

        GameObject.Find("StencilSphere").GetComponent<Renderer>().enabled = false;
    }

    public void UndoPicture() {
        numProjectors -= 1;
    }
}
