using Academy.HoloToolkit.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class SpeechManager : MonoBehaviour {

    public GameObject listener;
    public Material projectiveTextureMappingMaterial;
    public Shader stencilShader;
    int shaderType = 0;

    KeywordRecognizer keywordRecognizer = null;
    Dictionary<string, System.Action> keywords = new Dictionary<string, System.Action>();

    // Use this for initialization
    void Start() {
        keywords.Add("Take Picture", () => {
            listener.SendMessage("TakeSnapshot");
        });
        keywords.Add("End Picture Mode", () => {
            GameObject.Find("StateChangeAudio").GetComponent<AudioSource>().Play();
            MySceneManager.Instance.AdvanceState();
        });
        keywords.Add("Finalize Scan", () => {
            SpatialMappingManager.Instance.StopObserver();
        });
        keywords.Add("Continue Scan", () => {
            SpatialMappingManager.Instance.StartObserver();
        });
        projectiveTextureMappingMaterial.SetInt("_ShaderType", shaderType);
        keywords.Add("Switch Shader", () => {
            shaderType = (shaderType + 1) % 4;
            projectiveTextureMappingMaterial.SetInt("_ShaderType", shaderType);
        });
        keywords.Add("Remove Mesh", () => {
            //Camera.main.gameObject.SendMessage("RemoveSelection");
            SVOTextureMapping.Instance.RemoveSelection();
        });
        keywords.Add("Create Octree", () => {
            SVOTextureMapping.Instance.CreateSVO();
        });
        keywords.Add("Use Octree", () => {
            SVOTextureMapping.Instance.UseSVO();
            //SVOTextureMapping.Instance.RemoveSelection();
        });
        keywords.Add("Undo Picture", () => {
            ProjectiveTextureMapping.Instance.UndoPicture();
        });

        // Tell the KeywordRecognizer about our keywords.
        keywordRecognizer = new KeywordRecognizer(keywords.Keys.ToArray());

        // Register a callback for the KeywordRecognizer and start recognizing!
        keywordRecognizer.OnPhraseRecognized += KeywordRecognizer_OnPhraseRecognized;
        keywordRecognizer.Start();
    }

    private void KeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args) {
        System.Action keywordAction;
        if (keywords.TryGetValue(args.text, out keywordAction)) {
            keywordAction.Invoke();
        }
    }
}