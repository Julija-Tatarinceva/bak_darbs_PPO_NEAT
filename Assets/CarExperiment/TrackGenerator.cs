using UnityEngine;
using System.Collections.Generic;

namespace CarExperiment
{
    public class TrackGenerator : MonoBehaviour
    {
        [Header("Track Prefabs")] [Tooltip("Straight road piece prefab")]
        public GameObject straightPiece;

        [Tooltip("45-degree turn piece prefab")]
        public GameObject turn45Piece;

        [Tooltip("Goal/checkpoint piece prefab")]
        public GameObject goalPiece;

        [Header("Track Configuration")] [Tooltip("Number of road pieces in the track")]
        public int trackLength = 20;

        [Tooltip("Maximum attempts to generate a valid non-intersecting track before giving up")]
        public int maxGenerationAttempts = 10;

        [Tooltip("Difficulty level (0-1): 0=easy (mostly straight), 1=hard (many turns)")] [Range(0f, 1f)]
        public float difficulty = 0.5f;

        [Tooltip("Probability of 90-degree turn vs 45-degree turn at high difficulty")] [Range(0f, 1f)]
        public float sharpTurnProbability = 0.3f;

        [Tooltip("Random seed for reproducible tracks (0 = random seed)")]
        public int seed = 0;

        [Header("Curriculum Learning")] [Tooltip("Gradually increase difficulty over generations/episodes")]
        public bool useCurriculum = false;

        [Tooltip("Starting difficulty for curriculum")] [Range(0f, 1f)]
        public float startDifficulty = 0.1f;

        [Tooltip("Episodes/generations to reach max difficulty")]
        public int curriculumSteps = 100;

        [Header("Track Appearance")] [Tooltip("Width of each road piece")]
        public float pieceLength = 2f;

        [Tooltip("Thickness of the wall (X scale contribution)")]
        public float wallThickness = 0.05f;

        [Header("Track Walls")] [Tooltip("Wall prefab to instantiate along the track")]
        public GameObject wallPrefab;

        [Tooltip("Distance from road center to wall (half road width + half wall width)")]
        public float wallOffset = 2.7f;

        [Header("Wall Trimming")]
        [Tooltip("Inset amount to trim the right wall before a right turn (in meters)")]
        public float wallEndInset = 2f;

        private Vector3 lastWallEndL = Vector3.zero;
        private Vector3 lastWallEndR = Vector3.zero;
        private Quaternion lastPieceRot = Quaternion.identity;

        // Track data
        private List<GameObject> currentTrackPieces;
        private List<GameObject> currentWallPieces;
        private Vector3 currentPosition;
        private Quaternion currentRotation;
        private int generationCount = 0;

        // Track statistics (for analysis)
        public struct TrackStats
        {
            public int StraightCount;
            public int Turn45Count;
            public float TotalTurnAngle;
            public float ActualDifficulty;
        }

        private struct PieceInfo
        {
            public PieceType Type;
            public Vector3 Pos;
            public Quaternion Rot;
        }

        private TrackStats lastTrackStats;

        void Start()
        {
            currentTrackPieces = new List<GameObject>();
            currentWallPieces = new List<GameObject>();
            GenerateTrack(Random.Range(0, 1000000));;
        }
        public void GenerateTrack(int externalSeed)
        {
            // Force deterministic generation
            Random.InitState(externalSeed);

            GenerateNewTrackWithSeed();
        }
        
