using System.Collections.Generic;
using UnityEngine;

public static class SeedManager
{
    public static List<int> Seeds = new List<int>();
    private static int index = 0;

    public static void Initialize(int count, int baseSeed = 12345)
    {
        Seeds.Clear();
        Random.InitState(baseSeed);

        for (int i = 0; i < count; i++)
        {
            Seeds.Add(Random.Range(0, 1000000));
        }

        index = 0;
    }

    public static int GetNextSeed()
    {
        if (index >= Seeds.Count)
            index = 0;

        return Seeds[index++];
    }
    
    public static int GetSeedForEpisode(int episodeNumber)
    {
        if (Seeds.Count == 0)
        {
            Debug.LogError("SeedManager: No seeds available. Ensure Initialize() is called with a positive count.");
            return 0; // Default seed value
        }

        return Seeds[episodeNumber % Seeds.Count];
    }

    public static void Reset()
    {
        index = 0;
    }
}