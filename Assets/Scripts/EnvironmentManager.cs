using UnityEngine;

/// <summary>
/// Manages the multi-agent communication environment.
/// Handles episode initialization, target spawning, and episode termination.
/// </summary>
public class EnvironmentManager : MonoBehaviour
{
    [Header("Agent References")]
    public ObserverAgent observerAgent;
    public NavigatorAgent navigatorAgent;
    
    [Header("Environment Objects")]
    public Transform target;
    public Transform platform;
    
    [Header("Spawn Settings")]
    [Tooltip("Radius within which to spawn the target")]
    public float spawnRadius = 8f;
    [Tooltip("Fixed Y position for target")]
    public float targetHeight = 0.5f;
    
    [Header("Agent Spawn Positions")]
    public Vector3 observerStartPosition = new Vector3(-5f, 0.5f, 0f);
    public Vector3 navigatorStartPosition = new Vector3(5f, 0.5f, 0f);
    
    private CommunicationManager communicationManager;
    private int episodeCount = 0;
    private int successCount = 0;
    
    void Start()
    {
        communicationManager = GetComponent<CommunicationManager>();
        
        // Initialize agents if not set
        if (observerAgent == null)
        {
            observerAgent = GetComponentInChildren<ObserverAgent>();
        }
        if (navigatorAgent == null)
        {
            navigatorAgent = GetComponentInChildren<NavigatorAgent>();
        }
        
        // Set initial references
        observerAgent.target = target;
        navigatorAgent.target = target;
        observerAgent.environmentManager = this;
        navigatorAgent.environmentManager = this;
        observerAgent.communicationManager = communicationManager;
        navigatorAgent.communicationManager = communicationManager;
        
        // Start the first episode
        ResetEnvironment();
    }
    
    /// <summary>
    /// Called at the start of each episode to reset the environment.
    /// </summary>
    public void ResetEnvironment()
    {
        episodeCount++;
        
        Debug.Log($"========== EPISODE {episodeCount} START ==========");
        
        // Reset agent positions
        observerAgent.transform.localPosition = observerStartPosition;
        navigatorAgent.transform.localPosition = navigatorStartPosition;
        
        // Reset navigator physics completely
        if (navigatorAgent.rb != null)
        {
            navigatorAgent.rb.linearVelocity = Vector3.zero;
            navigatorAgent.rb.angularVelocity = Vector3.zero;
        }
        
        // DON'T spawn target here anymore - only spawn on success or first episode
        if (episodeCount == 1)
        {
            SpawnTarget(); // Only spawn on the very first episode
        }
        
        // Reset communication channel
        if (communicationManager != null)
        {
            communicationManager.ResetCommunication();
        }
        
        // Reset both agents
        observerAgent.OnEpisodeBegin();
        navigatorAgent.OnEpisodeBegin();
        
        Vector3 navToTarget = target.position - navigatorAgent.transform.position;
        Debug.Log($"[ENV] Episode {episodeCount} started.");
        Debug.Log($"[ENV] Target at: {target.position}");
        Debug.Log($"[ENV] Observer at: {observerAgent.transform.position}");
        Debug.Log($"[ENV] Navigator at: {navigatorAgent.transform.position}");
        Debug.Log($"[ENV] Distance Navigator→Target: {Vector3.Distance(navigatorAgent.transform.position, target.position):F2}");
        Debug.Log($"[ENV] Direction Navigator→Target: ({navToTarget.x:F2}, {navToTarget.z:F2})");
        Debug.Log($"==========================================");
    }
    
    /// <summary>
    /// Spawns the target at a random position within the spawn radius.
    /// </summary>
    void SpawnTarget()
    {
        // Random position in a circle
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 newPosition = new Vector3(randomCircle.x, targetHeight, randomCircle.y);
        
        // Set target position (local to platform if it exists)
        if (platform != null)
        {
            target.localPosition = newPosition;
        }
        else
        {
            target.position = newPosition;
        }
    }
    
    /// <summary>
    /// Called when the episode ends (success or failure).
    /// </summary>
    public void EndEpisode(bool success)
    {
        Debug.Log($"========== EPISODE {episodeCount} END ==========");
        
        if (success)
        {
            successCount++;
            observerAgent.OnNavigatorSuccess();
            float successRate = (successCount / (float)episodeCount) * 100f;
            Debug.Log($"[ENV] Episode {episodeCount} SUCCESS!");
            Debug.Log($"[ENV] Success rate: {successRate:F1}% ({successCount}/{episodeCount})");
            
            // ONLY spawn new target on success!
            SpawnTarget();
            Debug.Log($"[ENV] New target spawned at: {target.position}");
        }
        else
        {
            observerAgent.OnNavigatorFailed();
            Debug.Log($"[ENV] Episode {episodeCount} FAILED.");
            Debug.Log($"[ENV] Target remains at same position: {target.position}");
        }
        
        Debug.Log($"[ENV] Final Navigator position: {navigatorAgent.transform.position}");
        Debug.Log($"[ENV] Final distance: {Vector3.Distance(navigatorAgent.transform.position, target.position):F2}");
        Debug.Log($"==========================================");
        
        // End episode for both agents
        observerAgent.EndEpisode();
        navigatorAgent.EndEpisode();
        
        // IMPORTANT: Automatically start next episode after a brief delay
        Invoke("ResetEnvironment", 0.1f);
    }
    
    /// <summary>
    /// Visualize episode statistics.
    /// </summary>
    void OnGUI()
    {
        if (Application.isPlaying)
        {
            float successRate = episodeCount > 0 ? (successCount / (float)episodeCount) * 100f : 0f;
            GUI.Box(new Rect(10, 230, 200, 80), 
                $"Episode Statistics\n" +
                $"Episodes: {episodeCount}\n" +
                $"Successes: {successCount}\n" +
                $"Success Rate: {successRate:F1}%");
        }
    }
}
