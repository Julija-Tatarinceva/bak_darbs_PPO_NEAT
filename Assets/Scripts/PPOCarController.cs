using CarExperiment;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class PPOCarController : Agent
{
    public float Speed = 5f;
    public float TurnSpeed = 180f;
    public float SensorRange = 10f;

    private Rigidbody rb;
    private int CurrentPiece = 0;
    private int LastPiece = -1;
    private int WallHits = 0;
    private float episodeStartTime;
    private bool goalReached = false;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private TrackGenerator trackGenerator;

    // --- Add these member variables ---
    private Vector3[] lastRayOrigins = new Vector3[5];
    private Vector3[] lastRayDirs = new Vector3[5];
    private float[] lastRayLengths = new float[5];

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;

        // Diagnostic: Print all colliders under this car
        Collider[] carColliders = GetComponentsInChildren<Collider>();
        Debug.Log($"[CAR] Colliders attached to car: {carColliders.Length}");
        foreach (var c in carColliders)
        {
            Debug.Log($"[CAR] Collider: {c.name}, isTrigger={c.isTrigger}, enabled={c.enabled}, tag={c.tag}, layer={c.gameObject.layer}");
        }

        // Diagnostic: Print all colliders under the first road piece found
        var road = GameObject.FindGameObjectWithTag("Road");
        if (road != null)
        {
            Collider[] roadColliders = road.GetComponentsInChildren<Collider>();
            Debug.Log($"[ROAD] Colliders attached to road: {roadColliders.Length}");
            foreach (var c in roadColliders)
            {
                Debug.Log($"[ROAD] Collider: {c.name}, isTrigger={c.isTrigger}, enabled={c.enabled}, tag={c.tag}, layer={c.gameObject.layer}");
            }
        }
        else
        {
            Debug.Log("[ROAD] No GameObject with tag 'Road' found in scene!");
        }
    }

    public override void OnEpisodeBegin()
    {
        // Regenerate the track for PPO
        if (trackGenerator == null)
        {
            trackGenerator = FindObjectOfType<TrackGenerator>();
        }
        if (trackGenerator != null)
        {
            trackGenerator.GenerateNewTrack();
        }

        // Reset car position and state
        transform.position = startPosition; // Or starting position
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        CurrentPiece = 0;
        LastPiece = -1;
        WallHits = 0;
        goalReached = false;
        episodeStartTime = Time.time;

        Debug.Log("PPO Episode started");

    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Same 5 sensors as NEAT version
        float frontSensor = 0;
        float leftFrontSensor = 0;
        float leftSensor = 0;
        float rightFrontSensor = 0;
        float rightSensor = 0;
        float rightSensorNew = 0; // New sensor for PPO

        RaycastHit hit;
        Vector3 origin = transform.position + transform.forward * 1.1f;

        // Front sensor
        Vector3 dirFront = transform.TransformDirection(Vector3.forward);
        float frontRayLength = SensorRange;
        if (Physics.Raycast(origin, dirFront, out hit, SensorRange))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                frontSensor = 1 - hit.distance / SensorRange;
            }
            frontRayLength = hit.distance;
        }
        lastRayOrigins[0] = origin;
        lastRayDirs[0] = dirFront;
        lastRayLengths[0] = frontRayLength;

        // Right-front sensor
        Vector3 dirRightFront = transform.TransformDirection(new Vector3(0.5f, 0, 1).normalized);
        float rightFrontRayLength = SensorRange;
        if (Physics.Raycast(origin, dirRightFront, out hit, SensorRange))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                rightFrontSensor = 1 - hit.distance / SensorRange;
            }
            rightFrontRayLength = hit.distance;
        }
        lastRayOrigins[1] = origin;
        lastRayDirs[1] = dirRightFront;
        lastRayLengths[1] = rightFrontRayLength;

        // Right sensor
        Vector3 dirRight = transform.TransformDirection(Vector3.right);
        float rightRayLength = SensorRange;
        if (Physics.Raycast(origin, dirRight, out hit, SensorRange))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                rightSensor = 1 - hit.distance / SensorRange;
            }
            rightRayLength = hit.distance;
        }
        lastRayOrigins[2] = origin;
        lastRayDirs[2] = dirRight;
        lastRayLengths[2] = rightRayLength;

        // Left-front sensor
        Vector3 dirLeftFront = transform.TransformDirection(new Vector3(-0.5f, 0, 1).normalized);
        float leftFrontRayLength = SensorRange;
        if (Physics.Raycast(origin, dirLeftFront, out hit, SensorRange))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                leftFrontSensor = 1 - hit.distance / SensorRange;
            }
            leftFrontRayLength = hit.distance;
        }
        lastRayOrigins[3] = origin;
        lastRayDirs[3] = dirLeftFront;
        lastRayLengths[3] = leftFrontRayLength;

        // Left sensor
        Vector3 dirLeft = transform.TransformDirection(Vector3.left);
        float leftRayLength = SensorRange;
        if (Physics.Raycast(origin, dirLeft, out hit, SensorRange))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                leftSensor = 1 - hit.distance / SensorRange;
            }
            leftRayLength = hit.distance;
        }
        lastRayOrigins[4] = origin;
        lastRayDirs[4] = dirLeft;
        lastRayLengths[4] = leftRayLength;

        sensor.AddObservation(frontSensor);
        sensor.AddObservation(leftFrontSensor);
        sensor.AddObservation(leftSensor);
        sensor.AddObservation(rightFrontSensor);
        sensor.AddObservation(rightSensor);
        sensor.AddObservation(rightSensorNew);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steer = actions.ContinuousActions[0] * 2 - 1; // -1 to 1
        float gas = actions.ContinuousActions[1];   // 0 to 1

        float moveDist = gas * Speed * Time.deltaTime;
        float turnAngle = steer * TurnSpeed * Time.deltaTime * gas;

        transform.Rotate(Vector3.up, turnAngle);
        transform.Translate(Vector3.forward * moveDist);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal"); // Steer
        continuousActionsOut[1] = Input.GetAxis("Vertical");   // Gas
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[COLLISION] With: {collision.collider.name}, Tag: {collision.collider.tag}");
        if (collision.collider.CompareTag("Road"))
        {
            RoadPiece rp = collision.collider.GetComponentInParent<RoadPiece>();
            Debug.Log(rp == null ? "[COLLISION] No RoadPiece component found!" : $"[COLLISION] RoadPiece found, PieceNumber: {rp.PieceNumber}");
            if (rp != null && rp.PieceNumber != LastPiece && (rp.PieceNumber == CurrentPiece + 1 || (rp.PieceNumber == 0)))
            {
                LastPiece = CurrentPiece;
                CurrentPiece = rp.PieceNumber;
                AddReward(1f); // Reward for reaching next piece
                Debug.Log($"PPO Piece reached: {CurrentPiece}");
            }
            if (rp != null && rp.PieceNumber == 0 && CurrentPiece > 0)
            {
                CurrentPiece = 0;
            }
        }
        else if (collision.collider.CompareTag("Wall"))
        {
            WallHits++;
            AddReward(-0.2f); // Penalty for wall hit
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TRIGGER] With: {other.name}, Tag: {other.tag}");
        if (other.CompareTag("Road"))
        {
            RoadPiece rp = other.GetComponentInParent<RoadPiece>();
            Debug.Log(rp == null ? "[TRIGGER] No RoadPiece component found!" : $"[TRIGGER] RoadPiece found, PieceNumber: {rp.PieceNumber}");
            if (rp != null && rp.PieceNumber != LastPiece && (rp.PieceNumber == CurrentPiece + 1 || (rp.PieceNumber == 0)))
            {
                LastPiece = CurrentPiece;
                CurrentPiece = rp.PieceNumber;
                AddReward(1f); // Reward for reaching next piece
                Debug.Log($"PPO Piece reached: {CurrentPiece}");
            }
            if (rp != null && rp.PieceNumber == 0 && CurrentPiece > 0)
            {
                CurrentPiece = 0;
            }
        }
        else if (other.CompareTag("Wall"))
        {
            WallHits++;
            AddReward(-0.2f); // Penalty for wall hit
        }
        else if (other.CompareTag("Goal") && !goalReached)
        {
            goalReached = true;
            float timeTaken = Time.time - episodeStartTime;
            float speedBonus = 100f / Mathf.Max(timeTaken, 0.1f);
            AddReward(speedBonus + 10f); // Big reward for goal + speed bonus
            Debug.Log($"PPO Goal reached! Time: {timeTaken:F2}s, Reward: {speedBonus + 10f:F2}");
            EndEpisode();
        }
    }

    void OnCollisionStay(Collision collision)
    {
        Debug.Log($"[COLLISION STAY] With: {collision.collider.name}, Tag: {collision.collider.tag}, isTrigger={collision.collider.isTrigger}");
    }

    void OnTriggerStay(Collider other)
    {
        Debug.Log($"[TRIGGER STAY] With: {other.name}, Tag: {other.tag}, isTrigger={other.isTrigger}");
    }

    void FixedUpdate()
    {
        // Draw rays for visualization (only 5 per car per frame, always in the same directions)
        Color[] rayColors = { Color.red, Color.yellow, Color.green, Color.cyan, Color.blue };
        Vector3 origin = transform.position + transform.forward * 1.1f;
        Vector3[] dirs = new Vector3[] {
            transform.TransformDirection(Vector3.forward),
            transform.TransformDirection(new Vector3(0.5f, 0, 1).normalized),
            transform.TransformDirection(Vector3.right),
            transform.TransformDirection(new Vector3(-0.5f, 0, 1).normalized),
            transform.TransformDirection(Vector3.left)
        };
        for (int i = 0; i < 5; i++)
        {
            Debug.DrawRay(origin, dirs[i] * SensorRange, rayColors[i], 0f);
        }

        // Reward for forward progress
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        AddReward(forwardSpeed * 0.001f);

        // Small negative reward each step (time penalty)
        AddReward(-0.001f);

        // Penalty for spinning in place
        AddReward(-Mathf.Abs(rb.angularVelocity.y) * 0.001f);

        // Optional: Check for timeout
        if (Time.time - episodeStartTime > 30f) // 30 seconds timeout
        {
            // For TensorBoard: Only reward and episode length are logged by default.
            // If using ML-Agents >=2.0.0, you can use AddEpisodeStat("PiecesPassed", CurrentPiece);
            // AddEpisodeStat("WallsHit", WallHits);
            AddReward(CurrentPiece - WallHits * 0.2f); // Final reward based on progress
            Debug.Log($"PPO Timeout! Pieces: {CurrentPiece}, WallHits: {WallHits}, Final Reward: {CurrentPiece - WallHits * 0.2f:F2}");
            EndEpisode();
        }
    }
}
