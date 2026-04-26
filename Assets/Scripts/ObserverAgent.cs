using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// ObserverAgent can see the target position and must communicate it to the NavigatorAgent.
/// It learns which symbols are most effective for guiding the navigator.
/// </summary>
public class ObserverAgent : Agent
{
    [Header("References")]
    public Transform target;
    public EnvironmentManager environmentManager;
    public CommunicationManager communicationManager;
    
    [Header("Communication Settings")]
    [Tooltip("Number of discrete symbols available for communication")]
    public int vocabularySize = 4;
    
    private int lastSentMessage = 0;
    
    public override void Initialize()
    {
        // Ensure communication manager is set up
        if (communicationManager == null)
        {
            communicationManager = GetComponentInParent<CommunicationManager>();
        }
        if (environmentManager == null)
        {
            environmentManager = GetComponentInParent<EnvironmentManager>();
        }
    }
    
    public override void OnEpisodeBegin()
    {
        lastSentMessage = 0;
    }
    
    void FixedUpdate()
    {
        // Request a decision every physics step
        RequestDecision();
        Debug.Log($"[OBSERVER] FixedUpdate - Requesting decision, lastMessage: {lastSentMessage}");
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // Observe target position relative to navigator (not self)
        // This encourages the observer to think about what the navigator needs to know
        Vector3 targetPos = target.position;
        Vector3 navigatorPos = environmentManager.navigatorAgent.transform.position;
        Vector3 relativeTargetPos = targetPos - navigatorPos;
        
        // Normalize the relative position
        float normRelX = relativeTargetPos.x / 20f;
        float normRelZ = relativeTargetPos.z / 20f;
        float normDist = Vector3.Distance(navigatorPos, targetPos) / 20f;
        
        sensor.AddObservation(normRelX);
        sensor.AddObservation(normRelZ);
        sensor.AddObservation(normDist);
        
        // Also observe own position relative to target (helps with spatial reasoning)
        Vector3 observerRelativePos = target.position - transform.position;
        float normObsX = observerRelativePos.x / 20f;
        float normObsZ = observerRelativePos.z / 20f;
        
        sensor.AddObservation(normObsX);
        sensor.AddObservation(normObsZ);
        
        // Observe the last message sent (helps with temporal consistency)
        float normMsg = lastSentMessage / (float)vocabularySize;
        sensor.AddObservation(normMsg);
        
        // DEBUG: Print observations
        Debug.Log($"[OBSERVER] Observations - Target→Nav: ({normRelX:F2}, {normRelZ:F2}), Dist: {normDist:F2}, Target→Obs: ({normObsX:F2}, {normObsZ:F2}), LastMsg: {lastSentMessage}");
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Get the discrete communication action
        int messageToSend = actions.DiscreteActions[0];
        lastSentMessage = messageToSend;
        
        // DEBUG: Print action
        Debug.Log($"[OBSERVER] Sending message: {messageToSend}");
        
        // Send message through the communication manager (handles noise/delay)
        if (communicationManager != null)
        {
            communicationManager.SendMessage(messageToSend);
        }
        
        // Small penalty per step to encourage efficient communication
        AddReward(-0.001f);
    }
    
    /// <summary>
    /// Called by EnvironmentManager when the navigator reaches the target.
    /// Observer gets rewarded for successful guidance.
    /// </summary>
    public void OnNavigatorSuccess()
    {
        float reward = 1.0f;
        AddReward(reward);
        Debug.Log($"[OBSERVER] Navigator SUCCESS! Reward: +{reward}");
    }
    
    /// <summary>
    /// Called when episode times out without success.
    /// </summary>
    public void OnNavigatorFailed()
    {
        float penalty = -0.5f;
        AddReward(penalty);
        Debug.Log($"[OBSERVER] Navigator FAILED. Penalty: {penalty}");
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual control for testing: cycle through messages with number keys
        var discreteActions = actionsOut.DiscreteActions;
        
        if (Input.GetKeyDown(KeyCode.Alpha1)) discreteActions[0] = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha2)) discreteActions[0] = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha3)) discreteActions[0] = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha4)) discreteActions[0] = 3;
        else discreteActions[0] = lastSentMessage; // Keep sending same message
    }
}
