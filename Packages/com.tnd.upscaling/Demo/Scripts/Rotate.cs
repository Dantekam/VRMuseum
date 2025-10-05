using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float m_speed = 200;
    // Update is called once per frame
    void Update() {
        transform.Rotate(0, Time.deltaTime * m_speed, 0);
    }
}
