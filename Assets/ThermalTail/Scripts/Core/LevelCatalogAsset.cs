using System.Collections.Generic;
using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Ordered list of level scenes, written by the importer. Lives in Resources so a
    /// player build and the editor agree on the level order.
    /// </summary>
    [CreateAssetMenu(menuName = "Thermal Tail/Level Catalog", fileName = "LevelCatalog")]
    public class LevelCatalogAsset : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string SceneName;
            public string DisplayName;
        }

        public List<Entry> Levels = new List<Entry>();
    }
}
