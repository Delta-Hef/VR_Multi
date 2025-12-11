using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
using UnityEngine.SubsystemsImplementation;

public class BlockPlacementVRPhysicsScaling : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject blockPrefab;
    public GameObject ghostPrefab;

    [Header("Pinch & Scaling")]
    public float pinchThreshold = 0.035f;
    public float pinchReleaseThreshold = 0.05f;
    public float ghostFollowSpeed = 15f;
    public float ghostOffset = 0.2f; // 20 cm in front of hand

    private XRHandSubsystem handSubsystem;

    private GameObject ghost;
    private bool isPinching = false;
    private bool ghostValid = false;
    private int points;

    // Two-hand scaling
    private bool scalingMode = false;
    private float initialDistance;
    private Vector3 initialScale;

    void Start()
    {
        // Load XR Hand Subsystem
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0)
            handSubsystem = list[0];
        else
            Debug.LogError("No XRHandSubsystem found.");

        ghost = Instantiate(ghostPrefab);
        ghost.SetActive(false);
    }

    void Update()
    {
        if (handSubsystem == null) return;

        XRHand right = handSubsystem.rightHand;
        XRHand left = handSubsystem.leftHand;

        if (!right.isTracked) return;

        // --- Right hand joints
        if (!TryGetJointPose(right, XRHandJointID.IndexTip, out Pose rIndex) ||
            !TryGetJointPose(right, XRHandJointID.ThumbTip, out Pose rThumb) ||
            !TryGetJointPose(right, XRHandJointID.Palm, out Pose rPalm))
            return;

        Vector3 pinchCenter = (rIndex.position + rThumb.position) * 0.5f;
        float rDist = Vector3.Distance(rIndex.position, rThumb.position);
        bool rightPinching = rDist < pinchThreshold;

        // --- Left hand joints for scaling
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
        if (rightPinching && leftPinching && !scalingMode)
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
            ghostValid = true;
        }

        // --- Update ghost preview
        UpdateGhost(pinchCenter, rPalm);

        // --- Pinch release to place block
        if (!isPinching && rightPinching)
            isPinching = true;
        else if (isPinching && rDist > pinchReleaseThreshold)
        {
            isPinching = false;
            if (ghostValid && !scalingMode)
                TryPlaceBlock();
        }
    }

    void UpdateGhost(Vector3 targetPos, Pose handPose)
    {
        if (ghost == null) return;

        ghost.SetActive(true);

        // Offset in front of hand
        Vector3 offsetPos = targetPos + handPose.rotation * Vector3.forward * ghostOffset;

        // Smooth follow
        ghost.transform.position = Vector3.Lerp(ghost.transform.position, offsetPos, Time.deltaTime * ghostFollowSpeed);
        ghost.transform.rotation = Quaternion.Slerp(ghost.transform.rotation, handPose.rotation, Time.deltaTime * ghostFollowSpeed);

        ghostValid = true;
    }

    void TryPlaceBlock()
    {
        if (!ghostValid) return;

        GameObject newBlock = Instantiate(blockPrefab, ghost.transform.position, ghost.transform.rotation);

        ScoreDisplayVR display = FindObjectOfType<ScoreDisplayVR>();
        if (display != null)
        {
            display.AddScore(10);
        }
        // Add Rigidbody if missing
        if (newBlock.GetComponent<Rigidbody>() == null)
            newBlock.AddComponent<Rigidbody>();

        // Ensure collider exists
        if (newBlock.GetComponent<Collider>() == null)
            newBlock.AddComponent<BoxCollider>();

        // Apply ghost scale
        newBlock.transform.localScale = ghost.transform.localScale;
    }

    bool TryGetJointPose(XRHand hand, XRHandJointID jointID, out Pose pose)
    {
        XRHandJoint joint = hand.GetJoint(jointID);
        return joint.TryGetPose(out pose);
    }
}
