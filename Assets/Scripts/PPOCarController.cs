using CarExperiment;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.UI;

public class PPOCarController : Agent
{
    public float Speed = 5f;
    public float TurnSpeed = 180f;
    public float SensorRange = 10f;

    private Rigidbody rb;
    private int currentPiece = 0;
    private int lastPiece = -1;
    private int wallHits = 0;
    private float episodeStartTime;
    private bool goalReached = false;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private TrackGenerator trackGenerator;
    
    // UI elements for piece display
    private Text pieceDisplay;
    private Canvas uiCanvas;
    
    private Vector3[] lastRayOrigins = new Vector3[5];
    private Vector3[] lastRayDirs = new Vector3[5];
    private float[] lastRayLengths = new float[5];
    
    private int episodeCount = 0;
    private int fixedUpdateCount = 0; // Added counter for FixedUpdate

    void Start()
    {
        Time.timeScale = 50f;
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        
        // Create UI for piece display
        CreatePieceDisplay();
    }

    public override void OnEpisodeBegin()
    {
        // Reset episode start time
        episodeStartTime = Time.time;

        // Log the episode data before resetting for the next episode
        Logger.LogEpisode(
            "PPO",
            episodeCount++,
            currentPiece,
            wallHits,
            (currentPiece-1) * 1f - wallHits * 0.2f,
            fixedUpdateCount
        );

        // Regenerate the track for PPO
        if (trackGenerator == null)
        {
            trackGenerator = FindObjectOfType<TrackGenerator>();
        }
        if (trackGenerator != null)
        {
            int seed = SeedManager.GetSeedForEpisode(episodeCount);
            trackGenerator.GenerateTrack(seed);
        }

        // Reset car position and state
        transform.position = startPosition; // Or starting position
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentPiece = 0;
        lastPiece = -1;
        wallHits = 0;
        goalReached = false;

        Debug.Log("PPO Episode started");
        
        // Update UI display
        UpdatePieceDisplay();
    }

    private void CreatePieceDisplay()
    {
        // Find or create canvas
        uiCanvas = FindObjectOfType<Canvas>();
        if (uiCanvas == null)
        {
            GameObject canvasGO = new GameObject("PieceDisplayCanvas");
            uiCanvas = canvasGO.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create text element for piece display
        GameObject textGO = new GameObject("PieceDisplayText");
        textGO.transform.SetParent(uiCanvas.transform, false);

        pieceDisplay = textGO.AddComponent<Text>();
        pieceDisplay.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pieceDisplay.fontSize = 40;
        pieceDisplay.fontStyle = FontStyle.Bold;
        pieceDisplay.alignment = TextAnchor.UpperRight;
        pieceDisplay.text = "Piece: 0";
        pieceDisplay.color = Color.white;

        // Add outline for better visibility
        Outline outline = textGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);

        // Set position to upper right corner
        RectTransform rectTransform = textGO.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(1, 1);
        rectTransform.anchoredPosition = new Vector2(-20, -20);
        rectTransform.sizeDelta = new Vector2(400, 100);
    }

    private void UpdatePieceDisplay()
    {
        if (pieceDisplay != null)
        {
            pieceDisplay.text = $"Piece: {currentPiece}";
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Same 5 sensors as NEAT version
        float frontSensor = 0;
        float leftFrontSensor = 0;
        float leftSensor = 0;
        float rightFrontSensor = 0;
        float rightSensor = 0;

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
        sensor.AddObservation(frontSensor);
        
        
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
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steer = actions.ContinuousActions[0];
        float gas = Mathf.Clamp01(actions.ContinuousActions[1]);

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
        //Debug.Log($"[COLLISION] With: {collision.collider.name}, Tag: {collision.collider.tag}");
        if (collision.collider.CompareTag("Road"))
        {
            RoadPiece rp = collision.collider.GetComponentInParent<RoadPiece>();
            //Debug.Log(rp == null ? "[COLLISION] No RoadPiece component found!" : $"[COLLISION] RoadPiece found, PieceNumber: {rp.PieceNumber}");
            if (rp != null && rp.PieceNumber != lastPiece && (rp.PieceNumber == currentPiece + 1 || (rp.PieceNumber == 0)))
            {
                lastPiece = currentPiece;
                currentPiece = rp.PieceNumber;
                AddReward(1f); // Reward for reaching next piece
                UpdatePieceDisplay(); // Update UI
                //Debug.Log($"PPO Piece reached: {CurrentPiece}");
            }
            else if (rp.PieceNumber > currentPiece && rp.PieceNumber != lastPiece)
            {
                // RECOVERY: Car skipped pieces - track furthest progress
                Debug.LogWarning($"[PPO] ⚠ SKIPPED from {currentPiece} to {rp.PieceNumber}");
                lastPiece = currentPiece;
                currentPiece = rp.PieceNumber;
                AddReward(1f);
                UpdatePieceDisplay();
            }
            else
            {
                Debug.Log($"[PPO] Ignored piece {rp.PieceNumber} (not in sequence)");
            }
            if (rp != null && rp.PieceNumber == 0 && currentPiece > 0)
            {
                currentPiece = 0;
                UpdatePieceDisplay(); // Update UI
            }
        }
        else if (collision.collider.CompareTag("Wall"))
        {
            wallHits++;
            AddReward(-0.2f); // Penalty for wall hit
        }
    }

    void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"[TRIGGER] With: {other.name}, Tag: {other.tag}");
        if (other.CompareTag("Road"))
        {
            RoadPiece rp = other.GetComponentInParent<RoadPiece>();
            //Debug.Log(rp == null ? "[TRIGGER] No RoadPiece component found!" : $"[TRIGGER] RoadPiece found, PieceNumber: {rp.PieceNumber}");
            if (rp != null && rp.PieceNumber != lastPiece && (rp.PieceNumber == currentPiece + 1 || (rp.PieceNumber == 0)))
            {
                lastPiece = currentPiece;
                currentPiece = rp.PieceNumber;
                AddReward(1f); // Reward for reaching next piece
                UpdatePieceDisplay(); // Update UI
                //Debug.Log($"PPO Piece reached: {CurrentPiece}");
            }
            if (rp != null && rp.PieceNumber == 0 && currentPiece > 0)
            {
                currentPiece = 0;
                UpdatePieceDisplay(); // Update UI
            }
        }
        else if (other.CompareTag("Wall"))
        {
            wallHits++;
            AddReward(-0.2f); // Penalty for wall hit
        }
        else if (other.CompareTag("Goal") && !goalReached)
        {
            goalReached = true;
            float timeTaken = Time.time - episodeStartTime;
            float speedBonus = 100f / Mathf.Max(timeTaken, 0.1f);
            AddReward(speedBonus + 10f); // Big reward for goal + speed bonus

            // Debug log for goal reached (conditional to avoid flooding)
            if (episodeCount % 100 == 0) // Log every 100 episodes
            {
                Debug.Log($"PPO Goal reached! Time: {timeTaken:F2}s, Reward: {speedBonus + 10f:F2}");
            }

            EndEpisode();
        }
    }

    void FixedUpdate()
    {
        fixedUpdateCount++; // Increment counter on each FixedUpdate

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
    }
}
