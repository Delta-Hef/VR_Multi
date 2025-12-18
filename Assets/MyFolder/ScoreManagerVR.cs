using UnityEngine;
using Unity.Netcode;

public class ScoreDisplayVR : NetworkBehaviour
{
    // NetworkVariable synchronisée automatiquement (Propriétaire peut écrire, tout le monde peut lire)
    public NetworkVariable<int> score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public GameObject playerCamera;
    private TextMesh scoreText;

    public override void OnNetworkSpawn()
    {
        // Initialisation du texte
        GameObject scoreTextObj = new GameObject("ScoreText_" + OwnerClientId);

        // Si c'est notre propre score, on l'attache à notre caméra
        if (IsOwner)
        {
            if (playerCamera == null) playerCamera = Camera.main.gameObject;
            scoreTextObj.transform.SetParent(playerCamera.transform);
            scoreTextObj.transform.localPosition = new Vector3(0f, 0.2f, 1.5f);
        }
        else
        {
            // Si c'est le score de l'autre, on l'attache au-dessus de son avatar
            scoreTextObj.transform.SetParent(transform);
            scoreTextObj.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        }

        scoreTextObj.transform.localRotation = Quaternion.identity;
        scoreTextObj.transform.localScale = Vector3.one * 0.003f;

        scoreText = scoreTextObj.AddComponent<TextMesh>();
        scoreText.fontSize = 100;
        scoreText.color = IsOwner ? Color.yellow : Color.white;
        scoreText.anchor = TextAnchor.MiddleCenter;

        // S'abonner aux changements de valeur pour mettre à jour l'affichage
        score.OnValueChanged += (oldVal, newVal) => { UpdateScoreText(newVal); };
        UpdateScoreText(score.Value);
    }

    public void AddScore(int points)
    {
        if (IsOwner) score.Value += points;
    }

    void UpdateScoreText(int currentScore)
    {
        if (scoreText != null)
            scoreText.text = (IsOwner ? "Moi: " : "Ami: ") + currentScore;
    }
}