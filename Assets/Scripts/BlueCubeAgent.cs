using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BlueCubeAgent : Agent
{
    public GameObject yellowOrb;
    public GameObject greenCube;
    public Transform finishLine;

    private Rigidbody rb;
    private int episodeStep = 0;
    private int maxEpisodeSteps = 1000; // Limit episodes to 1000 steps

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        // Reset carrying state (detach orb before resetting position)
        yellowOrb.transform.SetParent(null);

        // Reset positions
        transform.localPosition = new Vector3(-4, 0.5f, 0);
        yellowOrb.transform.localPosition = new Vector3(0, 0.5f, 0);
        greenCube.transform.localPosition = new Vector3(4, 0.5f, 0);

        Debug.Log("Episode Begin - Agent reset to position: " + transform.localPosition);

        // FIX: Reset physics to prevent "teleporting" away with old momentum
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset episode counter
        episodeStep = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add observations
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(yellowOrb.transform.localPosition);
        sensor.AddObservation(greenCube.transform.localPosition);
        sensor.AddObservation(finishLine.localPosition);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Increment step counter
        episodeStep++;
        //Debug.Log("Episode step: " + episodeStep);
        
        // End episode if it goes too long
        if (episodeStep >= maxEpisodeSteps)
        {
            Debug.Log("Episode ended: Max steps reached");
            EndEpisode();
            return;
        }

        // Actions: Move the blue cube
        Vector3 move = new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]);
        rb.AddForce(move * 50f);

        Debug.Log($"Step {episodeStep}: Action {move}, Position {transform.localPosition}");
        
        // FIX: End episode if we fall off the platform
        if (transform.localPosition.y < -1f)
        {
            Debug.Log("Episode ended: Fell off platform");
            EndEpisode();
            return;
        }

        // Distance-based rewards to guide the agent
        float distanceToOrb = Vector3.Distance(transform.localPosition, yellowOrb.transform.localPosition);
        float distanceToGreenCube = Vector3.Distance(transform.localPosition, greenCube.transform.localPosition);

        // Small penalty per step to encourage efficiency
        AddReward(-0.001f);

        // If not carrying orb, reward getting closer to orb
        if (yellowOrb.transform.parent != transform)
        {
            // Stronger reward for being close to orb
            float orbProximityReward = (10f - distanceToOrb) / 10f; // Closer = higher reward
            AddReward(0.01f * orbProximityReward);
            
            // Big reward for actually picking up the orb
            if (distanceToOrb < 1.5f)
            {
                yellowOrb.transform.SetParent(transform);
                yellowOrb.transform.localPosition = new Vector3(0, 1f, 0);
                AddReward(5.0f); // Increased from 1.0
                Debug.Log("Orb picked up! Reward granted.");
            }
        }
        else
        {
            // If carrying orb, reward getting closer to green cube
            float cubeProximityReward = (10f - distanceToGreenCube) / 10f;
            AddReward(0.01f * cubeProximityReward);
            
            // Big reward for delivering the orb to the green cube
            if (distanceToGreenCube < 1.5f)
            {
                AddReward(10.0f); // Increased from 2.0
                Debug.Log("Orb delivered! Episode complete!");
                EndEpisode(); // CRITICAL: End the episode once successful
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // FIX: Correct syntax for manual control
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
    }
}
