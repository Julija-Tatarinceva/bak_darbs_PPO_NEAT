using UnityEditor;
using UnityEngine;

public class AutoRun
{
    public static void StartSimulation()
    {
        // Устанавливаем масштаб времени (ускоряем симуляцию)
        Time.timeScale = 10.0f; 
        
        // Включаем режим игры
        EditorApplication.isPlaying = true;
        
        Debug.Log("Simulation Started via CLI!");
    }
}