using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThermalTail
{
    /// <summary>Runtime access to the imported level order.</summary>
    public static class LevelCatalog
    {
        static LevelCatalogAsset _asset;

        public static LevelCatalogAsset Asset
        {
            get
            {
                if (_asset == null) _asset = Resources.Load<LevelCatalogAsset>("LevelCatalog");
                return _asset;
            }
        }

        public static int Count => Asset != null ? Asset.Levels.Count : 0;

        public static bool HasLevel(int index) => Asset != null && index >= 0 && index < Asset.Levels.Count;

        public static string SceneName(int index) => HasLevel(index) ? Asset.Levels[index].SceneName : null;

        public static string DisplayName(int index) => HasLevel(index) ? Asset.Levels[index].DisplayName : "";

        public static void Load(int index)
        {
            if (!HasLevel(index))
            {
                Debug.LogWarning("[ThermalTail] No level at index " + index);
                return;
            }
            SceneManager.LoadScene(Asset.Levels[index].SceneName);
        }
    }
}
