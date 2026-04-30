using System.IO;
using UnityEngine;

public static class Logger {
    private static string _baseDirectory = Application.dataPath + "/Logs/";
    private static string _currentFileName;

    public static void LogEpisode(
        string method,
        int episode,
        int pieces,
        int wallHits,
        float reward,
        int fixedUpdateCount,
        int networkSize = 0 // Optional for PPO
    )
    {
        if (_currentFileName == null)
        {
            _currentFileName = GenerateFileName(method, networkSize);
        }

        string filePath = _baseDirectory + _currentFileName;

        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }

        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "Method,Episode,Pieces,WallHits,Reward,FixedUpdateCount\n");
        }

        string line = $"{method},{episode},{pieces},{wallHits},{reward},{fixedUpdateCount}\n";
        File.AppendAllText(filePath, line);
    }

    private static string GenerateFileName(string method, int networkSize)
    {
        string baseName = method;
        if (method == "PPO")
        {
            baseName += $"_{networkSize}";
        }

        int fileNumber = 1;
        string fileName;
        do
        {
            fileName = $"{baseName}_{fileNumber}.csv";
            fileNumber++;
        } while (File.Exists(_baseDirectory + fileName));

        return fileName;
    }
}