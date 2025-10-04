using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ui_switch : MonoBehaviour
{
    public GameObject Canvas;
    public int index = 0;
    // Start is called before the first frame update
    void Start()
    {
        Canvas.SetActive(false);
    }   

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Canvas.SetActive(true);
            Canvas.transform.GetChild(index);
            Debug.Log(index);
            index++;
        }
    }
}
