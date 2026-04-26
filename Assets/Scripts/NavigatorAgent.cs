using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// NavigatorAgent cannot see the target but must reach it based on messages from ObserverAgent.
/// It learns to interpret the symbolic messages and navigate accordingly.
/// </summary>
public class NavigatorAgent : Agent
{
    [Header("References")]
    public Transform target;
    public EnvironmentManager environmentManager;
    public CommunicationManager communicationManager;
    public Rigidbody rb;
    
    [Header("Movement Settings")]
    [Tooltip("Force multiplier for movement")]
    public float moveForce = 30f;
    
    [Header("Reward Settings")]
    [Tooltip("Distance threshold to consider target reached")]
    public float targetReachDistance = 1.5f;
    
    private int receivedMessage = 0;
    private int episodeSteps = 0;
    private int maxEpisodeSteps = 1000;
    private float previousDistanceToTarget = float.MaxValue;
    
    public override void Initialize()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
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
        // Reset physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        episodeSteps = 0;
        receivedMessage = 0;
        
        // Initialize distance tracking
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);
    }
    
    void FixedUpdate()
    {
        // Request a decision every physics step
        RequestDecision();
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // Observe own position (normalized)
        float normPosX = transform.localPosition.x / 20f;
        float normPosZ = transform.localPosition.z / 20f;
        
        sensor.AddObservation(normPosX);
        sensor.AddObservation(normPosZ);
        
        // Observe own velocity (helps with momentum control)
        float normVelX = rb.linearVelocity.x / 5f;
        float normVelZ = rb.linearVelocity.z / 5f;
        
        sensor.AddObservation(normVelX);
        sensor.AddObservation(normVelZ);
        
        // Observe the received message from ObserverAgent
        // This is the key observation - the only information about the target
        if (communicationManager != null)
        {
            receivedMessage = communicationManager.GetDeliveredMessage();
        }
        float normMsg = receivedMessage / (float)communicationManager.vocabularySize;
        sensor.AddObservation(normMsg);
        
        // DEBUG: Print observations and distance to target
        float distToTarget = Vector3.Distance(transform.position, target.position);
        Debug.Log($"[NAVIGATOR] Step {episodeSteps} - Pos: ({normPosX:F2}, {normPosZ:F2}), Vel: ({normVelX:F2}, {normVelZ:F2}), ReceivedMsg: {receivedMessage}, DistToTarget: {distToTarget:F2}");
        
        // Note: We do NOT observe the target position directly
        // The navigator must learn to interpret the messages
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeSteps++;
        
        // Check for max steps timeout
        if (episodeSteps >= maxEpisodeSteps)
        {
            Debug.Log($"[NAVIGATOR] Episode TIMEOUT at step {episodeSteps}");
            AddReward(-1.0f);
            environmentManager.EndEpisode(false);
            return;
        }
        
        // Apply movement forces
        Vector3 moveDirection = new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]);
        rb.AddForce(moveDirection * moveForce);
        
        // DEBUG: Print actions
        Debug.Log($"[NAVIGATOR] Action: ({actions.ContinuousActions[0]:F2}, {actions.ContinuousActions[1]:F2})");
        
        // Small time penalty to encourage efficiency
        AddReward(-0.001f);
        
        // Reward for reducing distance to target (shaped reward)
        // This provides a gradient for learning without being exploitable
        float currentDistanceToTarget = Vector3.Distance(transform.position, target.position);
        
        // CRITICAL: Only compute distance reduction if previousDistance is valid (not first step)
        if (episodeSteps > 1 && previousDistanceToTarget != float.MaxValue)
        {
            float distanceReduction = previousDistanceToTarget - currentDistanceToTarget;
            
            // Reward progress AND penalize moving away (creates a proper gradient)
            AddReward(distanceReduction * 0.5f); // Increased multiplier, now symmetric reward/penalty
            
            if (distanceReduction > 0)
            {
                Debug.Log($"[NAVIGATOR] Progress! Reduced distance by {distanceReduction:F3}, reward: +{distanceReduction * 0.5f:F4}");
            }
            else if (distanceReduction < -0.01f) // Only log significant movements away
            {
                Debug.Log($"[NAVIGATOR] Moving away! Increased distance by {-distanceReduction:F3}, penalty: {distanceReduction * 0.5f:F4}");
            }
        }
        
        // Update distance tracking for next step
        previousDistanceToTarget = currentDistanceToTarget;
        
        // Check if target reached
        if (currentDistanceToTarget < targetReachDistance)
        {
            // Big reward for success
            float reward = 10.0f;
            AddReward(reward);
            Debug.Log($"[NAVIGATOR] TARGET REACHED! Distance: {currentDistanceToTarget:F2}, Reward: +{reward}");
            environmentManager.EndEpisode(true);
            return;
        }
        
        // Penalty for falling off the platform
        if (transform.position.y < -1f)
        {
            Debug.Log($"[NAVIGATOR] FELL OFF PLATFORM! Y position: {transform.position.y}");
            AddReward(-100.0f);
            environmentManager.EndEpisode(false);
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual control for testing
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
    }
}
