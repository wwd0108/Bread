using UnityEngine;

namespace BreadAR
{
    public class BreadSliceAnimator : MonoBehaviour
    {
        [Header("Slice Settings")]
        [Tooltip("The left half of the 3D model that will slide left")]
        [SerializeField] private Transform leftHalf;
        
        [Tooltip("The right half of the 3D model that will slide right")]
        [SerializeField] private Transform rightHalf;

        [Tooltip("Direction to slide (local space)")]
        [SerializeField] private Vector3 sliceDirection = Vector3.right;

        [Tooltip("How far the halves slide apart")]
        [SerializeField] private float sliceDistance = 1.2f;

        [Tooltip("Speed of the slice animation")]
        [SerializeField] private float animationSpeed = 4.0f;

        private bool isSliced = false;
        private Vector3 leftStartPos;
        private Vector3 rightStartPos;
        private Vector3 leftTargetPos;
        private Vector3 rightTargetPos;

        private void Start()
        {
            // Auto-detect halves if not manually assigned
            if (leftHalf == null && transform.childCount >= 1) leftHalf = transform.GetChild(0);
            if (rightHalf == null && transform.childCount >= 2) rightHalf = transform.GetChild(1);

            if (leftHalf != null)
            {
                leftStartPos = leftHalf.localPosition;
                leftTargetPos = leftStartPos - (sliceDirection.normalized * sliceDistance);
            }

            if (rightHalf != null)
            {
                rightStartPos = rightHalf.localPosition;
                rightTargetPos = rightStartPos + (sliceDirection.normalized * sliceDistance);
            }
        }

        private void Update()
        {
            if (leftHalf == null || rightHalf == null) return;

            // Interpolate position smoothly based on isSliced toggle state
            Vector3 targetL = isSliced ? leftTargetPos : leftStartPos;
            Vector3 targetR = isSliced ? rightTargetPos : rightStartPos;

            leftHalf.localPosition = Vector3.Lerp(leftHalf.localPosition, targetL, Time.deltaTime * animationSpeed);
            rightHalf.localPosition = Vector3.Lerp(rightHalf.localPosition, targetR, Time.deltaTime * animationSpeed);
        }

        // Public trigger called from ARTrackingController / JS Bridge
        public void ToggleSlice()
        {
            isSliced = !isSliced;
            Debug.Log($"[Bread Slice] Toggle Slice state to: {isSliced}");
        }

        public void ResetSlice()
        {
            isSliced = false;
            Debug.Log("[Bread Slice] Reset slice state (Closed)");
        }
    }
}
