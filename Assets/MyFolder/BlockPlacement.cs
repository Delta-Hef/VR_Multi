using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
using UnityEngine.SubsystemsImplementation;
using Unity.Netcode;

public class BlockPlacementVRPhysicsScaling : NetworkBehaviour
{
    [Header("Prefabs")]
    public GameObject blockPrefab;
    public GameObject ghostPrefab;

    [Header("Pinch & Scaling")]
    public float pinchThreshold = 0.045f;
    public float pinchReleaseThreshold = 0.06f;
    public float ghostFollowSpeed = 15f;
    public float ghostOffset = 0.2f;

    [Header("Keyboard Test")]
    public KeyCode spawnTestKey = KeyCode.F;
    public float keyboardSpawnDistance = 1.2f;

    private XRHandSubsystem handSubsystem;
    private GameObject ghost;

    private bool isPinching;
    private bool ghostValid;

    private bool scalingMode;
    private float initialDistance;
    private Vector3 initialScale;

    private Camera playerCamera;

    // =========================
    // NETCODE LIFECYCLE
    // =========================
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // XR Hands subsystem
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            handSubsystem = subsystems[0];

        // Ghost (LOCAL ONLY)
        ghost = Instantiate(ghostPrefab);
        ghost.SetActive(false);

        playerCamera = Camera.main;
    }

    // =========================
    // UPDATE
    // =========================
    void Update()
    {
        if (!IsOwner)
            return;

        // -------- Keyboard test (VERY IMPORTANT)
        if (Input.GetKeyDown(spawnTestKey))
        {
            SpawnFromKeyboard();
        }

        // -------- XR Hands logic
        if (handSubsystem == null)
            return;

        XRHand right = handSubsystem.rightHand;
        XRHand left = handSubsystem.leftHand;

        if (!right.isTracked)
            return;

        if (!TryGetJointPose(right, XRHandJointID.IndexTip, out Pose rIndex) ||
            !TryGetJointPose(right, XRHandJointID.ThumbTip, out Pose rThumb))
            return;

        TryGetJointPose(right, XRHandJointID.Palm, out Pose rPalm);

        Vector3 pinchCenter = (rIndex.position + rThumb.position) * 0.5f;
        float rDist = Vector3.Distance(rIndex.position, rThumb.position);
        bool rightPinching = rDist < pinchThreshold;

        // -------- Scaling with left hand
        bool leftPinching = false;
        if (left.isTracked &&
            TryGetJointPose(left, XRHandJointID.IndexTip, out Pose lIndex) &&
            TryGetJointPose(left, XRHandJointID.ThumbTip, out Pose lThumb))
        {
            leftPinching = Vector3.Distance(lIndex.position, lThumb.position) < pinchThreshold;

            if (rightPinching && leftPinching && !scalingMode)
            {
                scalingMode = true;
                initialDistance = Vector3.Distance(rIndex.position, lIndex.position);
                initialScale = ghost.transform.localScale;
            }

            if (scalingMode && rightPinching && leftPinching)
            {
                float currentDist = Vector3.Distance(rIndex.position, lIndex.position);
                ghost.transform.localScale = initialScale * (currentDist / initialDistance);
            }
        }

        if (!rightPinching || !leftPinching)
            scalingMode = false;

        UpdateGhost(pinchCenter, rPalm);

        // -------- Placement
        if (!isPinching && rightPinching)
            isPinching = true;
        else if (isPinching && rDist > pinchReleaseThreshold)
        {
            isPinching = false;
            if (ghostValid && !scalingMode)
                RequestPlaceBlock();
        }
    }

    // =========================
    // GHOST
    // =========================
    void UpdateGhost(Vector3 targetPos, Pose palmPose)
    {
        if (ghost == null)
            return;

        ghost.SetActive(true);

        Quaternion rot = palmPose.rotation == Quaternion.identity
            ? Quaternion.LookRotation(Camera.main.transform.forward)
            : palmPose.rotation;

        Vector3 offsetPos = targetPos + rot * Vector3.forward * ghostOffset;

        ghost.transform.position = Vector3.Lerp(
            ghost.transform.position,
            offsetPos,
            Time.deltaTime * ghostFollowSpeed);

        ghost.transform.rotation = Quaternion.Slerp(
            ghost.transform.rotation,
            rot,
            Time.deltaTime * ghostFollowSpeed);

        ghostValid = true;
    }

    // =========================
    // REQUEST PLACE
    // =========================
    void RequestPlaceBlock()
    {
        PlaceBlockServerRpc(
            ghost.transform.position,
            ghost.transform.rotation,
            ghost.transform.localScale
        );

        ScoreDisplayVR score = GetComponent<ScoreDisplayVR>();
        if (score != null)
            score.AddScore(10);
    }

    // =========================
    // KEYBOARD TEST SPAWN
    // =========================
    void SpawnFromKeyboard()
    {
        if (playerCamera == null)
            return;

        Vector3 pos = playerCamera.transform.position +
                      playerCamera.transform.forward * keyboardSpawnDistance;

        Quaternion rot = Quaternion.LookRotation(playerCamera.transform.forward);

        PlaceBlockServerRpc(pos, rot, Vector3.one);
    }

    // =========================
    // SERVER RPC
    // =========================
    [ServerRpc]
    void PlaceBlockServerRpc(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject block = Instantiate(blockPrefab, pos, rot);
        block.transform.localScale = scale;

        NetworkObject netObj = block.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
    }

    // =========================
    // UTILS
    // =========================
    bool TryGetJointPose(XRHand hand, XRHandJointID jointID, out Pose pose)
    {
        return hand.GetJoint(jointID).TryGetPose(out pose);
    }
}
