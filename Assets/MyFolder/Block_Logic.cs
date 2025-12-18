using UnityEngine;
using Unity.Netcode;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

public class Block_Logic : NetworkBehaviour
{
    public GameObject blockPrefab;
    public GameObject ghostPrefab;
    public float pinchThreshold = 0.035f;
    public float ghostOffset = 0.2f;

    private XRHandSubsystem handSubsystem;
    private GameObject ghost;
    private bool isPinching = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // Uniquement pour le joueur local

        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0) handSubsystem = list[0];

        ghost = Instantiate(ghostPrefab);
        ghost.SetActive(false);
    }

    void Update()
    {
        if (!IsOwner || handSubsystem == null) return;

        XRHand right = handSubsystem.rightHand;
        if (!right.isTracked) return;

        if (right.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose rIndex) &&
            right.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose rThumb) &&
            right.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose rPalm))
        {
            float dist = Vector3.Distance(rIndex.position, rThumb.position);
            bool pinching = dist < pinchThreshold;

            // Mise à jour du Ghost (visuel local)
            UpdateGhost(rIndex.position, rPalm);

            if (!isPinching && pinching) isPinching = true;
            else if (isPinching && !pinching)
            {
                isPinching = false;
                PlaceNetworkedBlock();
            }
        }
    }

    void UpdateGhost(Vector3 pos, Pose palm)
    {
        ghost.SetActive(true);
        ghost.transform.position = pos + palm.rotation * Vector3.forward * ghostOffset;
        ghost.transform.rotation = palm.rotation;
    }

    void PlaceNetworkedBlock()
    {
        // Création et synchronisation immédiate
        GameObject block = Instantiate(blockPrefab, ghost.transform.position, ghost.transform.rotation);
        block.transform.localScale = ghost.transform.localScale;

        if (block.TryGetComponent(out NetworkObject netObj))
        {
            netObj.Spawn(); // Synchronise avec ton amie

            // Ajouter 10 points
            if (TryGetComponent(out ScoreDisplayVR scoreScript))
                scoreScript.AddScore(10);
        }
    }
}