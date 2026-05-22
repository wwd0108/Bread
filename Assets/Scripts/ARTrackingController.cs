using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if AR_FOUNDATION_PRESENT
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace BreadAR
{
    public class ARTrackingController : MonoBehaviour
    {
        // -------------------------------------------------------------
        // 1. WebGL JavaScript Native Bridge Functions
        // -------------------------------------------------------------
        #if !UNITY_EDITOR && UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void TriggerOnBreadDetected(string breadId);

        [DllImport("__Internal")]
        private static extern void TriggerOnTrackingFailed();
        #else
        // Fallbacks for Editor or Non-WebGL builds
        private static void TriggerOnBreadDetected(string breadId)
        {
            Debug.Log($"[Mock Bridge] OnBreadDetected called for: {breadId}");
        }

        private static void TriggerOnTrackingFailed()
        {
            Debug.Log("[Mock Bridge] OnTrackingFailed called (3 failures simulated)");
        }
        #endif

        [Header("AR Configuration")]
        #if AR_FOUNDATION_PRESENT
        [SerializeField] private ARTrackedImageManager trackedImageManager;
        #endif
        
        [Header("Tracking Parameters")]
        [Tooltip("Seconds of no detection before incrementing fail count")]
        [SerializeField] private float trackingTimeoutSeconds = 8.0f;
        [SerializeField] private int maxAllowedFailures = 3;

        private int currentFailureCount = 0;
        private float timeoutTimer = 0f;
        private bool isTrackingActive = true;
        private HashSet<string> detectedBreads = new HashSet<string>();

        private void Awake()
        {
            // Auto-locate ARTrackedImageManager if present and not set
            #if AR_FOUNDATION_PRESENT
            if (trackedImageManager == null)
            {
                trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>();
            }
            #endif
        }

        private void OnEnable()
        {
            #if AR_FOUNDATION_PRESENT
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
            }
            #endif
        }

        private void OnDisable()
        {
            #if AR_FOUNDATION_PRESENT
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            }
            #endif
        }

        private void Update()
        {
            if (!isTrackingActive) return;

            // -------------------------------------------------------------
            // 2. Editor-Only Simulation Hotkeys (Developer Convenience)
            // -------------------------------------------------------------
            #if UNITY_EDITOR || true // Keep simulation active in WebGL for testing if camera permission is missing
            HandleEditorSimulationInputs();
            #endif

            // Tracking Timeout Calculation
            if (detectedBreads.Count == 0)
            {
                timeoutTimer += Time.deltaTime;
                if (timeoutTimer >= trackingTimeoutSeconds)
                {
                    HandleTrackingFailure();
                }
            }
        }

        #if AR_FOUNDATION_PRESENT
        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
        {
            foreach (var trackedImage in eventArgs.added)
            {
                UpdateTrackedImage(trackedImage);
            }
            foreach (var trackedImage in eventArgs.updated)
            {
                UpdateTrackedImage(trackedImage);
            }
        }

        private void UpdateTrackedImage(ARTrackedImage trackedImage)
        {
            // Verify tracking state is solid
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                string imageName = trackedImage.referenceImage.name.ToLower();
                
                // Map tracked image names to specific Bread IDs
                string breadId = "";
                if (imageName.Contains("salt") || imageName.Contains("salt_bread"))
                {
                    breadId = "salt_bread";
                }
                else if (imageName.Contains("melon") || imageName.Contains("melon_bread"))
                {
                    breadId = "melon_bread";
                }
                else if (imageName.Contains("croissant"))
                {
                    breadId = "croissant";
                }

                if (!string.IsNullOrEmpty(breadId))
                {
                    OnBreadRecognized(breadId);
                }
            }
        }
        #endif

        private void OnBreadRecognized(string breadId)
        {
            timeoutTimer = 0f;
            currentFailureCount = 0;
            detectedBreads.Add(breadId);
            
            Debug.Log($"[AR Controller] Bread Recognized: {breadId}");
            
            // Send trigger to browser frontend
            TriggerOnBreadDetected(breadId);

            // Play local Unity 3D particle or highlight effect
            PlayRecognitionEffect();
        }

        private void HandleTrackingFailure()
        {
            timeoutTimer = 0f;
            currentFailureCount++;
            Debug.LogWarning($"[AR Controller] Tracking failed. Count: {currentFailureCount}/{maxAllowedFailures}");

            if (currentFailureCount >= maxAllowedFailures)
            {
                isTrackingActive = false;
                Debug.LogError("[AR Controller] Max failures reached. Sending failure alert to web UI...");
                TriggerOnTrackingFailed();
            }
        }

        private void PlayRecognitionEffect()
        {
            // Add custom 3D recognition sparkles, pop effects, or sound effects here.
        }

        // -------------------------------------------------------------
        // 3. Web <=> Unity Inbound Message Handlers (Call from JS)
        // -------------------------------------------------------------

        // JS Command: window.unityInstance.SendMessage("ARController", "TriggerSliceAnimation");
        public void TriggerSliceAnimation()
        {
            Debug.Log("[AR Controller] Web UI triggered 3D Slicing Animation!");
            
            // Trigger your 3D Bread Object's animator or shader slicing effect here
            // Example:
            // Animator anim = GetComponentInChildren<Animator>();
            // if(anim != null) anim.SetTrigger("Slice");
        }

        // JS Command: window.unityInstance.SendMessage("ARController", "ResetTrackingFailures");
        public void ResetTrackingFailures()
        {
            Debug.Log("[AR Controller] Web UI requested tracking reset. Restarting tracking...");
            currentFailureCount = 0;
            timeoutTimer = 0f;
            detectedBreads.Clear();
            isTrackingActive = true;
        }

        // -------------------------------------------------------------
        // 4. Keyboard Testing System (WebGL Testing Simulator)
        // -------------------------------------------------------------
        private void HandleEditorSimulationInputs()
        {
            // Press '1' to simulate Salt Bread scanning
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                OnBreadRecognized("salt_bread");
            }
            // Press '2' to simulate Melon Bread scanning
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                OnBreadRecognized("melon_bread");
            }
            // Press '3' to simulate Croissant scanning
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                OnBreadRecognized("croissant");
            }
            // Press 'F' to force immediate tracking failure modal
            if (Input.GetKeyDown(KeyCode.F))
            {
                currentFailureCount = maxAllowedFailures - 1;
                HandleTrackingFailure();
            }
        }
    }
}
