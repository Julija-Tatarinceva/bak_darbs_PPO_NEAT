using UnityEngine;

public class SeedInitializer : MonoBehaviour
{
    void Awake()
    {
        SeedManager.Initialize(100000000, 12345);
    }
}