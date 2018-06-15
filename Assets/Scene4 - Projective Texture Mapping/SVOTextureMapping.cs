using Academy.HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.WSA.WebCam;

public class SVOTextureMapping : Singleton<SVOTextureMapping> {

    public ComputeShader formatDataShader;
    public ComputeShader flagSVOShader;
    public ComputeShader buildSVOShader;
    public ComputeShader averageSVOShader;
    //public ComputeShader useSVOShader;
    public Material SVOTextureMappingMaterial;
    public GameObject objectToTexture;
    Camera depthCamera;

    // LOD 1 == 9 nodes (1 node containing 8 children + 8 nodes containing 8 children)
    private int maxLOD = 5; //7;
    private Vector3 dimensions = new Vector3(10, 10, 10); // in meters
    // NOTE: Does not currently support voxels with differing lengths.

    unsafe struct SVOPixelData {
        public Vector3 pos;
        //public Vector3 color;
        public fixed uint color[3];
        public int nodeIndex;
    }

    SVOPixelData[] pixelData;

    unsafe struct SVONode {
        public fixed int children[8]; // [0-7] ulb urb dlb drb ulf urf dlf drf
        public Vector3 pos;
        //public Vector3 color;
        public fixed uint color[3];
        public uint pixelCount;
    }

    SVONode[] octree;

    uint nextLODIndex;
    uint lastLODIndex;
    uint lastVoxelIndex;
    Texture2D depthTexture;
    ComputeBuffer pixelData_gpu;
    ComputeBuffer octree_gpu;

    // Use this for initialization
    void Start () {
        depthCamera = ProjectiveTextureMapping.Instance.depthCamera.GetComponent<Camera>();
        depthTexture = new Texture2D(ProjectiveTextureMapping.Instance.textureWidth, ProjectiveTextureMapping.Instance.textureHeight);
        int pixelsPerImage = ProjectiveTextureMapping.Instance.textureWidth * ProjectiveTextureMapping.Instance.textureHeight;
        pixelData = new SVOPixelData[pixelsPerImage * ProjectiveTextureMapping.Instance.maxTextures];
        octree = new SVONode[(int)(Mathf.Pow(8, maxLOD) * 1.15)];
        //pixelData_gpu = new ComputeBuffer(pixelData.Length, 28);
        //octree_gpu = new ComputeBuffer(octree.Length, 60);
        nextLODIndex = 0;
        lastLODIndex = 0;
        lastVoxelIndex = 0;
    }
	
	// Update is called once per frame
	void Update () {
        if (Input.GetKeyDown("c")) {
            CreateSVO();
        }
        if (Input.GetKeyDown("u")) {
            UseSVO();
        }

        // Get depth texture
        RenderTexture.active = ProjectiveTextureMapping.Instance.depthRenderTexture;
        depthTexture.ReadPixels(new Rect(0, 0, ProjectiveTextureMapping.Instance.textureWidth, ProjectiveTextureMapping.Instance.textureHeight), 0, 0);
        depthTexture.Apply();

        // Get inverse view and projection matrices from depth camera
        Matrix4x4 V = depthCamera.worldToCameraMatrix;
        Matrix4x4 P = depthCamera.projectionMatrix;
        //Matrix4x4 InverseVP = (P * V).inverse;

        // Send depth texture and inverse VP to shader
        Renderer renderer = objectToTexture.GetComponent<Renderer>();
        renderer.sharedMaterial.SetTexture("_DepthTex", depthTexture);
        renderer.sharedMaterial.SetMatrix("_InverseVP", ProjectiveTextureMapping.Instance.currentInverseVP);
        renderer.sharedMaterial.SetMatrix("_DVP", P * V);
    }

