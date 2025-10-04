using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ui_switch : MonoBehaviour
{
    public GameObject Canvas;
    private int index = 9;
    // Start is called before the first frame update
    void Start()
    {
        Canvas.SetActive(false);
        int count = Canvas.transform.childCount;

        // Hide all children
        for (int i = 0; i < count; i++)
        {
            Canvas.transform.GetChild(i).gameObject.SetActive(false);
        }

        // Start with the first child active
        if (count > 0)
        {
            index = 0;
            Canvas.transform.GetChild(index).gameObject.SetActive(true);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 
        if (Input.GetKeyDown(KeyCode.Space)) //test input
        {
            Canvas.SetActive(true);
            int count = Canvas.transform.childCount;

            // Hide the current child
            Canvas.transform.GetChild(index).gameObject.SetActive(false);

            // Move to next index (wrap around)
            index = (index + 1) % count;

            // Show the new child
            Canvas.transform.GetChild(index).gameObject.SetActive(true);

            Debug.Log("Showing child index: " + index);
        }

        if (Input.GetKeyDown(KeyCode.Z)) //test input
        {
            Canvas.SetActive(true);

            // Hide the current child
            Canvas.transform.GetChild(index).gameObject.SetActive(false);

            // Move to next index (wrap around)

            // Show the new child
            Canvas.transform.GetChild(3).gameObject.SetActive(true);

            Debug.Log("Showing child index: " + 3);
        }


        if (Input.GetKeyDown(KeyCode.E))
        {
            Canvas.SetActive(false);
        }
    }
}
