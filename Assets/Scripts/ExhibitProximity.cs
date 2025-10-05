using UnityEngine;

public class ExhibitProximity : MonoBehaviour
{
    public GameObject uiPanel;
    public string exhibitID;

void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
    {
        uiPanel.SetActive(true);

        // Skip tracking for lobby or non-exhibit signs
        if (exhibitID != "Lobby")
        {
            ExhibitTracker.Instance?.MarkVisited(exhibitID);
        }
    }
}


    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            uiPanel.SetActive(false);
        }
    }
}

