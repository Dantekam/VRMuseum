using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test_move : MonoBehaviour
{
    public float speed = 10f;
    void Update()
    {
        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.UpArrow))
        {
            move += transform.forward;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.DownArrow))
        {
            move += -transform.forward;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.LeftArrow))
        {
            move += -transform.right;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.RightArrow))
        {
            move += transform.right;
        }

        transform.position += move.normalized * speed * Time.deltaTime;    
    }

}
