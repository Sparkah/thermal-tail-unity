using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class LockKeypadUI : MonoBehaviour
    {
        public PrototypeSession Session;
        string entered = "", feedback = "";
        LockActor current;
        Texture2D fingerprint;
        void Update()
        {
            var active = Session != null && Session.Screen == ModalScreen.Keypad ? Session.ActiveLock : null;
            if (active != current) { current = active; entered = feedback = ""; }
            if (current != null && !current.CanInteract(Session.Player)) Session.CloseScreen();
        }
        void OnGUI()
        {
            if (Session == null || Session.Screen != ModalScreen.Keypad || Session.ActiveLock == null) return;
            current = Session.ActiveLock;
            EnsureFingerprint();
            Rect panel = new Rect((Screen.width - 450) / 2f, (Screen.height - 510) / 2f, 450, 510);
            GUI.Box(panel, "INNER VAULT — FOUR DISTINCT DIGITS");
            GUI.Label(new Rect(panel.x + 28, panel.y + 35, 390, 30), entered.PadRight(4, '_'),
                new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(panel.x + 25, panel.y + 75, 400, 25), "Fingerprints identify keys, not their order.");
            for (int digit = 0; digit <= 9; digit++)
            {
                int slot = digit == 0 ? 10 : digit - 1;
                Rect button = new Rect(panel.x + 65 + slot % 3 * 108, panel.y + 110 + slot / 3 * 65, 100, 56);
                if (GUI.Button(button, digit.ToString()) && entered.Length < 4) entered += digit.ToString();
                if (current.HasFingerprint(digit)) GUI.DrawTexture(new Rect(button.xMax - 37, button.y + 8, 30, 40), fingerprint);
            }
            if (GUI.Button(new Rect(panel.x + 25, panel.y + 380, 120, 35), "Delete") && entered.Length > 0) entered = entered.Substring(0, entered.Length - 1);
            if (GUI.Button(new Rect(panel.x + 165, panel.y + 380, 120, 35), "Clear")) entered = "";
            if (GUI.Button(new Rect(panel.x + 305, panel.y + 380, 120, 35), "Submit"))
            {
                if (!current.Submit(entered)) { feedback = "Incorrect code — try again"; entered = ""; }
            }
            GUI.Label(new Rect(panel.x + 25, panel.y + 425, 400, 25), feedback);
            if (GUI.Button(new Rect(panel.x + 25, panel.y + 460, 190, 30), "Read collected clues")) Session.OpenNotes();
            if (GUI.Button(new Rect(panel.x + 235, panel.y + 460, 190, 30), "Close [E]")) Session.CloseScreen();
        }
        void EnsureFingerprint()
        {
            if (fingerprint != null) return;
            fingerprint = new Texture2D(48, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 48; x++)
            {
                float dx = (x - 24) / 21f, dy = (y - 32) / 29f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float stripe = Mathf.Sin(radius * 65 + dx * 3);
                fingerprint.SetPixel(x, y, new Color(0.7f, 0.88f, 0.95f, radius < 1 && stripe > 0.25f ? 0.65f : 0));
            }
            fingerprint.Apply();
        }
        void OnDestroy() { if (fingerprint != null) Destroy(fingerprint); }
    }
}
