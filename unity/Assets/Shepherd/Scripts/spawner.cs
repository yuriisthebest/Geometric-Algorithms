using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawner : MonoBehaviour
{
    //public Transform spawnPos;
    public GameObject shepherd;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            var mousePos = Input.mousePosition;
            mousePos.z = 2.0f;
            var objectPos = Camera.main.ScreenToWorldPoint(mousePos);
            Instantiate(shepherd, objectPos, Quaternion.identity);
            //Instantiate(shepherd, spawnPos.position, spawnPos.rotation);
        }
    }
}