    public void CreateSVO() {
        // Formatting Data
        FormatPixelData();

        // DEBUGGING
        unsafe {
            fixed (SVOPixelData* pdata = pixelData) {
                print("Pixel data: " + pdata[0].color[0] + ", " + pdata[0].color[1] + ", " + pdata[0].color[2]);
                print("Pixel pos: " + pdata[0].pos);
            }
        }

        for (uint lod = 0; lod < maxLOD; lod++) {
            // Flagging
            FlagSVO();

            // Store the location of the starting voxel for the next LOD
            nextLODIndex = lastVoxelIndex + 1;

            // Building
            if (lod < maxLOD - 1) {
                BuildSVO(lod);
            }

            print("lastLODIndex: " + lastLODIndex);
            print("nextLODIndex: " + nextLODIndex);
            print("lastVoxelIndex: " + lastVoxelIndex);

            // Update the LOD index to the start of the next LOD
            lastLODIndex = nextLODIndex;
        }

        // DEBUGGING
        unsafe {
            fixed (SVONode* oct = octree) {
                print("Before Averaging");
                string childrenString = "";
                for (int i = 0; i < 8; i++) {
                    childrenString += oct[0].children[i] + " ";
                }
                print("Node 0 children: " + childrenString);
                print("Node 0 pixel count: " + oct[0].pixelCount);
                print("Node 0 color: " + oct[0].color[0] + ", " + oct[0].color[1] + ", " + oct[0].color[2]);
            }
        }

        // Averaging
        AverageSVO();
        print("After Averaging");

        // DEBUGGING
        unsafe {
            fixed (SVONode* oct = octree) {
                for (int i = 0; i < 10; i++) {
                    string childrenString = "";
                    for (int j = 0; j < 8; j++) {
                        childrenString += oct[i].children[j] + " ";
                    }
                    print("Node " + i + " (children: " + childrenString + ") (pixels: " + oct[i].pixelCount + ") (color: " + oct[i].color[0] + ", " + oct[i].color[1] + ", " + oct[i].color[2] + ") (pos: " + oct[i].pos + ")");
                    //print("Node 0 pixel count: " + oct[0].pixelCount);
                    //print("Node 0 color: " + oct[0].color[0] + ", " + oct[0].color[1] + ", " + oct[0].color[2]);
                }
                for (int i = 10; i < 80; i += 4) {
                    string childrenString = "";
                    for (int j = 0; j < 8; j++) {
                        childrenString += oct[i].children[j] + " ";
                    }
                    print("Node " + i + " (children: " + childrenString + ") (pixels: " + oct[i].pixelCount + ") (color: " + oct[i].color[0] + ", " + oct[i].color[1] + ", " + oct[i].color[2] + ") (pos: " + oct[i].pos + ")");
                    //print("Node 0 pixel count: " + oct[0].pixelCount);
                    //print("Node 0 color: " + oct[0].color[0] + ", " + oct[0].color[1] + ", " + oct[0].color[2]);
                }
            }
        }

        // Dispose of buffers
        pixelData_gpu.Dispose();
        //octree_gpu.Dispose();

        // Audio feedback that the process has finished
        GameObject.Find("StateChangeAudio").GetComponent<AudioSource>().Play();
        print("Finished creating SVO");
    }

