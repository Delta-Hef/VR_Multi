using UnityEngine;
using Unity.Netcode;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

public class Block_Logic : NetworkBehaviour
{
    public GameObject blockPrefab;
    public GameObject ghostPrefab;
    public float pinchThreshold = 0.045f; // Augmenté un peu pour plus de stabilité
    public float ghostOffset = 0.2f;

    private XRHandSubsystem handSubsystem;
    private GameObject ghost;
    private bool isPinching = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

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
        if (!right.isTracked) { ghost?.SetActive(false); return; }

        if (right.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose rIndex) &&
            right.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose rThumb) &&
            right.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose rPalm))
        {
            float dist = Vector3.Distance(rIndex.position, rThumb.position);
            bool pinching = dist < pinchThreshold;

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
        if (ghost == null) return;
        ghost.SetActive(true);
        ghost.transform.position = pos + palm.rotation * Vector3.forward * ghostOffset;
        ghost.transform.rotation = palm.rotation;
    }

    void PlaceNetworkedBlock()
    {
        // 1. Création locale
        GameObject block = Instantiate(blockPrefab, ghost.transform.position, ghost.transform.rotation);
        block.transform.localScale = ghost.transform.localScale;

        // 2. FORCE l'activation immédiate pour éviter l'erreur NetworkBehaviourId
        block.SetActive(true);

        if (block.TryGetComponent(out NetworkObject netObj))
        {
            // 3. Spawn Distributed (Autorité au joueur qui place)
            netObj.Spawn();

            // 4. Score (On cherche le script sur le même objet Joueur)
            ScoreDisplayVR scoreScript = GetComponent<ScoreDisplayVR>();
            if (scoreScript != null)
            {
                scoreScript.AddScore(10);
            }
        }
    }
}