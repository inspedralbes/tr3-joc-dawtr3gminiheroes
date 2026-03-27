using System;
using UnityEngine;

namespace MiniHeroes2D.Bootstrap
{
    public static class GameSessionConfig
    {
        private const string PlayerClassKey = "MiniHeroes2D.PlayerClass";
        private const string AiClassKey = "MiniHeroes2D.AiClass";

        public static BattleBootstrap.CharacterClass PlayerClass { get; private set; } = BattleBootstrap.CharacterClass.Caballero;
        public static BattleBootstrap.CharacterClass AiClass { get; private set; } = BattleBootstrap.CharacterClass.Arquero;

        public static void LoadFromPrefs()
        {
            PlayerClass = ReadEnum(PlayerClassKey, PlayerClass);
            AiClass = ReadEnum(AiClassKey, AiClass);
        }

        public static void SaveToPrefs(BattleBootstrap.CharacterClass playerClass, BattleBootstrap.CharacterClass aiClass)
        {
            PlayerClass = playerClass;
            AiClass = aiClass;

            PlayerPrefs.SetString(PlayerClassKey, playerClass.ToString());
            PlayerPrefs.SetString(AiClassKey, aiClass.ToString());
            PlayerPrefs.Save();
        }

        private static BattleBootstrap.CharacterClass ReadEnum(string key, BattleBootstrap.CharacterClass fallback)
        {
            string raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            if (Enum.TryParse(raw, ignoreCase: true, out BattleBootstrap.CharacterClass result))
                return result;

            return fallback;
        }
    }
}

