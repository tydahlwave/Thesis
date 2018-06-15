using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity;

public class InpaintFrame : MonoBehaviour {

    public Material inpaintFrameMaterial;
    public Material inpaintGenerateMaskMaterial;
    public RenderTexture inpaintMaskRenderTexture;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void GenerateMask(RenderTexture source, Texture2D srcTexture) {
        inpaintGenerateMaskMaterial.SetTexture("_Src", srcTexture);
        Graphics.Blit(source, inpaintMaskRenderTexture, inpaintGenerateMaskMaterial);
    }

    Texture Inpaint(Texture source, Texture mask) {
        var currentTime = Time.realtimeSinceStartup;
        Debug.Log("Current time: " + currentTime);
        Debug.Log(Time.timeSinceLevelLoad);

        //Texture2D srcTexture = Resources.Load("lena") as Texture2D;

        //Mat srcMat = new Mat(srcTexture.height, srcTexture.width, CvType.CV_8UC3);

        //Utils.texture2DToMat(srcTexture, srcMat);
        //Debug.Log("srcMat.ToString() " + srcMat.ToString());

        //Texture2D maskTexture = Resources.Load("lena_inpaint_mask") as Texture2D;

        //Mat maskMat = new Mat(maskTexture.height, maskTexture.width, CvType.CV_8UC1);

        //Utils.texture2DToMat(maskTexture, maskMat);
        //Debug.Log("maskMat.ToString() " + maskMat.ToString());

        Texture2D srcTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Texture2D maskTexture = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);

        Mat srcMat = new Mat(source.height, source.width, CvType.CV_8UC3);
        Utils.textureToTexture2D(source, srcTexture);
        Utils.texture2DToMat(srcTexture, srcMat);

        Mat maskMat = new Mat(mask.height, mask.width, CvType.CV_8UC1);
        Utils.textureToTexture2D(mask, maskTexture);
        Utils.texture2DToMat(maskTexture, maskMat);

        Mat dstMat = new Mat(srcMat.rows(), srcMat.cols(), CvType.CV_8UC3);
        Photo.inpaint(srcMat, maskMat, dstMat, 5, Photo.INPAINT_TELEA);
        //Photo.inpaint(srcMat, maskMat, dstMat, 5, Photo.INPAINT_NS);
        Texture2D inpaintTexture = new Texture2D(dstMat.cols(), dstMat.rows(), TextureFormat.RGBA32, false);
        Utils.matToTexture2D(dstMat, inpaintTexture);

        //gameObject.GetComponent<Renderer>().material.mainTexture = inpaintTexture;

        Debug.Log("Inpaint time: " + (Time.realtimeSinceStartup - currentTime));
        Debug.Log(Time.timeSinceLevelLoad);
        return inpaintTexture;
    }

    Texture2D RenderTextureToTexture2D(RenderTexture renderTexture) {
        Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture.Apply();
        RenderTexture.active = null;
        return texture;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Texture2D srcTexture = RenderTextureToTexture2D(source);
        GenerateMask(source, srcTexture);
        Texture2D maskTexture = RenderTextureToTexture2D(inpaintMaskRenderTexture);

        Texture inpaintTexture = Inpaint(srcTexture, maskTexture);

        inpaintFrameMaterial.SetTexture("_Src", srcTexture);
        inpaintFrameMaterial.SetTexture("_Inpaint", inpaintTexture);
        Graphics.Blit(source, destination, inpaintFrameMaterial);
    }
}
