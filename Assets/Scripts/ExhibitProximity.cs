using UnityEngine;

public class ExhibitProximity : MonoBehaviour
{
    public GameObject uiPanel;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            uiPanel.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            uiPanel.SetActive(false);
        }
    }
}
