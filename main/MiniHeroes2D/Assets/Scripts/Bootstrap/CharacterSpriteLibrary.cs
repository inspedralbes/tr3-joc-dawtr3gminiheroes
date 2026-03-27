using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniHeroes2D.Bootstrap
{
    public static class CharacterSpriteLibrary
    {
        public static readonly BattleBootstrap.CharacterClass[] AllClasses =
        {
            BattleBootstrap.CharacterClass.Caballero,
            BattleBootstrap.CharacterClass.Arquero,
            BattleBootstrap.CharacterClass.Mago,
            BattleBootstrap.CharacterClass.Ladron,
            BattleBootstrap.CharacterClass.Curandero,
            BattleBootstrap.CharacterClass.Angel,
            BattleBootstrap.CharacterClass.Demonio
        };

        public static Sprite TryGetPortrait(BattleBootstrap.CharacterClass characterClass)
        {
            string sheetBaseName = SheetBaseName(characterClass);
            if (string.IsNullOrWhiteSpace(sheetBaseName)) return null;

            string desiredSpriteName = $"{sheetBaseName}_0";

            Sprite sprite = TryLoadSpriteFromResources(sheetBaseName, desiredSpriteName);
#if UNITY_EDITOR
            sprite ??= TryLoadSpriteFromAssetDatabase(sheetBaseName, desiredSpriteName);
#endif
            return sprite;
        }

        private static string SheetBaseName(BattleBootstrap.CharacterClass characterClass)
        {
            return characterClass switch
            {
                BattleBootstrap.CharacterClass.Caballero => "SpritesheetMiniCaballero",
                BattleBootstrap.CharacterClass.Arquero => "SpritesheetMiniArquero",
                BattleBootstrap.CharacterClass.Mago => "SpritesheetMiniMago",
                BattleBootstrap.CharacterClass.Ladron => "SpritesheetMiniLadron",
                BattleBootstrap.CharacterClass.Curandero => "SpritesheetMiniCurandero",
                BattleBootstrap.CharacterClass.Angel => "SpritesheetMiniAngel",
                BattleBootstrap.CharacterClass.Demonio => "SpritesheetMiniDemonio",
                _ => null
            };
        }

        private static Sprite TryLoadSpriteFromResources(string sheetBaseName, string desiredSpriteName)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>($"Personatges/{sheetBaseName}");
            if (sprites == null || sprites.Length == 0) return null;

            for (int i = 0; i < sprites.Length; i += 1)
            {
                if (sprites[i] != null && sprites[i].name == desiredSpriteName) return sprites[i];
            }

            return sprites[0];
        }

#if UNITY_EDITOR
        private static Sprite TryLoadSpriteFromAssetDatabase(string sheetBaseName, string desiredSpriteName)
        {
            string assetPath = $"Assets/Personatges/{sheetBaseName}.png";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null || assets.Length == 0) return null;

            Sprite fallback = null;
            for (int i = 0; i < assets.Length; i += 1)
            {
                if (assets[i] is not Sprite sprite) continue;
                fallback ??= sprite;
                if (sprite.name == desiredSpriteName) return sprite;
            }

            return fallback;
        }
#endif
    }
}