        public void GenerateNewTrackWithSeed()
        {

            ClearTrack();
            generationCount++;

            List<TrackGenerator.PieceInfo> pieces = new List<TrackGenerator.PieceInfo>();
            bool generationSuccessful = false;
            int attempts = 0;

            while (!generationSuccessful && attempts < maxGenerationAttempts)
            {
                attempts++;
                pieces.Clear();
                Vector3 tempPos = transform.position;
                Quaternion tempRot = transform.rotation;
                PieceType nextType = PieceType.Straight;
                bool foundIntersectionInThisAttempt = false;

                for (int i = 0; i < trackLength; i++)
                {
                    if (i == trackLength - 1)
                    {
                        pieces.Add(new TrackGenerator.PieceInfo { Type = PieceType.Straight, Pos = tempPos, Rot = tempRot });
                    }
                    else
                    {
                        // Intersection check and piece selection
                        PieceType selected = nextType;
                        // Try types in this order: desired, straight, left, right
                        PieceType[] attemptTypes = { nextType, PieceType.Straight, PieceType.Turn45Left, PieceType.Turn45Right, PieceType.Turn90Left, PieceType.Turn90Right };
                        bool foundValidPiece = false;

                        foreach (PieceType attempt in attemptTypes)
                        {
                            Vector3 nextPos = GetNextPosition(tempPos, tempRot, attempt);
                            bool intersects = false;

                            // Start checking against previous pieces (skip the immediate previous one)
                            for (int j = 0; j < pieces.Count - 1; j++)
                            {
                                if (Vector3.Distance(nextPos, pieces[j].Pos) < pieceLength * 1.5f)
                                {
                                    intersects = true;
                                    break;
                                }
                            }

                            if (!intersects)
                            {
                                selected = attempt;
                                foundValidPiece = true;
                                break;
                            }
                        }

                        if (!foundValidPiece)
                        {
                            foundIntersectionInThisAttempt = true;
                            break; // Fail this entire track attempt
                        }

                        pieces.Add(new TrackGenerator.PieceInfo { Type = selected, Pos = tempPos, Rot = tempRot });
                        tempPos = GetNextPosition(tempPos, tempRot, selected);
                        tempRot = GetNextRotation(tempRot, selected);
                        nextType = SelectPieceType(CalculateCurrentDifficulty());
                    }
                }

                if (!foundIntersectionInThisAttempt)
                {
                    generationSuccessful = true;
                }
            }
            
            lastWallEndL = Vector3.zero;
            lastWallEndR = Vector3.zero;

            TrackStats stats = new TrackStats();
            for (int i = 0; i < pieces.Count; i++)
            {
                TrackGenerator.PieceInfo info = pieces[i];
                GameObject piece;

                if (i == trackLength - 1)
                {
                    piece = Instantiate(goalPiece, info.Pos, info.Rot, transform);
                    piece.name = $"Goal_Piece_{i}";
                    var teleport = piece.AddComponent<GoalTeleport>();
                }
                else
                {
                    switch (info.Type)
                    {
                        case PieceType.Straight:
                            piece = Instantiate(straightPiece, info.Pos, info.Rot, transform);
                            piece.name = $"Straight_Piece_{i}";
                            stats.StraightCount++;
                            break;
                        case PieceType.Turn45Left:
                            piece = Instantiate(turn45Piece, info.Pos, info.Rot, transform);
                            piece.name = $"Turn45L_Piece_{i}";
                            stats.Turn45Count++;
                            stats.TotalTurnAngle += 45f;
                            break;
                        case PieceType.Turn45Right:
                            piece = Instantiate(turn45Piece, info.Pos, info.Rot, transform);
                            piece.name = $"Turn45R_Piece_{i}";
                            stats.Turn45Count++;
                            stats.TotalTurnAngle += 45f;
                            break;
                        case PieceType.Turn90Left:
                            piece = Instantiate(turn45Piece, info.Pos, info.Rot, transform);
                            piece.name = $"Turn90L_Piece_{i}";
                            stats.Turn45Count++;
                            stats.TotalTurnAngle += 90f;
                            break;
                        case PieceType.Turn90Right:
                            piece = Instantiate(turn45Piece, info.Pos, info.Rot, transform);
                            piece.name = $"Turn90R_Piece_{i}";
                            stats.Turn45Count++;
                            stats.TotalTurnAngle += 90f;
                            break;
                        default:
                            piece = Instantiate(straightPiece, info.Pos, info.Rot, transform);
                            piece.name = $"Default_Piece_{i}";
                            stats.StraightCount++;
                            break;
                    }
                }

                currentTrackPieces.Add(piece);

                // Set PieceNumber for road pieces
                if (i < trackLength - 1)
                {
                    RoadPiece rp = piece.GetComponentInChildren<RoadPiece>();
                    if (rp != null)
                    {
                        rp.PieceNumber = i;
                    }
                }

                if (wallPrefab != null && i < pieces.Count - 1)
                {
                    // Start from where the previous wall ended (or compute fresh for piece 0)
                    Vector3 wallStartL = (lastWallEndL != Vector3.zero)
                        ? lastWallEndL
                        : info.Pos + info.Rot * (Vector3.left * wallOffset);

                    Vector3 wallStartR = (lastWallEndR != Vector3.zero)
                        ? lastWallEndR
                        : info.Pos + info.Rot * (Vector3.right * wallOffset);

                    Quaternion exitRot = GetNextRotation(info.Rot, info.Type);
                    Vector3 exitPos = GetNextPosition(info.Pos, info.Rot, info.Type);

                    Vector3 wallEndL = exitPos + exitRot * (Vector3.left * wallOffset);
                    Vector3 wallEndR = exitPos + exitRot * (Vector3.right * wallOffset);

                    if (pieces[i + 1].Type == PieceType.Turn45Left)
                        wallEndL = wallEndL - exitRot * (Vector3.forward * wallEndInset);

                    if (pieces[i + 1].Type == PieceType.Turn45Right)
                        wallEndR = wallEndR - exitRot * (Vector3.forward * wallEndInset);

                    if (pieces[i + 1].Type == PieceType.Turn90Left)
                        wallEndL = wallEndL - exitRot * (Vector3.forward * wallEndInset * 2f);

                    if (pieces[i + 1].Type == PieceType.Turn90Right)
                        wallEndR = wallEndR - exitRot * (Vector3.forward * wallEndInset * 2f);

                    lastWallEndL = wallEndL;
                    lastWallEndR = wallEndR;

                    PlaceWall(wallStartL, wallEndL, $"WallL_{i}", wallThickness);
                    PlaceWall(wallStartR, wallEndR, $"WallR_{i}", wallThickness);

                    // After placing walls, store current piece's rotation for next iteration
                    lastPieceRot = info.Rot;
                    if (i == 0)
                    {
                        Vector3 backL = info.Pos + info.Rot * (Vector3.left * wallOffset);
                        Vector3 backR = info.Pos + info.Rot * (Vector3.right * wallOffset);
                        PlaceWall(backL, backR, "BackWall", wallThickness);
                    }
                }
            }
        }

