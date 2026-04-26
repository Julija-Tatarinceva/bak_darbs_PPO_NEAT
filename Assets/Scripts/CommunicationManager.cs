using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages communication between ObserverAgent and NavigatorAgent.
/// Handles message delay, noise, and delivery.
/// </summary>
public class CommunicationManager : MonoBehaviour
{
    [Header("Communication Settings")]
    [Tooltip("Number of discrete symbols in the vocabulary")]
    public int vocabularySize = 4;
    
    [Header("Noise Settings")]
    [Tooltip("Probability (0-1) that a message gets corrupted")]
    [Range(0f, 1f)]
    public float noiseProbability = 0.0f;
    
    [Header("Delay Settings")]
    [Tooltip("Number of steps to delay message delivery (0 = instant)")]
    public int delaySteps = 0;
    
    // Message queue for implementing delay
    private Queue<int> messageQueue = new Queue<int>();
    private int currentDeliveredMessage = 0;
    
    void Start()
    {
        // Initialize the queue with default messages (0)
        for (int i = 0; i <= delaySteps; i++)
        {
            messageQueue.Enqueue(0);
        }
    }
    
    /// <summary>
    /// Called by ObserverAgent to send a message.
    /// </summary>
    public void SendMessage(int message)
    {
        // Clamp message to valid range
        message = Mathf.Clamp(message, 0, vocabularySize - 1);
        
        // Apply noise if enabled
        int originalMessage = message;
        if (noiseProbability > 0 && Random.value < noiseProbability)
        {
            // Replace with random symbol
            message = Random.Range(0, vocabularySize);
            Debug.Log($"[COMM] Message corrupted by noise! Original: {originalMessage} → Noisy: {message}");
        }
        
        // Add to delay queue
        messageQueue.Enqueue(message);
        
        // If delay is 0, deliver immediately
        if (delaySteps == 0)
        {
            currentDeliveredMessage = messageQueue.Dequeue();
            Debug.Log($"[COMM] Message sent: {originalMessage} → Delivered immediately: {currentDeliveredMessage}");
        }
        else
        {
            // Otherwise, dequeue the oldest message (implements delay)
            if (messageQueue.Count > delaySteps)
            {
                currentDeliveredMessage = messageQueue.Dequeue();
                Debug.Log($"[COMM] Message sent: {originalMessage} → Delivered (delayed): {currentDeliveredMessage}, Queue size: {messageQueue.Count}");
            }
        }
    }
    
    /// <summary>
    /// Called by NavigatorAgent to get the currently delivered message.
    /// </summary>
    public int GetDeliveredMessage()
    {
        return currentDeliveredMessage;
    }
    
    /// <summary>
    /// Reset the communication channel at episode start.
    /// </summary>
    public void ResetCommunication()
    {
        messageQueue.Clear();
        currentDeliveredMessage = 0;
        
        // Refill queue with zeros
        for (int i = 0; i <= delaySteps; i++)
        {
            messageQueue.Enqueue(0);
        }
        
        Debug.Log($"[COMM] Communication channel reset. Delay steps: {delaySteps}, Noise probability: {noiseProbability}");
    }
    
    /// <summary>
    /// Visualize the current message in the editor.
    /// </summary>
    void OnGUI()
    {
        if (Application.isPlaying)
        {
            GUI.Box(new Rect(10, 160, 200, 60), $"Communication\nCurrent Message: {currentDeliveredMessage}\nQueue Size: {messageQueue.Count}");
        }
    }
}
