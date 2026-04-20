using System;
using UnityEngine;

public static class MiniHeroesRuntimeMode
{
    private static bool initialized;
    private static bool isTraining;

    public static bool IsTraining
    {
        get
        {
            if (!initialized)
            {
                Initialize();
            }

            return isTraining;
        }
    }

    private static void Initialize()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-miniheroes-train", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "--miniheroes-train", StringComparison.OrdinalIgnoreCase))
            {
                isTraining = true;
                initialized = true;
                return;
            }
        }

        isTraining = Application.isBatchMode;
        initialized = true;
    }
}
