using UnityEngine;

namespace ThermalTail.Prototype
{
    [CreateAssetMenu(menuName = "Thermal Tail/Note")]
    public sealed class NoteData : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(3, 10)] public string Text;
    }
}
