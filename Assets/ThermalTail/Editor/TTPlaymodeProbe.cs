using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Enters real play mode headlessly, drives the lizard with scripted input for a fixed
    /// number of frames, and writes what actually happened to a text file.
    ///
    ///   Unity -batchmode -projectPath . -executeMethod ThermalTail.EditorTools.TTPlaymodeProbe.Run
    ///
    /// This is the check the screenshots cannot make: that the ground controller moves the
    /// lizard, that the hop leaves the floor, and that the camera rig follows it. It exits
    /// the editor itself, so no -quit (which would tear down before play mode starts).
    /// </summary>
    public static class TTPlaymodeProbe
    {
        const string ReportPath = "_shots3d/playmode_probe.txt";
        const int WarmupFrames = 20;
        const int DriveFrames = 150;

        static int _frame;
        static StringWriter _log;
        static Vector3 _startPos;
        static float _maxLift, _maxSpeed, _camTravel;
        static Vector3 _camStart;
        static bool _sawJump;

        public static void Run()
        {
            Directory.CreateDirectory("_shots3d");
            _log = new StringWriter();
            _frame = 0;
            _maxLift = _maxSpeed = _camTravel = 0f;
            _sawJump = false;

            EditorSceneManager.OpenScene("Assets/ThermalTail/Levels/01_Fernlight_Verge.unity", OpenSceneMode.Single);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;   // still transitioning
            _frame++;

            var dir = Object.FindFirstObjectByType<ThermalDirector>();
            if (dir == null)
            {
                Finish("FAIL: no ThermalDirector in the running scene");
                return;
            }

            if (_frame == WarmupFrames)
            {
                _startPos = new Vector3(dir.px, dir.PLift, dir.py);
                _camStart = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                _log.WriteLine($"groundPlay={dir.GroundPlay} climbMode={dir.ClimbMode} planAuthored={dir.PlanAuthored} plane={TTCoord.Plane}");
                _log.WriteLine($"start px={dir.px:F1} py={dir.py:F1} lift={dir.PLift:F1} angle={dir.PAngle:F2} mode={dir.Mode}");
                _log.WriteLine($"cam start {_camStart}");
                TTInput.Scripted = true;
            }

            if (_frame > WarmupFrames)
            {
                // Hold "forward" the whole time; hop once a second.
                TTInput.Clear();
                TTInput.Up = true;
                TTInput.Right = true;
                if ((_frame - WarmupFrames) % 60 == 30) { TTInput.JumpQueued = true; _sawJump = true; }

                _maxLift = Mathf.Max(_maxLift, dir.PLift);
                _maxSpeed = Mathf.Max(_maxSpeed, TTMath.Hypot(dir.pvx, dir.pvy));
                if (Camera.main != null)
                    _camTravel = Mathf.Max(_camTravel, Vector3.Distance(Camera.main.transform.position, _camStart));
            }

            if (_frame >= WarmupFrames + DriveFrames)
            {
                var end = new Vector3(dir.px, dir.PLift, dir.py);
                float moved = Vector2.Distance(new Vector2(_startPos.x, _startPos.z), new Vector2(end.x, end.z));
                _log.WriteLine($"end   px={dir.px:F1} py={dir.py:F1} lift={dir.PLift:F1} mode={dir.Mode}");
                _log.WriteLine($"moved {moved:F1} px over {DriveFrames} frames");
                _log.WriteLine($"max plane speed {_maxSpeed:F1} px/s");
                _log.WriteLine($"max hop lift {_maxLift:F1} px (jump requested: {_sawJump})");
                _log.WriteLine($"camera travelled {_camTravel:F2} units");

                bool movedOk = moved > 40f;
                bool hopOk = _maxLift > 20f;
                bool camOk = _camTravel > 0.2f;
                _log.WriteLine($"RESULT move={(movedOk ? "PASS" : "FAIL")} hop={(hopOk ? "PASS" : "FAIL")} camera={(camOk ? "PASS" : "FAIL")}");
                Finish(movedOk && hopOk && camOk ? "OVERALL PASS" : "OVERALL FAIL");
            }
        }

        static void Finish(string verdict)
        {
            EditorApplication.update -= Tick;
            TTInput.Scripted = false;
            _log.WriteLine(verdict);
            File.WriteAllText(ReportPath, _log.ToString());
            Debug.Log("[probe] " + verdict);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(verdict.Contains("PASS") ? 0 : 1);
        }
    }
}