    void FormatPixelData() {
        int kernel = formatDataShader.FindKernel("CSMain");
        Vector3 threadsPerBlock = new Vector3(8, 8, 1);

        // Send data to compute shader
        pixelData_gpu = new ComputeBuffer(pixelData.Length, 28);
        pixelData_gpu.SetData(pixelData);
        formatDataShader.SetBuffer(kernel, "pixelData", pixelData_gpu);

        print("Color of pixel 0: " + ProjectiveTextureMapping.Instance.colorTexArray.GetPixels(0)[0]);
        print("Depth of pixel 0: " + ProjectiveTextureMapping.Instance.depthTexArray.GetPixels(0)[0]);

        formatDataShader.SetTexture(kernel, "_ColorTexArray", ProjectiveTextureMapping.Instance.colorTexArray);
        formatDataShader.SetTexture(kernel, "_DepthTexArray", ProjectiveTextureMapping.Instance.depthTexArray);
        formatDataShader.SetMatrixArray("_VPArray", ProjectiveTextureMapping.Instance.vpArray.ToArray());
        formatDataShader.SetMatrixArray("_DVPArray", ProjectiveTextureMapping.Instance.dvpArray.ToArray());
        formatDataShader.SetMatrixArray("_InverseVPArray", ProjectiveTextureMapping.Instance.invVPArray.ToArray());
        //formatDataShader.SetMatrixArray("_InverseVPArray", ProjectiveTextureMapping.Instance.vpArray.Select(mat => mat.inverse).ToArray());
        formatDataShader.SetMatrixArray("_InverseDVPArray", ProjectiveTextureMapping.Instance.dvpArray.Select(mat => mat.inverse).ToArray());
        //formatDataShader.SetVectorArray("_PosArray", ProjectiveTextureMapping.Instance.posArray.ToArray());

        print("VP Array: (size: " + ProjectiveTextureMapping.Instance.vpArray.Count + ")");
        for (int i = 0; i < ProjectiveTextureMapping.Instance.vpArray.Count; i++) {
            print(ProjectiveTextureMapping.Instance.vpArray[i]);
        }
        print("DVP Array: (size: " + ProjectiveTextureMapping.Instance.dvpArray.Count + ")");
        for (int i = 0; i < ProjectiveTextureMapping.Instance.dvpArray.Count; i++) {
            print(ProjectiveTextureMapping.Instance.dvpArray[i]);
        }
        var invVPArray = ProjectiveTextureMapping.Instance.invVPArray.ToArray();
        print("invVP Array: (size: " + invVPArray.Length + ")");
        for (int i = 0; i < invVPArray.Length; i++) {
            print(invVPArray[i]);
        }
        var invDVPArray = ProjectiveTextureMapping.Instance.dvpArray.Select(mat => mat.inverse).ToArray();
        print("invDVP Array: (size: " + invDVPArray.Length + ")");
        for (int i = 0; i < invDVPArray.Length; i++) {
            print(invDVPArray[i]);
        }

        // Run compute shader
        int numBlocksX = Mathf.CeilToInt(ProjectiveTextureMapping.Instance.textureWidth / threadsPerBlock.x);
        int numBlocksY = Mathf.CeilToInt(ProjectiveTextureMapping.Instance.textureHeight / threadsPerBlock.y);
        int numBlocksZ = Mathf.CeilToInt(ProjectiveTextureMapping.Instance.maxTextures / threadsPerBlock.z);
        formatDataShader.Dispatch(kernel, numBlocksX, numBlocksY, numBlocksZ);

        // Get the data from the GPU
        pixelData_gpu.GetData(pixelData);
        pixelData_gpu.Dispose();
    }

    void FlagSVO() {
        int kernel = flagSVOShader.FindKernel("CSMain");
        Vector3 threadsPerBlock = new Vector3(64, 1, 1);

        // Send data to compute shader
        pixelData_gpu = new ComputeBuffer(pixelData.Length, 28);
        pixelData_gpu.SetData(pixelData);
        flagSVOShader.SetBuffer(kernel, "pixelData", pixelData_gpu);
        octree_gpu = new ComputeBuffer(octree.Length, 60);
        octree_gpu.SetData(octree);
        flagSVOShader.SetBuffer(kernel, "octree", octree_gpu);
        flagSVOShader.SetInt("octreeSize", octree.Length);

        // Run compute shader
        int numBlocksX = Mathf.CeilToInt(pixelData.Length / threadsPerBlock.x);
        flagSVOShader.Dispatch(kernel, numBlocksX, 1, 1);

        // Get the data from the GPU
        pixelData_gpu.GetData(pixelData);
        pixelData_gpu.Dispose();
        octree_gpu.GetData(octree);
        octree_gpu.Dispose();
    }

    void BuildSVO(uint currentLOD) {
        int kernel = buildSVOShader.FindKernel("CSMain");
        Vector3 threadsPerBlock = new Vector3(64, 1, 1);

        // Send data to compute shader
        octree_gpu = new ComputeBuffer(octree.Length, 60);
        octree_gpu.SetData(octree);
        buildSVOShader.SetBuffer(kernel, "octree", octree_gpu);
        buildSVOShader.SetInt("octreeSize", octree.Length);
        //buildSVOShader.SetInt("octreeLastVoxelIndex", lastVoxelIndex);
        //buildSVOShader.SetInt("octreeLastLODIndex", lastLODIndex);
        var octreeMetadata = new uint[3] { lastLODIndex, lastVoxelIndex, currentLOD };
        ComputeBuffer octreeMetadata_gpu = new ComputeBuffer(octreeMetadata.Length, 4);
        octreeMetadata_gpu.SetData(octreeMetadata);
        buildSVOShader.SetBuffer(kernel, "octreeMetadata", octreeMetadata_gpu);
        float currentVoxelSize = dimensions.x / Mathf.Pow(2, currentLOD);
        buildSVOShader.SetFloat("currentVoxelSize", currentVoxelSize);

        // Run compute shader
        int LODSize = ((int)lastVoxelIndex - (int)lastLODIndex + 1) * 8;
        int numBlocksX = Mathf.CeilToInt(LODSize / threadsPerBlock.x);
        buildSVOShader.Dispatch(kernel, numBlocksX, 1, 1);

        // Get the data from the GPU
        octree_gpu.GetData(octree);
        octree_gpu.Dispose();
        octreeMetadata_gpu.GetData(octreeMetadata);
        octreeMetadata_gpu.Dispose();
        //lastLODIndex = octreeMetadata[0];
        lastVoxelIndex = octreeMetadata[1];
        print("Octree METADATA: " + octreeMetadata[0] + ", " + octreeMetadata[1] + ", " + octreeMetadata[2]);
    }

