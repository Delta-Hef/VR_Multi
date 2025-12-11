using UnityEngine;

public class ScoreDisplayVR : MonoBehaviour
{
    public int score = 0;
    public GameObject playerCamera;
    private GameObject scoreTextObj;
    private TextMesh scoreText;

    void Start()
    {
        if (playerCamera == null)
        {
            Camera cam = FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("Aucune caméra VR détectée.");
                return;
            }
            playerCamera = cam.gameObject;
        }

        scoreTextObj = new GameObject("ScoreText");
        scoreTextObj.transform.SetParent(playerCamera.transform);

        // recule de toi, plus haut, et plus petit
        scoreTextObj.transform.localPosition = new Vector3(0f, 0.2f, 1.5f);
        scoreTextObj.transform.localRotation = Quaternion.identity;

        // SCALE VR QUI FAIT TOUTE LA DIFFÉRENCE
        scoreTextObj.transform.localScale = Vector3.one * 0.003f;

        scoreText = scoreTextObj.AddComponent<TextMesh>();
        scoreText.fontSize = 100;
        scoreText.color = Color.yellow;
        scoreText.anchor = TextAnchor.MiddleCenter;
        scoreText.alignment = TextAlignment.Center;

        UpdateScoreText();
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateScoreText();
    }

    public void RemoveScore(int points)
    {
        score -= points;
        if (score < 0) score = 0;
        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        scoreText.text = "Score : " + score;
    }
}
