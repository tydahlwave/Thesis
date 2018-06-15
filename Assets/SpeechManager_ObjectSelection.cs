using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class SpeechManager_ObjectSelection : MonoBehaviour {

    public GameObject spatialProcessing;

    KeywordRecognizer keywordRecognizer = null;
    Dictionary<string, System.Action> keywords = new Dictionary<string, System.Action>();

    // Use this for initialization
    void Start() {
        keywords.Add("Remove Mesh", () => {
            //listener.SendMessage("TakeSnapshot");
            GameObject selectionSphere = GameObject.Find("SelectionSphere");
            if (selectionSphere != null && spatialProcessing != null) {
                spatialProcessing.SendMessage("RemoveSurfaceVerticesWithinBounds", new List<GameObject>() { selectionSphere });
            } else {
                Debug.Log("Either SelectionSphere or SpatialProcessing objects could not be found.");
            }
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