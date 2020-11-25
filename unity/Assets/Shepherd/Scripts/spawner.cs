using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawner : MonoBehaviour {
    //public Transform spawnPos;
    public GameObject shepherd;
    private bool cooldown = false;

    // Update is called once per frame
    void Update() {
        // Create an object when the mouse is pressed, don't spam this function
        if (Input.GetMouseButton(0) && !cooldown) {
            var mousePos = Input.mousePosition;
            mousePos.z = 2.0f;
            var objectPos = Camera.main.ScreenToWorldPoint(mousePos);
            Instantiate(shepherd, objectPos, Quaternion.identity);

            Invoke("ResetCooldown", 0.5f);
            cooldown = true;
        }
    }

    private void ResetCooldown() {
        cooldown = false;
    }
}
