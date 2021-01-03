using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OwnerScript : MonoBehaviour
{

    public int owner;

    public void SetOwner(int newOwner)
    {
        this.owner = newOwner;
    }

    public int GetOwner()
    {
        return this.owner;
    }
}
