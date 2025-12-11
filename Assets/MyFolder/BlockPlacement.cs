using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
using UnityEngine.SubsystemsImplementation;


//placer bug un peu et ya scaling et snapping


public class BlockPlacementPinchPro : MonoBehaviour
{
    [Header("Grid & Prefabs")]
    public float gridSize = 1f;
    public GameObject blockPrefab;
    public GameObject ghostPrefab;

    [Header("Smoothing & Pinch")]
    public float raySmoothSpeed = 20f;
    public float ghostSmooth = 10f;
    public float pinchThreshold = 0.035f;
    public float pinchReleaseThreshold = 0.05f;
    public float rayLength = 10f;

    private XRHandSubsystem handSubsystem;

    private Vector3 smoothedRayOrigin;
    private Vector3 smoothedRayDir;

    private bool isPinching = false;
    private bool ghostValid = false;
    private GameObject ghost;
    private Vector3 lastSnapPos = Vector3.zero;

    // Two-hand scaling
    private bool scalingMode = false;
    private float initialDistance;
    private Vector3 initialScale;

    // Grid tracking to prevent overlap
    private Dictionary<Vector3, GameObject> placedBlocks = new Dictionary<Vector3, GameObject>();

    void Start()
    {
        // Load XR Hand Subsystem
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0)
            handSubsystem = list[0];
        else
            Debug.LogError("No XRHandSubsystem found.");

        // Instantiate ghost
        ghost = Instantiate(ghostPrefab);
        ghost.SetActive(false);

        smoothedRayOrigin = Vector3.zero;
        smoothedRayDir = Vector3.forward;
    }

    void Update()
    {
        if (handSubsystem == null) return;

        XRHand right = handSubsystem.rightHand;
        XRHand left = handSubsystem.leftHand;

        if (!right.isTracked) return;

        // --- Get right hand joints
        if (!TryGetJointPose(right, XRHandJointID.IndexTip, out Pose rIndex) ||
            !TryGetJointPose(right, XRHandJointID.ThumbTip, out Pose rThumb))
            return;

        // --- Compute stable ray (center between index & thumb)
        Vector3 pinchCenter = (rIndex.position + rThumb.position) * 0.5f;
        Vector3 pinchDir = (rIndex.position - rThumb.position).normalized;

        float smooth = 1f - Mathf.Exp(-raySmoothSpeed * Time.deltaTime);
        smoothedRayOrigin = Vector3.Lerp(smoothedRayOrigin, pinchCenter, smooth);
        smoothedRayDir = Vector3.Lerp(smoothedRayDir, pinchDir, smooth);

        Debug.DrawRay(smoothedRayOrigin, smoothedRayDir * rayLength, Color.red);

        // --- Right hand pinch detection
        float rDist = Vector3.Distance(rIndex.position, rThumb.position);
        bool rightPinching = rDist < pinchThreshold;

        // Hysteresis
        if (!isPinching && rightPinching)
            isPinching = true;
        else if (isPinching && rDist > pinchReleaseThreshold)
        {
            isPinching = false;
            if (!scalingMode)
                TryPlaceBlock();
        }

        // --- Left hand pinch detection
        bool leftPinching = false;
        Pose lIndex = default, lThumb = default;

        if (left.isTracked &&
            TryGetJointPose(left, XRHandJointID.IndexTip, out lIndex) &&
            TryGetJointPose(left, XRHandJointID.ThumbTip, out lThumb))
        {
            float lDist = Vector3.Distance(lIndex.position, lThumb.position);
            leftPinching = lDist < pinchThreshold;
        }

        // --- Two-hand scaling mode
        if (rightPinching && leftPinching && ghostValid && !scalingMode)
        {
            scalingMode = true;
            initialDistance = Vector3.Distance(rIndex.position, lIndex.position);
            initialScale = ghost.transform.localScale;
        }

        if (!rightPinching || !leftPinching)
            scalingMode = false;

        if (scalingMode)
        {
            float currentDist = Vector3.Distance(rIndex.position, lIndex.position);
            float ratio = currentDist / initialDistance;
            ghost.transform.localScale = initialScale * ratio;
            return; // skip placement while scaling
        }

        // --- Update ghost
        UpdateGhostPreview();
    }

    // Utility: safe joint pose
    bool TryGetJointPose(XRHand hand, XRHandJointID jointID, out Pose pose)
    {
        XRHandJoint joint = hand.GetJoint(jointID);
        return joint.TryGetPose(out pose);
    }

    // Ghost preview with snapping & collision check
    void UpdateGhostPreview()
    {
        RaycastHit hit;
        if (Physics.Raycast(smoothedRayOrigin, smoothedRayDir, out hit, rayLength))
        {
            Vector3 snapped = Snap(hit.point + hit.normal * (gridSize * 0.5f));

            // Prevent overlapping: check existing blocks
            if (placedBlocks.ContainsKey(snapped))
            {
                snapped += Vector3.up * gridSize;
                if (placedBlocks.ContainsKey(snapped))
                {
                    ghostValid = false;
                    ghost.SetActive(false);
                    return;
                }
            }

            // Smooth ghost movement
            ghost.transform.position = Vector3.Lerp(ghost.transform.position, snapped, Time.deltaTime * ghostSmooth);
            ghost.transform.rotation = Quaternion.identity;
            ghost.SetActive(true);
            ghostValid = true;
            lastSnapPos = snapped;
        }
        else
        {
            ghost.SetActive(false);
            ghostValid = false;
        }
    }

    // Place block at ghost
    void TryPlaceBlock()
    {
        if (!ghostValid) return;

        Vector3 pos = ghost.transform.position;

        if (placedBlocks.ContainsKey(pos)) return; // safeguard

        GameObject newBlock = Instantiate(blockPrefab, pos, ghost.transform.rotation);
        newBlock.transform.localScale = ghost.transform.localScale;

        placedBlocks[pos] = newBlock;
    }

    // Snap to grid
    Vector3 Snap(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x / gridSize) * gridSize,
            Mathf.Round(pos.y / gridSize) * gridSize,
            Mathf.Round(pos.z / gridSize) * gridSize
        );
    }
}
