// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

namespace Academy.HoloToolkit.Unity
{
    /// <summary>
    /// GestureManager creates a gesture recognizer and signs up for a tap gesture.
    /// When a tap gesture is detected, GestureManager uses GazeManager to find the game object.
    /// GestureManager then sends a message to that game object.
    /// </summary>
    [RequireComponent(typeof(GazeManager))]
    public partial class GestureManager : Singleton<GestureManager>
    {
        /// <summary>
        /// To select even when a hologram is not being gazed at,
        /// set the override focused object.
        /// If its null, then the gazed at object will be selected.
        /// </summary>
        public GameObject OverrideFocusedObject
        {
            get; set;
        }

        /// <summary>
        /// Gets the currently focused object, or null if none.
        /// </summary>
        public GameObject FocusedObject
        {
            get { return focusedObject; }
        }

        private GestureRecognizer gestureRecognizer;
        private GameObject focusedObject;

        private GameObject selectionSphere;
        private Renderer selectionSphereRenderer;
        private float selectionSphereCurrentScale;
        public Material selectionSphereMaterial;

        void Start()
        {
            // Create selection sphere
            selectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            selectionSphere.name = "SelectionSphere";
            selectionSphereRenderer = selectionSphere.GetComponent<Renderer>();
            selectionSphereRenderer.material = selectionSphereMaterial;
            selectionSphereRenderer.material.color = new Color(0.5f, 1, 1, 0.3f);
            //selectionSphereRenderer.isVisible = false;
            selectionSphere.layer = LayerMask.NameToLayer("Ignore Raycast");
            selectionSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            selectionSphere.transform.position = new Vector3(0.3f, 0, 1.1f);

            // Create a new GestureRecognizer. Sign up for tapped events.
            gestureRecognizer = new GestureRecognizer();
            gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap | GestureSettings.DoubleTap | GestureSettings.Hold | GestureSettings.ManipulationTranslate);

            gestureRecognizer.Tapped += GestureRecognizer_Tapped;
            gestureRecognizer.HoldStarted += GestureRecognizer_Hold_Started;
            gestureRecognizer.HoldCompleted += GestureRecognizer_Hold_Completed;
            gestureRecognizer.HoldCanceled += GestureRecognizer_Hold_Canceled;
            gestureRecognizer.ManipulationStarted += GestureRecognizer_Manipulation_Started;
            gestureRecognizer.ManipulationUpdated += GestureRecognizer_Manipulation_Updated;
            gestureRecognizer.ManipulationCompleted += GestureRecognizer_Manipulation_Completed;
            gestureRecognizer.ManipulationCanceled += GestureRecognizer_Manipulation_Canceled;

            // Start looking for gestures.
            gestureRecognizer.StartCapturingGestures();
        }

        private void GestureRecognizer_Tapped(TappedEventArgs args)
        {
            Debug.Log("Tapped: " + args.tapCount);
            if (focusedObject != null) {
                focusedObject.SendMessage("OnSelect");
            }

            //if (args.tapCount == 2) {
            //    Camera.main.gameObject.SendMessage("RemoveSelection");
            //}

            // Let SceneManager decide what to do when tap occurs
            MySceneManager.Instance.SendMessage("HandleTap", args);
        }

        private void GestureRecognizer_Hold_Started(HoldStartedEventArgs args)
        {
            Debug.Log("Hold Started");
            //if (GazeManager.Instance.Hit && OverrideFocusedObject == null && GazeManager.Instance.HitInfo.collider != null) {
            //    selectionSphere.transform.position = GazeManager.Instance.HitInfo.point;
            //    selectionSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            //    selectionSphereRenderer.enabled = true;
            //}
        }

        private void GestureRecognizer_Hold_Completed(HoldCompletedEventArgs args)
        {
            Debug.Log("Hold Completed");
        }

        private void GestureRecognizer_Hold_Canceled(HoldCanceledEventArgs args)
        {
            Debug.Log("Hold Canceled");
        }

        private void GestureRecognizer_Manipulation_Started(ManipulationStartedEventArgs args)
        {
            Debug.Log("Manipulation Started");
            //selectionSphereCurrentScale = selectionSphere.transform.localScale.x;
        }

        private void GestureRecognizer_Manipulation_Updated(ManipulationUpdatedEventArgs args)
        {
            Debug.Log("Manipulation Updated: " + args.cumulativeDelta);
            //ManipulateSelectionSphere(args.cumulativeDelta);
        }

        private void GestureRecognizer_Manipulation_Completed(ManipulationCompletedEventArgs args)
        {
            Debug.Log("Manipulation Completed: " + args.cumulativeDelta);
            //ManipulateSelectionSphere(args.cumulativeDelta);
        }

        private void GestureRecognizer_Manipulation_Canceled(ManipulationCanceledEventArgs args)
        {
            Debug.Log("Manipulation Canceled");
        }

        private void ManipulateSelectionSphere(Vector3 cumulativeDelta) {
            // Find delta relative to the main camera instead of in world space
            Vector3 viewportDelta = Camera.main.worldToCameraMatrix.MultiplyPoint(Camera.main.transform.position + cumulativeDelta);
            Debug.Log("Viewport Delta: " + viewportDelta);
            float maxDelta = Mathf.Max(viewportDelta.x, viewportDelta.y, viewportDelta.z);
            float minDelta = Mathf.Min(viewportDelta.x, viewportDelta.y, viewportDelta.z);
            float delta = (Mathf.Abs(maxDelta) > Mathf.Abs(minDelta)) ? maxDelta : minDelta;
            Vector3 currentScale = Vector3.one * selectionSphereCurrentScale;
            Vector3 deltaScale = Vector3.one * delta * 4;
            Vector3 finalScale = currentScale + deltaScale;
            if (finalScale.x < 0) finalScale = Vector3.zero;
            selectionSphere.transform.localScale = finalScale;
        }

        void LateUpdate()
        {
            GameObject oldFocusedObject = focusedObject;

            if (GazeManager.Instance.Hit &&
                OverrideFocusedObject == null &&
                GazeManager.Instance.HitInfo.collider != null)
            {
                // If gaze hits a hologram, set the focused object to that game object.
                // Also if the caller has not decided to override the focused object.
                focusedObject = GazeManager.Instance.HitInfo.collider.gameObject;
            }
            else
            {
                // If our gaze doesn't hit a hologram, set the focused object to null or override focused object.
                focusedObject = OverrideFocusedObject;
            }

            if (focusedObject != oldFocusedObject)
            {
                // If the currently focused object doesn't match the old focused object, cancel the current gesture.
                // Start looking for new gestures.  This is to prevent applying gestures from one hologram to another.
                gestureRecognizer.CancelGestures();
                gestureRecognizer.StartCapturingGestures();
            }
        }

        void OnDestroy()
        {
            gestureRecognizer.StopCapturingGestures();
            gestureRecognizer.Tapped -= GestureRecognizer_Tapped;
        }
    }
}