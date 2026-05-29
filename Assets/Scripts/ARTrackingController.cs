using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace BreadAR
{
    public enum ScanningMode
    {
        OrganicBread, // 1단계: 실물 빵 자체 스캔 모드
        Nametag       // 2단계: 3회 실패 후 글자/네임텍 카드 스캔 모드
    }

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
            Debug.Log("[Mock Bridge] OnTrackingFailed called (Transitioned to Nametag Scan Mode)");
        }
        #endif

        [Header("AR Configuration")]
        [SerializeField] private ARTrackedImageManager trackedImageManager;
        [SerializeField] private TemporarySliceController sliceController;
        
        [Header("Tracking Parameters")]
        [Tooltip("Seconds of no detection before incrementing fail count")]
        [SerializeField] private float trackingTimeoutSeconds = 8.0f;
        [SerializeField] private int maxAllowedFailures = 3;
        
        [Header("Interaction Configuration")]
        [SerializeField] private BreadSliceAnimator sliceAnimator;

        private ScanningMode currentMode = ScanningMode.OrganicBread;
        private int currentFailureCount = 0;
        private float timeoutTimer = 0f;
        private bool isTrackingActive = true;
        private HashSet<string> detectedBreads = new HashSet<string>();

        private void Awake()
        {
            if (trackedImageManager == null)
            {
                trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>();
            }

            if (sliceController == null)
            {
                sliceController = FindFirstObjectByType<TemporarySliceController>();
            }
            if (sliceController == null)
            {
                GameObject mockBread = new GameObject("MockBreadMesh");
                sliceController = mockBread.AddComponent<TemporarySliceController>();
            }
        }

        private void OnEnable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
            }
        }

        private void OnDisable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            }
        }

        private void Update()
        {
            if (!isTrackingActive) return;

            // -------------------------------------------------------------
            // 2. Editor-Only Simulation Hotkeys (Developer Convenience)
            // -------------------------------------------------------------
            #if UNITY_EDITOR || true // Keep simulation active in WebGL for testing
            HandleEditorSimulationInputs();
            #endif

            // Tracking Timeout Calculation (Only count timeouts in organic bread mode)
            if (currentMode == ScanningMode.OrganicBread && detectedBreads.Count == 0)
            {
                timeoutTimer += Time.deltaTime;
                if (timeoutTimer >= trackingTimeoutSeconds)
                {
                    HandleTrackingFailure();
                }
            }
        }

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
                
                // Map tracked image names to specific Bread and Nametag IDs
                string detectedId = "";
                
                // Salt Bread mapping
                if (imageName.Contains("salt") || imageName.Contains("salt_bread"))
                {
                    detectedId = imageName.Contains("nametag") ? "salt_bread_nametag" : "salt_bread";
                }
                // Melon Bread mapping
                else if (imageName.Contains("melon") || imageName.Contains("melon_bread"))
                {
                    detectedId = imageName.Contains("nametag") ? "melon_bread_nametag" : "melon_bread";
                }
                // Croissant mapping
                else if (imageName.Contains("croissant"))
                {
                    detectedId = imageName.Contains("nametag") ? "croissant_nametag" : "croissant";
                }
                // Ham Cheese Morning Bread mapping
                else if (imageName.Contains("ham") || imageName.Contains("morning"))
                {
                    detectedId = imageName.Contains("nametag") ? "hamcheese_morning_bread_nametag" : "hamcheese_morning_bread";
                }

                if (!string.IsNullOrEmpty(detectedId))
                {
                    OnBreadOrNametagRecognized(detectedId);
                }
            }
        }

        private void OnBreadOrNametagRecognized(string detectedId)
        {
            // Check state conditions
            bool isNametag = detectedId.EndsWith("_nametag");
            string breadId = isNametag ? detectedId.Replace("_nametag", "") : detectedId;

            if (currentMode == ScanningMode.OrganicBread && !isNametag)
            {
                // Successful primary bread match!
                OnSuccess(breadId);
            }
            else if (currentMode == ScanningMode.Nametag && isNametag)
            {
                // Successful secondary nametag match fallback!
                OnSuccess(breadId);
            }
        }

        private void OnSuccess(string breadId)
        {
            timeoutTimer = 0f;
            currentFailureCount = 0;
            detectedBreads.Add(breadId);
            
            Debug.Log($"[AR Controller] Success! Recognized Bread ID: {breadId} under mode: {currentMode}");
            
            // Send trigger to browser frontend
            TriggerOnBreadDetected(breadId);
            PlayRecognitionEffect();
        }

        private void HandleTrackingFailure()
        {
            timeoutTimer = 0f;
            currentFailureCount++;
            Debug.LogWarning($"[AR Controller] Bread scan failed. Fail count: {currentFailureCount}/{maxAllowedFailures}");

            if (currentFailureCount >= maxAllowedFailures)
            {
                // Transition dynamically to Nametag Scan Mode instead of shutting down!
                currentMode = ScanningMode.Nametag;
                Debug.LogError("[AR Controller] Max fails reached. Transitioning to Nametag Scanning Mode! Invoking Web alert...");
                
                // Signal web overlay to change guide text & show fallback instructions
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
            if (sliceAnimator == null)
            {
                sliceAnimator = FindFirstObjectByType<BreadSliceAnimator>();
            }
            if (sliceAnimator != null)
            {
                sliceAnimator.ToggleSlice();
            }
        }

        // JS Command: window.unityInstance.SendMessage("ARController", "ResetTrackingFailures");
        public void ResetTrackingFailures()
        {
            Debug.Log("[AR Controller] Web UI requested tracking reset. Restarting organic scan...");
            currentFailureCount = 0;
            timeoutTimer = 0f;
            detectedBreads.Clear();
            currentMode = ScanningMode.OrganicBread;
            isTrackingActive = true;

            if (sliceAnimator == null)
            {
                sliceAnimator = FindFirstObjectByType<BreadSliceAnimator>();
            }
            if (sliceAnimator != null)
            {
                sliceAnimator.ResetSlice();
            }
        }

        // -------------------------------------------------------------
        // 4. Keyboard Testing System (WebGL Testing Simulator)
        // -------------------------------------------------------------
        private void HandleEditorSimulationInputs()
        {
            // Press '1' to simulate Organic Salt Bread scanning (Only works in OrganicBread mode)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("[Sim] Pressed 1: Simulating Organic Salt Bread tracking...");
                OnBreadOrNametagRecognized("salt_bread");
            }
            // Press '4' to simulate Salt Bread Nametag scanning (Only works in Nametag mode)
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Debug.Log("[Sim] Pressed 4: Simulating Salt Bread Nametag card tracking...");
                OnBreadOrNametagRecognized("salt_bread_nametag");
            }
            // Press 'F' to force immediate tracking failure and shift to Nametag mode
            if (Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log("[Sim] Pressed F: Simulating Organic scan failures...");
                currentFailureCount = maxAllowedFailures - 1;
                HandleTrackingFailure();
            }
        }
    }
}
