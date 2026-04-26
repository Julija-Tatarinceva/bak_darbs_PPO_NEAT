using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class Walker : Agent
{
    public int episodeStep = 0;
    public int maxEpisodeSteps = 30000; // Limit episodes to 1000 steps

    public HingeJoint leftLegHinge;
    public HingeJoint rightLegHinge;
    public Rigidbody bodyRigidbody;
    public float maxMotorSpeed = 1000f;

    // Target to reach (set this in Unity Inspector)
    public Transform target;

    private Vector3[] startPositions;
    private Quaternion[] startRotations;
    private Rigidbody[] rigidbodies;
    private float previousDistanceToTarget;
    
    void Start()
    {
        rigidbodies = GetComponentsInChildren<Rigidbody>();

        startPositions = new Vector3[rigidbodies.Length];
        startRotations = new Quaternion[rigidbodies.Length];

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            startPositions[i] = rigidbodies[i].transform.position;
            startRotations[i] = rigidbodies[i].transform.rotation;
        }
    }

    public override void OnEpisodeBegin()
    {
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.transform.position = startPositions[i];
            rb.transform.rotation = startRotations[i];

            rb.Sleep();
            rb.WakeUp();
        }

        // Reset episode counter
        episodeStep = 0;

        // Initialize distance tracking
        if (target != null)
        {
            previousDistanceToTarget = Vector3.Distance(bodyRigidbody.transform.position, target.position);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeStep++;
        
        // End episode if it goes too long
        // if (episodeStep >= maxEpisodeSteps)
        // {
        //     Debug.Log("Episode ended: Max steps reached");
        //     EndEpisode();
        //     return;
        // }

        // Control leg motors
        JointMotor leftMotor = leftLegHinge.motor;
        JointMotor rightMotor = rightLegHinge.motor;

        float leftSpeed = actions.ContinuousActions[0] * maxMotorSpeed;
        float rightSpeed = actions.ContinuousActions[1] * maxMotorSpeed;

        leftMotor.targetVelocity = leftSpeed;
        leftMotor.force = leftLegHinge.motor.force;
        leftLegHinge.motor = leftMotor;

        rightMotor.targetVelocity = rightSpeed;
        rightMotor.force = rightLegHinge.motor.force;
        rightLegHinge.motor = rightMotor;

        // Reward for getting closer to target
        if (target != null)
        {
            float currentDistanceToTarget = Vector3.Distance(bodyRigidbody.transform.position, target.position);
            
            // Reward for reducing distance to target
            float distanceReduction = previousDistanceToTarget - currentDistanceToTarget;
            AddReward(distanceReduction * 0.1f);
            
            // Small continuous reward for being close to target
            float proximityReward = (20f - currentDistanceToTarget) / 20f;
            AddReward(0.01f * Mathf.Max(0, proximityReward));
            
            previousDistanceToTarget = currentDistanceToTarget;

            // Big reward for reaching the target
            if (currentDistanceToTarget < 1.5f)
            {
                AddReward(10.0f);
                Debug.Log("Target reached!");
                EndEpisode();
                return;
            }
        }

        // Small penalty per step to encourage efficiency
        AddReward(-0.001f);

        // Penalty for not moving enough (prevents standing still)
        float horizontalSpeed = new Vector2(bodyRigidbody.linearVelocity.x, bodyRigidbody.linearVelocity.z).magnitude;
        if (horizontalSpeed < 0.1f) // If moving slower than 0.1 units/second
        {
            AddReward(-0.01f); // Penalty for being too static
        }
        else
        {
            AddReward(0.005f * Mathf.Min(horizontalSpeed / 2f, 1f)); // Reward for moving (capped)
        }

        // Reward for staying upright over time
        float uprightBonus = Mathf.Abs(bodyRigidbody.transform.up.y);
        AddReward(0.005f * uprightBonus); // Increased from 0.001f
        
        // Penalty for falling
        if (bodyRigidbody.transform.position.y < 0.3f){
            AddReward(-2f);
            Debug.Log("Walker fell!");
            EndEpisode();
            return;
        }

    // Penalty for excessive tilting (prevent falling strategy)
         float tiltAngle = Mathf.Abs(bodyRigidbody.transform.eulerAngles.z);
        
        if (tiltAngle > 180f) tiltAngle = 360f - tiltAngle;
        
        if (tiltAngle > 45f) // If tilted more than 45 degrees
        {
            AddReward(-0.01f * (tiltAngle / 45f)); // Progressive penalty for tilting
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Leg angles (normalized)
        float leftLegAngle = leftLegHinge.angle / leftLegHinge.limits.max;
        float rightLegAngle = rightLegHinge.angle / rightLegHinge.limits.max;

        // Leg angular velocities (normalized)
        float leftLegSpeed = leftLegHinge.velocity / maxMotorSpeed;
        float rightLegSpeed = rightLegHinge.velocity / maxMotorSpeed;

        // Body tilt (normalized)
        float bodyTilt = bodyRigidbody.transform.eulerAngles.z;
        if (bodyTilt > 180f) bodyTilt -= 360f;
        bodyTilt /= 180f;

        sensor.AddObservation(leftLegAngle);
        sensor.AddObservation(rightLegAngle);
        sensor.AddObservation(leftLegSpeed);
        sensor.AddObservation(rightLegSpeed);
        sensor.AddObservation(bodyTilt);

        // Add target direction and distance observations
        if (target != null)
        {
            Vector3 directionToTarget = (target.position - bodyRigidbody.transform.position).normalized;
            sensor.AddObservation(directionToTarget.x);
            sensor.AddObservation(directionToTarget.z);
            
            float distanceToTarget = Vector3.Distance(bodyRigidbody.transform.position, target.position);
            sensor.AddObservation(distanceToTarget / 20f); // Normalized distance
        }
        else
        {
            // If no target, add zeros
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Body velocity
        sensor.AddObservation(bodyRigidbody.linearVelocity.x / 5f);
        sensor.AddObservation(bodyRigidbody.linearVelocity.z / 5f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = 1f;  // постоянное движение левой ноги
        continuousActions[1] = 1f;  // постоянное движение правой ноги
        Debug.Log("Heuristic called");
    }
}
