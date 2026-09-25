using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThermalTail.Prototype
{
    public enum ModalScreen { None, Notes, Keypad }

    [DefaultExecutionOrder(-200)]
    public sealed class PrototypeSession : MonoBehaviour
    {
        public PrototypeSettings Settings;
        public SurfaceMotor Player;
        public PlayerThermal Thermal;
        public Transform InitialSpawn;
        public event Action ResetOccurred;
        public event Action<Vector3> CivilianAlert;
        public event Action Completed;
        public event Action KeysChanged;
        public readonly List<NoteData> Notes = new List<NoteData>();
        readonly HashSet<string> collected = new HashSet<string>();
        Vector3 checkpointPosition;
        Quaternion checkpointRotation;
        float checkpointTemperature, resetAt = -1f, nextContactAlert;
        public float GraceRemaining { get; private set; }
        public float AttemptTime { get; private set; }
        public bool IsComplete { get; private set; }
        public bool Resetting => resetAt >= 0f;
        public bool PlayerInputBlocked => IsComplete || Resetting || Screen != ModalScreen.None;
        public ModalScreen Screen { get; private set; }
        public LockActor ActiveLock { get; private set; }
        public int SelectedNote { get; set; }
        public int MothsCollected { get; private set; }
        public int KeysCollected { get; private set; }
        public string Section { get; private set; } = "Lobby";
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }

        void Awake()
        {
            if (Settings == null)
            {
                Debug.LogError("Assign PrototypeSettings to the prototype session.", this);
                enabled = false;
            }
        }

        void Start()
        {
            if (Player == null) Player = FindFirstObjectByType<SurfaceMotor>();
            if (Thermal == null && Player != null) Thermal = Player.GetComponent<PlayerThermal>();
            if (Player == null || Thermal == null) { enabled = false; return; }
            Transform spawn = InitialSpawn != null ? InitialSpawn : Player.transform;
            SetCheckpoint(spawn.position, spawn.rotation, Settings.Ambient, "Lobby", false);
            RestoreCheckpoint();
        }

        void Update()
        {
            if (IsComplete) return;
            if (Resetting)
            {
                if (Time.time >= resetAt) RestoreCheckpoint();
                return;
            }
            AttemptTime += Time.deltaTime;
            GraceRemaining = Mathf.Max(0f, GraceRemaining - Time.deltaTime);
        }

        public void SetCheckpoint(Vector3 position, Quaternion rotation, float temperature, string section, bool notify = true)
        {
            checkpointPosition = position;
            checkpointRotation = rotation;
            checkpointTemperature = temperature;
            Section = section;
            if (notify) Notify("Checkpoint: " + section);
        }

        public bool TryCapture(string reason, bool ignoreRespawnGrace = false)
        {
            if (IsComplete || Resetting || (!ignoreRespawnGrace && GraceRemaining > 0f)) return false;
            CloseScreen();
            resetAt = Time.time + 0.8f;
            Notify(reason + " — returning to checkpoint", 2f);
            return true;
        }

        public void RestoreCheckpoint()
        {
            resetAt = -1f;
            AttemptTime = 0f;
            nextContactAlert = 0f;
            GraceRemaining = Settings.RespawnGrace;
            CloseScreen();
            ResetOccurred?.Invoke();
            if (Player != null) Player.Warp(checkpointPosition, checkpointRotation);
            Physics.SyncTransforms();
            if (Thermal != null) Thermal.SetTemperature(checkpointTemperature);
            // Notes and collected IDs deliberately survive an attempt reset.
        }

        public bool TryCollect(string id)
        {
            return !IsComplete && !Resetting && !string.IsNullOrWhiteSpace(id) && collected.Add(id);
        }
        public bool HasCollected(string id) => collected.Contains(id);

        public void AddNote(NoteData note)
        {
            if (note == null) return;
            if (!Notes.Contains(note)) Notes.Add(note);
            SelectedNote = Notes.IndexOf(note);
            // Do not replace a keypad the player is currently using.
            if (Screen != ModalScreen.Keypad) Screen = ModalScreen.Notes;
            Notify("Clue stored — J opens your notes");
        }

        public void AddMoth() { MothsCollected++; Notify("Glowmoth collected"); }
        public void AddKey()
        {
            KeysCollected++;
            KeysChanged?.Invoke();
            Notify("Key collected — " + KeysCollected);
        }
        public void OpenNotes() { if (!IsComplete && !Resetting) Screen = ModalScreen.Notes; }
        public void OpenLock(LockActor actor)
        {
            if (IsComplete || Resetting) return;
            ActiveLock = actor;
            Screen = ModalScreen.Keypad;
        }
        public void CloseScreen() { Screen = ModalScreen.None; ActiveLock = null; }

        public void ReportCivilianContact(Vector3 position)
        {
            ReportCivilianAlert(position, "Civilian alerted security — break contact!");
        }

        public void ReportCivilianThermalAlert(Vector3 position)
        {
            ReportCivilianAlert(position, "Civilian noticed your temperature — security alerted!");
        }

        void ReportCivilianAlert(Vector3 position, string message)
        {
            if (IsComplete || Resetting || GraceRemaining > 0f || Time.time < nextContactAlert) return;
            nextContactAlert = Time.time + 2f;
            CivilianAlert?.Invoke(position);
            Notify(message, 2f);
        }

        public void Complete()
        {
            if (IsComplete || Resetting) return;
            IsComplete = true;
            CloseScreen();
            Completed?.Invoke();
            Notify("INNER VAULT OPEN — MISSION COMPLETE", 1000f);
        }
        public void Notify(string message, float duration = 3f) { Message = message; MessageUntil = Time.time + duration; }
    }
}
