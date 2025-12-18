using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class VRSpawnDistributed : NetworkBehaviour
{
    [Header("Configuration")]
    public GameObject blockPrefab;
    public float spawnDistance = 1.0f;

    void Update()
    {
        // On ne gère que les entrées du joueur local possesseur de ce rig
        if (!IsOwner || !IsSpawned) return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            Debug.Log("F détecté : Création en mode Distributed Authority...");
            SpawnBlockDistributed();
        }
    }

    void SpawnBlockDistributed()
    {
        // 1. Calcul de la position devant vous (Caméra ou Main)
        Transform origin = Camera.main != null ? Camera.main.transform : transform;
        Vector3 spawnPos = origin.position + origin.forward * spawnDistance;

        // 2. Instanciation LOCALE (Standard Unity)
        GameObject newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);

        // 3. RÉSEAU : En mode Distributed Authority, le client "Spawn" directement
        if (newBlock.TryGetComponent(out NetworkObject netObj))
        {
            // Cette ligne rend l'objet visible pour votre amie immédiatement
            netObj.Spawn();
            Debug.Log("<color=green>SUCCESS : Bloc synchronisé sur le réseau !</color>");
        }
        else
        {
            Debug.LogError("Le prefab n'a pas de NetworkObject !");
        }
    }
}