    void AverageSVO() {
        int kernel = averageSVOShader.FindKernel("CSMain");
        Vector3 threadsPerBlock = new Vector3(64, 1, 1);

        // Send data to compute shader
        octree_gpu = new ComputeBuffer(octree.Length, 60);
        octree_gpu.SetData(octree);
        averageSVOShader.SetBuffer(kernel, "octree", octree_gpu);
        averageSVOShader.SetInt("octreeSize", octree.Length);

        // Run compute shader
        int numBlocksX = Mathf.CeilToInt(octree.Length / threadsPerBlock.x);
        averageSVOShader.Dispatch(kernel, numBlocksX, 1, 1);

        // Get the data from the GPU
        octree_gpu.GetData(octree);
        octree_gpu.Dispose();
    }

    public void UseSVO() {
        octree_gpu = new ComputeBuffer(octree.Length, 60);
        octree_gpu.SetData(octree);

        // Send octree to SVO Texture Mapping shader
        SVOTextureMappingMaterial.SetBuffer("octree", octree_gpu);

        // Set object's shader to SVO Texture Mapping
        objectToTexture.GetComponent<Renderer>().material = SVOTextureMappingMaterial;

        // Update material of spatial mapping surfaces
        SpatialMappingManager.Instance.surfaceMaterial = SVOTextureMappingMaterial;
        SpatialMappingManager.Instance.defaultMaterial = SVOTextureMappingMaterial;
        GameObject[] surfaces = GameObject.FindGameObjectsWithTag("SpatialSurface");
        foreach (GameObject surface in surfaces) {
            surface.GetComponent<Renderer>().material = SVOTextureMappingMaterial;
        }

        // Audio feedback that the process has finished
        GameObject.Find("StateChangeAudio").GetComponent<AudioSource>().Play();
        print("Finished using SVO");

        //octree_gpu.Dispose();
    }

    public void RemoveSelection() {
        // If currently gazing at an object, remove that object
        if (GazeManager.Instance.Hit && GazeManager.Instance.HitInfo.collider != null) {
            GameObject selectionSphere = GameObject.Find("SelectionSphere");
            selectionSphere.transform.position = GazeManager.Instance.HitInfo.point;
            selectionSphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            // Remove all vertices within the selection sphere
            GameObject spatialProcessing = GameObject.Find("SpatialProcessing");
            if (selectionSphere != null && spatialProcessing != null) {
                spatialProcessing.SendMessage("RemoveSurfaceVerticesWithinBoundsAndGenerateMesh", new List<GameObject>() { selectionSphere });

                // Stop updating room mesh
                GameObject spatialMapping = GameObject.Find("SpatialMapping");
                spatialMapping.SendMessage("StopObserver");
                
                // Toggle stencil buffer
                //spatialMapping.SendMessage("OnStencilToggle");
                //selectionSphere.GetComponent<Renderer>().material.shader = stencilShader;
                selectionSphere.GetComponent<Renderer>().enabled = false;
                GameObject.Find("StencilSphere").GetComponent<Renderer>().enabled = false;
            } else {
                Debug.Log("Either SelectionSphere or SpatialProcessing objects could not be found.");
            }

            // Play sound upon success
            GameObject.Find("PickupAudio").GetComponent<AudioSource>().Play();
        }
    }
}
