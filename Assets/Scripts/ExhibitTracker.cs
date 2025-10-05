using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class ExhibitTracker : MonoBehaviour
{
    public static ExhibitTracker Instance;

    private HashSet<string> visitedExhibits = new HashSet<string>();
    public int totalExhibits = 10;

    [Header("UI Elements")]
    public GameObject rewardPanel;
    public TextMeshProUGUI counterText;

    [Header("Celebration Effects")]
    public ParticleSystem confettiEffect; // Assign in Inspector
    public Transform playerTransform;     // Assign CenterEyeAnchor or player rig

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void MarkVisited(string exhibitID)
    {
        if (visitedExhibits.Contains(exhibitID)) return;

        visitedExhibits.Add(exhibitID);
        UpdateCounter();

        if (visitedExhibits.Count >= totalExhibits)
        {
            TriggerReward();
        }
    }

    void UpdateCounter()
    {
        if (counterText != null)
        {
            counterText.text = $"Visited: {visitedExhibits.Count}/{totalExhibits}";
        }
    }

    void TriggerReward()
    {
        Debug.Log("All exhibits visited!");

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(true);
        }

        if (confettiEffect != null && playerTransform != null)
        {
            // Position confetti slightly above the player
            confettiEffect.transform.position = playerTransform.position + new Vector3(0, 0.5f, 0);
            confettiEffect.gameObject.SetActive(true);
            confettiEffect.Play();
        }
    }
}
