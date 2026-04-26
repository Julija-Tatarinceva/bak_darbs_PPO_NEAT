using UnityEngine;


public class GreenCube : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BlueCubeAgent"))
        {
            Debug.Log("Blue Cube reached the Green Cube!");
        }
    }
}