        private Vector3 GetNextPosition(Vector3 startPos, Quaternion startRot, PieceType type)
        {
            Vector3 pos = startPos;
            Quaternion rot = startRot;
            if (type == PieceType.Turn45Left)
            {
                rot *= Quaternion.Euler(0, -45, 0);
                pos += rot * (-Vector3.right * 0.78641f + -Vector3.forward * 1.66428f);
            }
            else if (type == PieceType.Turn45Right)
            {
                rot *= Quaternion.Euler(0, 45, 0);
                pos += rot * (Vector3.right * 0.78641f + -Vector3.forward * 1.66428f);
            }
            else if (type == PieceType.Turn90Left)
            {
                rot *= Quaternion.Euler(0, -90, 0);
                // For 90-degree turns, the offset is purely lateral and forward
                pos += rot * (-Vector3.right * 2f + -Vector3.forward * 2f);
            }
            else if (type == PieceType.Turn90Right)
            {
                rot *= Quaternion.Euler(0, 90, 0);
                // For 90-degree turns, the offset is purely lateral and forward
                pos += rot * (Vector3.right * 2f + -Vector3.forward * 2f);
            }
            pos += rot * Vector3.forward * pieceLength;
            return pos;
        }

        private Quaternion GetNextRotation(Quaternion startRot, PieceType type)
        {
            Quaternion rot = startRot;
            if (type == PieceType.Turn45Left)
            {
                rot *= Quaternion.Euler(0, -45, 0);
            }
            else if (type == PieceType.Turn45Right)
            {
                rot *= Quaternion.Euler(0, 45, 0);
            }
            else if (type == PieceType.Turn90Left)
            {
                rot *= Quaternion.Euler(0, -90, 0);
            }
            else if (type == PieceType.Turn90Right)
            {
                rot *= Quaternion.Euler(0, 90, 0);
            }
            return rot;
        }
        
        private PieceType SelectPieceType(float currentDifficulty)
        {
            float roll = Random.value;
            
            if (roll > currentDifficulty)
            {
                return PieceType.Straight;
            }
            bool useSharpTurn = Random.value < sharpTurnProbability;
            bool turnLeft = Random.value < 0.5f;
                
            if (useSharpTurn)
            {
                return turnLeft ? PieceType.Turn90Left : PieceType.Turn90Right;
            }
            return turnLeft ? PieceType.Turn45Left : PieceType.Turn45Right;
        }
        
        private float CalculateCurrentDifficulty()
        {
            if (!useCurriculum)
            {
                return difficulty;
            }
            
            float progress = Mathf.Min(generationCount / (float)curriculumSteps, 1f);
            return Mathf.Lerp(startDifficulty, difficulty, progress);
        }
        
        private void ClearTrack()
        {
            foreach (GameObject piece in currentTrackPieces)
            {
                Destroy(piece);
            }

            currentTrackPieces.Clear();
            foreach (GameObject wall in currentWallPieces)
            {
                Destroy(wall);
            }

            currentWallPieces.Clear();
        }
        
        public TrackStats GetLastTrackStats()
        {
            return lastTrackStats;
        }
        
        public int GetGenerationCount()
        {
            return generationCount;
        }
        
        public void ResetGenerationCount()
        {
            generationCount = 0;
        }

        // void OnDrawGizmos()
        // {
        //     // Visualize track in editor
        //     if (currentTrackPieces.Count > 0)
        //     {
        //         Gizmos.color = Color.cyan;
        //         for (int i = 0; i < currentTrackPieces.Count - 1; i++)
        //         {
        //             Gizmos.DrawLine(
        //                 currentTrackPieces[i].transform.position + Vector3.up * 0.5f,
        //                 currentTrackPieces[i + 1].transform.position + Vector3.up * 0.5f
        //             );
        //         }
        //     }
        // }

        private void PlaceWall(Vector3 start, Vector3 end, string wallName, float thickness)
        {
            Vector3 diff = end - start;
            //Debug.Log(end + " - " + start + " = " + diff);
            float dist = diff.magnitude;
            if (dist < 0.001f) return;

            Vector3 wallPos = start + diff * 0.5f;
            Quaternion wallRot = Quaternion.LookRotation(diff);

            GameObject wall = Instantiate(wallPrefab, wallPos, wallRot, transform);
            wall.name = wallName;

            // Clean scale: thickness (X), height (Y), and distance (Z)
            // Use 1.0f for Y to preserve prefab height relative to its transform
            wall.transform.localScale = new Vector3(thickness, wall.transform.localScale.y, dist);

            currentWallPieces.Add(wall);
        }

        private enum PieceType
        {
            Straight,
            Turn45Left,
            Turn45Right,
            Turn90Left,
            Turn90Right
        }
    }
}
