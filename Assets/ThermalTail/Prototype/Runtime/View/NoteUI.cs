using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class NoteUI : MonoBehaviour
    {
        public PrototypeSession Session;
        void OnGUI()
        {
            if (Session == null || Session.Screen != ModalScreen.Notes) return;
            Rect panel = new Rect((Screen.width - 500) / 2f, (Screen.height - 330) / 2f, 500, 330);
            GUI.Box(panel, "CLUE JOURNAL — world remains active");
            GUILayout.BeginArea(new Rect(panel.x + 24, panel.y + 42, panel.width - 48, panel.height - 60));
            if (Session.Notes.Count == 0) GUILayout.Label("No clues collected yet.");
            else
            {
                Session.SelectedNote = Mathf.Clamp(Session.SelectedNote, 0, Session.Notes.Count - 1);
                NoteData note = Session.Notes[Session.SelectedNote];
                GUILayout.Label((Session.SelectedNote + 1) + " / " + Session.Notes.Count + "    " + note.Title);
                GUILayout.Space(20);
                GUILayout.Label(note.Text, new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 18 });
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous")) Session.SelectedNote = (Session.SelectedNote - 1 + Session.Notes.Count) % Session.Notes.Count;
                if (GUILayout.Button("Next")) Session.SelectedNote = (Session.SelectedNote + 1) % Session.Notes.Count;
                GUILayout.EndHorizontal();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close [E / J]")) Session.CloseScreen();
            GUILayout.EndArea();
        }
    }
}
