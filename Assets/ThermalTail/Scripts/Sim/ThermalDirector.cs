using System.Collections.Generic;
using UnityEngine;

namespace ThermalTail
{
    public enum GameMode { Playing, Respawning, LevelComplete, Failed, Victory, Paused }
    public enum GuardState { Patrol, Search, Chase, Return }

    /// <summary>Score and lives survive level transitions, as the browser build's globals did.</summary>
    public static class TTSession
    {
        public static int Score;
        public static int Lives = 3;
        public static int LevelBaseScore;
        public static bool Initialised;

        public static void ResetRun(ThermalTuning tuning)
        {
            Score = 0;
            Lives = tuning != null ? tuning.StartingLives : 3;
            LevelBaseScore = 0;
            Initialised = true;
        }
    }

    /// <summary>One authored object, flattened for the simulation. Index-aligned with the source list.</summary>
    public class Obj
    {
        public int index;
        public TTObjectType type;
        public float x, y, w, h, value;
        public string label;
        public TTObject comp;
        public Transform tf;
        public Renderer rend;

        // Authored rect, restored on level reset.
        public float ax, ay, aw, ah;

        // Decoded behaviour (replaces the shipping build's substring-matched label).
        public LensFacing facing;
        public SweepRate sweep;
        public SweepArc arc;
        public ClimbAim climbAim;
        public bool alwaysOn, pulse;
        public GuardSpeed guardSpeed;
        public PatrolAxis patrol;
        public PatrolAxis ledgeAxis;
        public bool ledgeFast;
    }

    public class Enemy
    {
        public int index;
        public float homeX, homeY, x, y, vx, vy, w, h, span, speed;
        public bool vertical;
        public int dir = 1, face = 1;
        public float angle;
        public GuardState state = GuardState.Patrol;
        public float tx, ty, alert, searchTimer, spot, phase, anim;
        public bool hasBounds;
        public float boundsLeft, boundsRight, boundsTop;
        public bool dead;
        public float fade;
        public Transform tf;
    }

    public class Alarm
    {
        public float x, y, timer, max;
    }

    public struct LensInfo
    {
        public bool active;
        public float phase;
        public int direction;
        public float range, height, threshold;
    }

    public struct ClimbLensInfo
    {
        public float aim, spread, radius, phase, threshold;
        public bool active;
    }

    public struct Solid
    {
        public int index;
        public TTObjectType type;
        public Rect rect;
    }

    /// <summary>
    /// The ported Thermal Tail simulation. Runs in source-pixel space at a pinned 1/60
    /// step so every constant matches the shipping browser build exactly; Unity transforms
    /// are driven from the result at 1 unit = 100 px.
    /// </summary>
    public partial class ThermalDirector : MonoBehaviour
    {
        public ThermalTuning Tuning;
        public LevelSettings Settings;
        public Transform PlayerView;
        public FollowCameraRig CameraRig;

        [Header("3D")]
        [Tooltip("Lay every level out as a floor plan on the XZ ground plane and play it top-down in 3D. " +
                 "Off falls back to the original side-on 2D port.")]
        public bool GroundPlay = true;
        [Tooltip("Build a floor, lighting and fog at runtime so the greybox reads as a place.")]
        public bool BuildWorldDressing = true;

        [Header("Debug")]
        public bool LogEvents;
        public string LastEvent = "";

        // ---- level ----
        public List<Obj> Objects = new List<Obj>();
        public bool ClimbMode;
        public float BaseAmbient;
        public int TotalMoths;
        public int RequiredMoths;
        public float LevelTime;

        // ---- player ----
        public float px, py, pw = 72f, ph = 48f, pvx, pvy;
        /// <summary>
        /// Height off the floor in source pixels, and its velocity. Ground play only.
        /// Side play keeps using py/pvy for the vertical axis exactly as the port did, so
        /// this pair stays at zero there and no shared constant has to mean two things.
        /// </summary>
        public float PLift, PLiftVel;
        public int PFacing = 1;
        public float PAngle = -Mathf.PI * 0.5f;
        public bool PGrounded;
        public int PGroundIndex = -1;
        public float PCoyote = 0.12f, PJumpBuffer;
        public float PTemp, PAmbient, PFocus = 100f;
        public bool PMatching, PMaskLocked, PHidden;

        // ---- world state ----
        public List<Enemy> Enemies = new List<Enemy>();
        public List<Alarm> Alarms = new List<Alarm>();
        public float[] CameraMeters = new float[0];
        public float[] CameraCooldowns = new float[0];
        public bool[] GateStates = new bool[0];
        public HashSet<int> Collected = new HashSet<int>();
        public float MaxDetection;
        public int DangerCamera = -1;

        public GameMode Mode = GameMode.Playing;
        public float Grace, TransitionTimer;
        public float CheckpointX, CheckpointY;
        /// <summary>
        /// The den, after any plan-view correction. Always read this rather than
        /// Settings.Goal, which is the authored side-view position and can sit over a drop.
        /// </summary>
        public float GoalX, GoalY;
        public int ActivatedCheckpoint = -1;
        public int Combo;
        public float ComboTimer;
        public int LevelCatches, AlertCount, LevelExecutions;
        public string CatchReason = "";
        public int LastBonus;
        public float StrikeCooldown, StrikeFlash;
        public bool ExecutionReady;
        public string Toast = "";
        public float ToastTimer;
        float _warningCooldown, _goalHintCooldown, _spotWarnCooldown;
        bool _blendLatched, _hiddenLatched, _executeHintShown;
        List<Solid> _climbSolidCache;
        bool _climbCacheValid;
        float _accumulator;
        TTRandom _rng;

        public float Mismatch => Mathf.Abs(PTemp - PAmbient);
        public int MothsCollected => Collected.Count;

        void Awake()
        {
            if (Tuning == null)
                Tuning = Resources.Load<ThermalTuning>("ThermalTuning");
            if (Settings == null)
                Settings = FindFirstObjectByType<LevelSettings>();
            if (!TTSession.Initialised) TTSession.ResetRun(Tuning);
            GatherObjects();
            LoadLevel();
        }

        /// <summary>
        /// Build the index-aligned object list from the scene. Ordering is the authored
        /// ordering: the detection meters, gate states and per-lens phase offsets
        /// (index * 0.713) all key off it, so it has to be stable.
        /// </summary>
        void GatherObjects()
        {
            Objects.Clear();
            var found = new List<TTObject>(FindObjectsByType<TTObject>(FindObjectsSortMode.None));
            found.Sort((a, b) => a.SourceIndex.CompareTo(b.SourceIndex));
            for (int i = 0; i < found.Count; i++)
            {
                var c = found[i];
                var o = new Obj
                {
                    index = i,
                    type = c.Type,
                    x = c.SourceX, y = c.SourceY, w = c.SourceW, h = c.SourceH,
                    ax = c.SourceX, ay = c.SourceY, aw = c.SourceW, ah = c.SourceH,
                    value = c.Value,
                    label = c.DisplayName,
                    comp = c,
                    tf = c.transform,
                    rend = c.GetComponentInChildren<Renderer>(),
                    facing = c.Facing,
                    sweep = c.Sweep,
                    arc = c.Arc,
                    climbAim = c.ClimbBaseAim,
                    alwaysOn = c.AlwaysOn,
                    pulse = c.Pulse,
                    guardSpeed = c.Speed,
                    patrol = c.Patrol,
                    ledgeAxis = c.LedgeAxis,
                    ledgeFast = c.LedgeFast
                };
                Objects.Add(o);
            }
        }

        /// <summary>
        /// Extrude the floor plan. The authored rects say where a thing is and how much
        /// floor it takes; ViewDepth was only ever a presentation nicety in the flat build,
        /// so on the ground plane it becomes the standing height and the greybox turns into
        /// geometry you can walk between. Authored values are untouched.
        /// </summary>
        /// <summary>
        /// True when the authored rects are a floor plan already. Climb levels were drawn
        /// that way; side levels are elevations, and their ledges are decking in plan view
        /// rather than walls. Everything that has to reconcile the two readings asks here.
        /// </summary>
        public bool PlanAuthored => Settings != null && Settings.IsClimb;

        bool CoversLevel(Obj o) =>
            Settings != null && o.w * o.h >= Settings.Width * Settings.Height * 0.8f;

        /// <summary>
        /// Bring everything the player has to reach onto a deck.
        ///
        /// In the side view a glowmoth hanging in the air above a ledge was a jump you made;
        /// read as a plan that same rect is out over the drop, unreachable however well you
        /// play. Same for the den, a scent mark, or a vent that floated over a gap. Each one
        /// moves to the nearest deck, keeping its authored size, so the designer's intent -
        /// this thing belongs by that ledge - survives the change of reading.
        ///
        /// Only side-authored levels need it. A climb level was drawn as a plan already, and
        /// anything placed off its walls was placed on the floor on purpose.
        /// </summary>
        void SnapObjectsToDecks()
        {
            if (!GroundPlay || PlanAuthored) return;

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.comp != null && o.comp.IsMetadata) continue;
                if (o.type == TTObjectType.Platform || o.type == TTObjectType.MovingPlatform) continue;

                float cx = o.x + o.w * 0.5f, cy = o.y + o.h * 0.5f;
                if (!OverVoid(cx, cy)) continue;

                SnapToSurface(ref cx, ref cy);
                o.x = cx - o.w * 0.5f;
                o.y = cy - o.h * 0.5f;
            }

            // The den is the one goal that is not an object, so it is moved by hand, along
            // with the marker the importer dropped in the scene for it.
            GoalX = Settings.Goal.x;
            GoalY = Settings.Goal.y;
            SnapToSurface(ref GoalX, ref GoalY);

            var den = GameObject.Find("Exit Den");
            if (den != null)
                den.transform.position = TTCoord.Point(GoalX, GoalY, TTView.DeckHeight + 0.25f);
        }

        void ApplyStandingHeights()
        {
            TTObject.SuppressAuthoring = true;
            try
            {
                for (int i = 0; i < Objects.Count; i++)
                {
                    var o = Objects[i];
                    if (o.comp == null || o.tf == null) continue;
                    if (o.comp.IsMetadata) { o.tf.gameObject.SetActive(false); continue; }
                    o.comp.ViewDepth = TTView.StandingHeight(o.type, PlanAuthored);
                    o.tf.localScale = TTCoord.RectScale(o.w, o.h, o.comp.ViewDepth);
                    o.tf.localPosition = TTCoord.RectCenter(o.x, o.y, o.w, o.h, o.comp.ViewDepth * 0.5f);
                    o.tf.localRotation = Quaternion.identity;

                    // A climate zone drawn across the whole level is just the level's base
                    // ambient restated. Flat on the floor it would paint over every other
                    // read in the scene, so it stays simulated and stops being drawn.
                    if (o.rend != null && TTView.IsAirVolume(o.type))
                        o.rend.enabled = !CoversLevel(o);
                }
            }
            finally { TTObject.SuppressAuthoring = false; }
        }

        public void LoadLevel()
        {
            if (Settings == null || Tuning == null)
            {
                Debug.LogError("[ThermalTail] Missing " + (Settings == null ? "LevelSettings in the scene" : "ThermalTuning asset") + ".");
                enabled = false;
                return;
            }

            TTCoord.Plane = GroundPlay ? ViewPlane.Ground3D : ViewPlane.Flat2D;

            // Ground play runs every level through the climb rules: 8-way movement over a
            // floor, per-axis AABB blocking, no gravity on the play plane. Those rules were
            // already a top-down controller - the browser build just drew them on a wall -
            // so promoting them is what turns the whole game 3D without retuning anything.
            ClimbMode = GroundPlay || Settings.IsClimb;

            LevelTime = 0f;
            BaseAmbient = TTMath.Clamp(Settings.Ambient, 0f, 100f);

            TotalMoths = 0;
            for (int i = 0; i < Objects.Count; i++)
                if (Objects[i].type == TTObjectType.Moth) TotalMoths++;
            RequiredMoths = (int)TTMath.Clamp(Mathf.Round(Settings.Quota), 0, TotalMoths);

            // Restore authored rects so a reset after a moving-ledge run is exact.
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                o.x = o.ax; o.y = o.ay; o.w = o.aw; o.h = o.ah;
            }

            GoalX = Settings.Goal.x;
            GoalY = Settings.Goal.y;

            // Everything below reads the restored rects, so the plan-view corrections and
            // the greybox placement have to happen after that restore, not before it.
            if (GroundPlay)
            {
                SnapObjectsToDecks();
                ApplyStandingHeights();
                if (BuildWorldDressing && Application.isPlaying) ThermalWorld3D.Build(Settings, PlanAuthored);
            }

            float maxX = Mathf.Max(18f, Settings.Width - 18f);
            float sx = TTMath.Clamp(Settings.PlayerStart.x, 18f, maxX);
            float sy = TTMath.Clamp(Settings.PlayerStart.y, -200f, Settings.Height + 100f);
            float air = AmbientAt(sx, sy);

            SnapToSurface(ref sx, ref sy);
            px = sx; py = sy;
            pw = Tuning.PlayerWidth; ph = Tuning.PlayerHeight;
            pvx = pvy = 0f;
            PLiftVel = 0f;
            PLift = GroundPlay ? Mathf.Max(0f, SurfaceTopAt(sx, sy)) : 0f;
            PFacing = 1;
            PAngle = -Mathf.PI * 0.5f;
            if (GroundPlay)
            {
                // Face the objective at spawn. These levels were authored as side-on runs,
                // so their long axis is wherever the den is - assuming a fixed forward put
                // the chase camera outside the arena looking at a wall on level 1.
                float gdx = GoalX - sx;
                float gdy = GoalY - sy;
                if (Mathf.Abs(gdx) > 1f || Mathf.Abs(gdy) > 1f) PAngle = Mathf.Atan2(gdy, gdx);
                PFacing = gdx >= 0f ? 1 : -1;
            }
            PGrounded = false;
            PGroundIndex = -1;
            PCoyote = Tuning.CoyoteTime;
            PJumpBuffer = 0f;
            PTemp = TTMath.Clamp(Settings.StartTemp, 0f, 100f);
            PAmbient = air;
            PFocus = 100f;
            PMatching = PMaskLocked = PHidden = false;

            CheckpointX = sx; CheckpointY = sy;
            ActivatedCheckpoint = -1;
            Collected.Clear();
            CameraMeters = new float[Objects.Count];
            CameraCooldowns = new float[Objects.Count];
            GateStates = new bool[Objects.Count];
            MaxDetection = 0f;
            DangerCamera = -1;
            Grace = Tuning.GraceInitial;
            TransitionTimer = 0f;
            ToastTimer = 0f;
            _warningCooldown = _goalHintCooldown = _spotWarnCooldown = 0f;
            Combo = 0; ComboTimer = 0f;
            LevelCatches = 0; AlertCount = 0; LevelExecutions = 0;
            CatchReason = ""; LastBonus = 0;
            StrikeCooldown = 0f; StrikeFlash = 0f;
            ExecutionReady = false; _executeHintShown = false;
            _blendLatched = _hiddenLatched = false;
            Alarms.Clear();
            _rng = new TTRandom(Settings.LevelIndex);
            Mode = GameMode.Playing;
            _accumulator = 0f;

            BuildEnemies();
            InvalidateClimbCache();
            TTInput.Clear();
            SyncView();
            if (CameraRig != null) CameraRig.SnapTo(ViewTargetX(), ViewTargetY());
            ShowToast(Settings.LevelName, 2.5f);
        }

        void Update()
        {
            if (Tuning == null || Settings == null) return;

            if (TTInput.PressedThisFrame("r")) { RestartLevel(); return; }
            if (TTInput.PressedThisFrame("p"))
                Mode = Mode == GameMode.Paused ? GameMode.Playing : (Mode == GameMode.Playing ? GameMode.Paused : Mode);

            if (Mode == GameMode.Failed || Mode == GameMode.Victory)
            {
                SyncView();
                return;
            }

            TTInput.Poll(ClimbMode, GroundPlay);

            float elapsed = Mathf.Min(Tuning.MaxFrameElapsed, Mathf.Max(0f, Time.deltaTime));
            _accumulator += elapsed;
            int guard = 0;
            while (_accumulator >= Tuning.FixedStep && guard++ < 8)
            {
                Simulate(Tuning.FixedStep);
                _accumulator -= Tuning.FixedStep;
            }
            SyncView();
        }

        /// <summary>Port of simulate(dt). Timer decay, transitions, then one player update.</summary>
        void Simulate(float dt)
        {
            if (Mode == GameMode.Paused) return;

            if (ToastTimer > 0f) ToastTimer = Mathf.Max(0f, ToastTimer - dt);
            if (_warningCooldown > 0f) _warningCooldown = Mathf.Max(0f, _warningCooldown - dt);
            if (_goalHintCooldown > 0f) _goalHintCooldown = Mathf.Max(0f, _goalHintCooldown - dt);
            if (_spotWarnCooldown > 0f) _spotWarnCooldown = Mathf.Max(0f, _spotWarnCooldown - dt);
            if (StrikeCooldown > 0f) StrikeCooldown = Mathf.Max(0f, StrikeCooldown - dt);
            if (StrikeFlash > 0f) StrikeFlash = Mathf.Max(0f, StrikeFlash - dt);
            if (ComboTimer > 0f)
            {
                ComboTimer = Mathf.Max(0f, ComboTimer - dt);
                if (ComboTimer == 0f) Combo = 0;
            }

            if (Mode == GameMode.LevelComplete)
            {
                TransitionTimer -= dt;
                if (TransitionTimer <= 0f) GoNext();
                return;
            }
            if (Mode == GameMode.Respawning)
            {
                TransitionTimer -= dt;
                if (TransitionTimer <= 0f) Respawn();
                return;
            }
            if (Mode != GameMode.Playing) return;

            LevelTime += dt;
            Grace = Mathf.Max(0f, Grace - dt);

            for (int i = Alarms.Count - 1; i >= 0; i--)
            {
                Alarms[i].timer -= dt;
                if (Alarms[i].timer <= 0f) Alarms.RemoveAt(i);
            }

            InvalidateClimbCache();
            UpdatePlayer(dt);
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>Port of objectRect(o, index, time), including the moving-ledge sine.</summary>
        public Rect ObjectRect(Obj o, float time)
        {
            float x = o.x, y = o.y, w = o.w, h = o.h;
            if (o.type == TTObjectType.MovingPlatform)
            {
                float speed = o.ledgeFast ? Tuning.LedgeSpeedFast : Tuning.LedgeSpeedDefault;
                float amp = TTMath.Clamp(Mathf.Abs(o.value), 0f, Tuning.LedgeAmplitudeMax);
                float wave = Mathf.Sin(time * speed + o.index * Tuning.LedgePhasePerIndex) * amp;
                if (o.ledgeAxis == PatrolAxis.Vertical) y += wave; else x += wave;
            }
            if (w < 0f) { x += w; w = -w; }
            if (h < 0f) { y += h; h = -h; }
            return new Rect(x, y, w, h);
        }

        public static bool Intersects(Rect a, Rect b)
            => a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;

        public static bool PointIn(float x, float y, Rect r)
            => x >= r.x && x <= r.x + r.width && y >= r.y && y <= r.y + r.height;

        /// <summary>Port of playerRect(extra). Climb mode uses a square around the centre.</summary>
        public Rect PlayerRect(float extra)
        {
            if (ClimbMode)
            {
                float s = Tuning.ClimbHalfExtent + extra;
                return new Rect(px - s, py - s, s * 2f, s * 2f);
            }
            return new Rect(px - pw * 0.5f - extra, py - ph * 0.5f - extra, pw + extra * 2f, ph + extra * 2f);
        }

        /// <summary>Port of ambientAt(x,y): the level ambient unless a climate zone overrides it.</summary>
        public float AmbientAt(float x, float y)
        {
            float value = BaseAmbient;
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.AmbientZone) continue;
                if (PointIn(x, y, ObjectRect(o, LevelTime))) value = TTMath.Clamp(o.value, 0f, 100f);
            }
            return value;
        }

        public float PaceScale() => Tuning.PaceScale(PTemp);

        /// <summary>Port of solidList(time), including the implicit ground slab fallback.</summary>
        public List<Solid> SolidList(float time)
        {
            var outList = new List<Solid>();
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type == TTObjectType.Platform || o.type == TTObjectType.MovingPlatform)
                    outList.Add(new Solid { index = i, type = o.type, rect = ObjectRect(o, time) });
            }
            if (outList.Count == 0 && !ClimbMode)
            {
                outList.Add(new Solid
                {
                    index = -2,
                    type = TTObjectType.Platform,
                    rect = new Rect(0f, Settings.Height * 0.78f, Settings.Width, Mathf.Max(80f, Settings.Height * 0.22f))
                });
            }
            return outList;
        }

        List<Solid> BuildClimbSolids()
        {
            // In a side-authored level the ledges lie flat as decking, so they are scenery,
            // not geometry. A closed thermal gate is a barrier in either reading and always
            // blocks - it is the one obstacle the hop is not allowed to beat.
            bool ledgesBlock = !GroundPlay || PlanAuthored;

            var outList = new List<Solid>();
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (ledgesBlock && (o.type == TTObjectType.Platform || o.type == TTObjectType.MovingPlatform))
                    outList.Add(new Solid { index = i, type = o.type, rect = ObjectRect(o, LevelTime) });
                else if (o.type == TTObjectType.ThermalGate && !GateOpen(o))
                    outList.Add(new Solid { index = i, type = o.type, rect = ObjectRect(o, LevelTime) });
            }
            return outList;
        }

        void InvalidateClimbCache()
        {
            _climbCacheValid = false;
            if (ClimbMode) { _climbSolidCache = BuildClimbSolids(); _climbCacheValid = true; }
        }

        List<Solid> ClimbSolids()
        {
            if (_climbCacheValid && _climbSolidCache != null) return _climbSolidCache;
            return BuildClimbSolids();
        }

        bool ClimbBlocked(float x, float y, float half, out Solid hit)
        {
            var list = ClimbSolids();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i].rect;
                if (x + half > r.x && x - half < r.x + r.width && y + half > r.y && y - half < r.y + r.height)
                {
                    hit = list[i];
                    return true;
                }
            }
            hit = default;
            return false;
        }

        /// <summary>Port of lineBlocked: 12 samples along the segment, climb levels only.</summary>
        bool LineBlocked(float x1, float y1, float x2, float y2)
        {
            if (!ClimbMode) return false;
            var list = ClimbSolids();
            if (list.Count == 0) return false;
            for (int s = 1; s < 13; s++)
            {
                float t = s / 13f;
                float x = x1 + (x2 - x1) * t;
                float y = y1 + (y2 - y1) * t;
                for (int j = 0; j < list.Count; j++)
                {
                    var r = list[j].rect;
                    if (x >= r.x && x <= r.x + r.width && y >= r.y && y <= r.y + r.height) return true;
                }
            }
            return false;
        }

        /// <summary>Port of gateOpen: within 6.5 degrees of the gate's target temperature.</summary>
        public bool GateOpen(Obj o)
            => Mathf.Abs(PTemp - TTMath.Clamp(o.value, 0f, 100f)) <= Tuning.GateTolerance;

        // ---------------------------------------------------------------- environment

        public struct Env
        {
            public float cool, warm, focusBonus, dry, wind;
            public bool hidden;
        }

        /// <summary>Port of environmentForPlayer(): overlap tests against every volume type.</summary>
        public Env EnvironmentForPlayer()
        {
            var p = PlayerRect(Tuning.EnvironmentPad);
            var e = new Env();
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.CoolRock && o.type != TTObjectType.IceMist &&
                    o.type != TTObjectType.WarmVent && o.type != TTObjectType.SunPatch &&
                    o.type != TTObjectType.Shelter && o.type != TTObjectType.DryAir &&
                    o.type != TTObjectType.Wind) continue;
                var r = ObjectRect(o, LevelTime);
                if (!Intersects(p, r)) continue;

                if (o.type == TTObjectType.CoolRock || o.type == TTObjectType.IceMist)
                {
                    float rate = Mathf.Abs(o.value); if (rate == 0f) rate = Tuning.CoolRateDefault;
                    e.cool = Mathf.Max(e.cool, rate);
                    e.focusBonus = Mathf.Max(e.focusBonus, Tuning.FocusBonusCool);
                }
                else if (o.type == TTObjectType.WarmVent || o.type == TTObjectType.SunPatch)
                {
                    float rate = Mathf.Abs(o.value); if (rate == 0f) rate = Tuning.WarmRateDefault;
                    e.warm = Mathf.Max(e.warm, rate);
                    e.focusBonus = Mathf.Max(e.focusBonus, Tuning.FocusBonusWarm);
                }
                else if (o.type == TTObjectType.Shelter)
                {
                    e.hidden = true;
                    e.focusBonus = Mathf.Max(e.focusBonus, Tuning.FocusBonusShelter);
                }
                else if (o.type == TTObjectType.DryAir) e.dry += Mathf.Abs(o.value);
                else if (o.type == TTObjectType.Wind) e.wind += o.value;
            }
            return e;
        }

        bool IsHiddenNow()
        {
            var p = PlayerRect(Tuning.HiddenPad);
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type == TTObjectType.Shelter && Intersects(p, ObjectRect(o, LevelTime))) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- player

        /// <summary>
        /// Port of updatePlayer(dt). The order here is load-bearing: mask and drift resolve
        /// before movement, ambient and cover are re-read after it, then detection, guards
        /// and pickups run in that sequence, each gated on still being in play.
        /// </summary>
        void UpdatePlayer(float dt)
        {
            float oldAmbient = PAmbient;
            PAmbient = AmbientAt(px, py);
            var env = EnvironmentForPlayer();
            PHidden = env.hidden;

            if (!TTInput.Match && PFocus >= Tuning.MaskReleaseFocus) PMaskLocked = false;
            if (TTInput.Match && PFocus <= Tuning.MaskDepleteFocus && !PMaskLocked)
            {
                PMaskLocked = true;
                ShowToast("MASK DEPLETED - RECHARGING", 1.25f);
            }
            if (PMaskLocked && PFocus >= Tuning.MaskRechargedFocus)
            {
                PMaskLocked = false;
                if (TTInput.Match) ShowToast("MASK READY", 0.8f);
            }
            PMatching = TTInput.Match && !PMaskLocked && PFocus > Tuning.MaskMinFocus;

            if (PMatching)
            {
                float delta = Mathf.Abs(PTemp - PAmbient);
                PTemp = TTMath.MoveToward(PTemp, PAmbient, Tuning.MaskPullRate * dt);
                PFocus -= dt * (Tuning.MaskFocusDrain + delta * Tuning.MaskFocusDrainPerDelta);
            }
            else
            {
                float metabolic = PAmbient + Tuning.DriftAmbientOffset +
                                  Mathf.Min(Tuning.DriftSpeedBonusMax, Mathf.Abs(pvx) / Tuning.DriftSpeedDivisor);
                PTemp = TTMath.MoveToward(PTemp, metabolic, Tuning.DriftRate * dt);
                float rechargeBase = PHidden ? Tuning.FocusRegenHidden : Tuning.FocusRegenOpen;
                float rechargeBonus = PHidden ? 0f : env.focusBonus;
                PFocus += Mathf.Max(0f, rechargeBase - env.dry + rechargeBonus) * dt;
            }

            if (env.cool > 0f) PTemp -= env.cool * dt;
            if (env.warm > 0f) PTemp += env.warm * dt;
            PTemp = TTMath.Clamp(PTemp, 0f, 100f);
            PFocus = TTMath.Clamp(PFocus, 0f, 100f);

            float pace = PaceScale();
            if (GroundPlay) GroundMove3D(dt, pace, env);
            else if (ClimbMode) ClimbMove(dt, pace, env);
            else SideMove(dt, pace, env);

            PAmbient = AmbientAt(px, py);
            PHidden = IsHiddenNow();

            if (Mathf.Abs(PAmbient - oldAmbient) > 2f && LevelTime > 0.3f)
                ShowToast("AIR SHIFT - " + PAmbient.ToString("0") + " deg", 1.6f);

            float thermalGap = Mathf.Abs(PTemp - PAmbient);
            if (PMatching && thermalGap <= Tuning.BlendLatchGap && !_blendLatched)
            {
                _blendLatched = true;
                ShowToast("THERMAL MASK LOCKED", 0.95f);
            }
            else if (thermalGap > Tuning.BlendBreakGap) _blendLatched = false;

            if (PHidden && !_hiddenLatched)
            {
                _hiddenLatched = true;
                ShowToast("BLIND SPOT - MASK RECHARGING", 1.2f);
            }
            else if (!PHidden) _hiddenLatched = false;

            if (TTInput.StrikeQueued)
            {
                TTInput.StrikeQueued = false;
                AttemptExecution();
            }
            ExecutionReady = ExecutionTargetAway() != null;
            if (ExecutionReady && !_executeHintShown)
            {
                _executeHintShown = true;
                ShowToast("TARGET IN REACH - UNSEEN - PRESS SHIFT", 1.7f);
            }

            var pr = PlayerRect(0f);
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type == TTObjectType.Thorn && Intersects(pr, ObjectRect(o, LevelTime)))
                {
                    CatchPlayer("a live cable");
                    break;
                }
            }
            // Falling off the world. Side play drops you down the canvas; ground play drops
            // you between the decks, which is the same death seen from a different angle.
            if (Mode == GameMode.Playing)
            {
                if (GroundPlay)
                {
                    if (PLift < -Tuning.FallDeathDepth) CatchPlayer("the fall");
                }
                else if (!ClimbMode && py > Settings.Height + Tuning.FallDeathDepth)
                {
                    CatchPlayer("the fall");
                }
            }

            if (Mode != GameMode.Playing) return;
            UpdateCameraDetection(dt);
            if (Mode != GameMode.Playing) return;
            UpdateEnemies(dt);
            if (Mode != GameMode.Playing) return;
            UpdateCollectiblesAndGoal();
        }

        /// <summary>Port of sideMove: buffered jump, coyote time, gate collision, swept one-way landing.</summary>
        void SideMove(float dt, float pace, Env env)
        {
            if (TTInput.JumpQueued)
            {
                PJumpBuffer = Tuning.JumpBufferTime;
                TTInput.JumpQueued = false;
            }
            else PJumpBuffer = Mathf.Max(0f, PJumpBuffer - dt);

            if (PGrounded) PCoyote = Tuning.CoyoteTime;
            else PCoyote = Mathf.Max(0f, PCoyote - dt);

            // Standing on a moving ledge carries you by its per-frame delta.
            if (PGroundIndex >= 0 && PGroundIndex < Objects.Count &&
                Objects[PGroundIndex].type == TTObjectType.MovingPlatform)
            {
                var ob = Objects[PGroundIndex];
                var now = ObjectRect(ob, LevelTime);
                var before = ObjectRect(ob, Mathf.Max(0f, LevelTime - dt));
                px += now.x - before.x;
                py += now.y - before.y;
            }

            int dir = (TTInput.Right ? 1 : 0) - (TTInput.Left ? 1 : 0);
            if (dir != 0) PFacing = dir;
            float maxSpeed = (PMatching ? Tuning.MaskedMaxSpeed : Tuning.MaxSpeed) * pace;
            float accel = (PGrounded ? Tuning.GroundAccel : Tuning.AirAccel) * pace;
            if (dir != 0) pvx = TTMath.MoveToward(pvx, dir * maxSpeed, accel * dt);
            else pvx = TTMath.MoveToward(pvx, 0f, (PGrounded ? Tuning.GroundFriction : Tuning.AirFriction) * dt);

            pvx += env.wind * dt;
            pvx = TTMath.Clamp(pvx, -Tuning.SpeedClamp, Tuning.SpeedClamp);
            PTemp = TTMath.Clamp(PTemp + Mathf.Abs(pvx) / Tuning.MoveHeatDivisor * Tuning.MoveHeatScale * dt, 0f, 100f);

            if (PJumpBuffer > 0f && PCoyote > 0f)
            {
                pvy = Tuning.JumpImpulse;
                PGrounded = false;
                PGroundIndex = -1;
                PCoyote = 0f;
                PJumpBuffer = 0f;
                PTemp = TTMath.Clamp(PTemp + Tuning.JumpHeat, 0f, 100f);
            }

            float oldX = px, oldY = py;
            px += pvx * dt;
            px = TTMath.Clamp(px, Tuning.LevelEdgePad, Mathf.Max(Tuning.LevelEdgePad, Settings.Width - Tuning.LevelEdgePad));

            // Closed thermal gates are solid walls.
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.ThermalGate) continue;
                bool open = GateOpen(o);
                GateStates[i] = open;
                if (open) continue;
                var r = ObjectRect(o, LevelTime);
                var pr = PlayerRect(0f);
                if (Intersects(pr, r))
                {
                    if (oldX + pw * 0.5f <= r.x + 5f) { px = r.x - pw * 0.5f; pvx = Mathf.Min(0f, pvx); }
                    else if (oldX - pw * 0.5f >= r.x + r.width - 5f) { px = r.x + r.width + pw * 0.5f; pvx = Mathf.Max(0f, pvx); }
                    else px = oldX;
                    if (_goalHintCooldown <= 0f)
                    {
                        _goalHintCooldown = 1f;
                        ShowToast("SEAL TARGET " + TTMath.Clamp(o.value, 0f, 100f).ToString("0") + " deg", 1f);
                    }
                }
            }

            bool wasGrounded = PGrounded;
            pvy = Mathf.Min(Tuning.TerminalFall, pvy + Tuning.Gravity * dt);
            py += pvy * dt;
            PGrounded = false;
            PGroundIndex = -1;

            float oldBottom = oldY + ph * 0.5f;
            float newBottom = py + ph * 0.5f;
            bool hasLanding = false;
            Solid landing = default;
            if (pvy >= 0f)
            {
                var solids = SolidList(LevelTime);
                for (int i = 0; i < solids.Count; i++)
                {
                    var s = solids[i];
                    var r = s.rect;
                    // One-way: you only land if your bottom edge was above the top last frame.
                    if (oldBottom <= r.y + Tuning.LandingTopTolerance && newBottom >= r.y &&
                        px + pw * Tuning.LandingHalfWidthFactor > r.x &&
                        px - pw * Tuning.LandingHalfWidthFactor < r.x + r.width)
                    {
                        if (!hasLanding || r.y < landing.rect.y) { landing = s; hasLanding = true; }
                    }
                }
            }
            if (hasLanding)
            {
                py = landing.rect.y - ph * 0.5f;
                pvy = 0f;
                PGrounded = true;
                PGroundIndex = landing.index;
            }
        }

        /// <summary>Port of climbMove: 8-way on a vertical wall, no gravity, per-axis blocking.</summary>
        void ClimbMove(float dt, float pace, Env env)
        {
            int dx = (TTInput.Right ? 1 : 0) - (TTInput.Left ? 1 : 0);
            int dy = (TTInput.Down ? 1 : 0) - (TTInput.Up ? 1 : 0);
            float maxSpeed = (PMatching ? Tuning.ClimbMaskedMaxSpeed : Tuning.ClimbMaxSpeed) * pace;
            float accel = (PMatching ? Tuning.ClimbMaskedAccel : Tuning.ClimbAccel) * pace;

            if (dx != 0 || dy != 0)
            {
                float len = TTMath.Hypot(dx, dy);
                if (len == 0f) len = 1f;
                pvx = TTMath.MoveToward(pvx, dx / len * maxSpeed, accel * dt);
                pvy = TTMath.MoveToward(pvy, dy / len * maxSpeed, accel * dt);
                if (dx != 0) PFacing = dx > 0 ? 1 : -1;
            }
            else
            {
                pvx = TTMath.MoveToward(pvx, 0f, Tuning.ClimbFriction * dt);
                pvy = TTMath.MoveToward(pvy, 0f, Tuning.ClimbFriction * dt);
            }

            pvx += env.wind * dt;
            pvx = TTMath.Clamp(pvx, -Tuning.ClimbSpeedClamp, Tuning.ClimbSpeedClamp);
            pvy = TTMath.Clamp(pvy, -Tuning.ClimbSpeedClamp, Tuning.ClimbSpeedClamp);

            float sp = TTMath.Hypot(pvx, pvy);
            PTemp = TTMath.Clamp(PTemp + sp / Tuning.ClimbHeatDivisor * Tuning.ClimbHeatScale * dt, 0f, 100f);

            float half = Tuning.ClimbHalfExtent;
            float nx = px + pvx * dt;
            if (!ClimbBlocked(nx, py, half, out _)) px = nx; else pvx = 0f;
            float ny = py + pvy * dt;
            if (!ClimbBlocked(px, ny, half, out _)) py = ny; else pvy = 0f;

            px = TTMath.Clamp(px, Tuning.ClimbEdgePad, Mathf.Max(Tuning.ClimbEdgePad, Settings.Width - Tuning.ClimbEdgePad));
            py = TTMath.Clamp(py, Tuning.ClimbEdgePad, Mathf.Max(Tuning.ClimbEdgePad, Settings.Height - Tuning.ClimbEdgePad));

            if (sp > 10f) PAngle = TTMath.AngleToward(PAngle, Mathf.Atan2(pvy, pvx), Tuning.ClimbAngleEase * dt);

            PGrounded = true;
            PGroundIndex = -1;
            PCoyote = Tuning.CoyoteTime;
            TTInput.JumpQueued = false;

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.ThermalGate) continue;
                GateStates[i] = GateOpen(o);
            }
        }

        /// <summary>
        /// Ground play. Movement across the floor is climbMove unchanged - the same 8-way
        /// acceleration, the same per-axis AABB blocking, the same heat and mask constants -
        /// because that controller was already top-down. What is new is the third axis:
        /// PLift carries the lizard off the floor so a hop can clear a low ledge, while a
        /// thermal gate stands too tall to jump and still has to be opened by matching it.
        /// </summary>
        void GroundMove3D(float dt, float pace, Env env)
        {
            float yaw = CameraRig != null ? CameraRig.InputYaw : 0f;
            float rawX = (TTInput.Right ? 1 : 0) - (TTInput.Left ? 1 : 0);
            float rawY = (TTInput.Down ? 1 : 0) - (TTInput.Up ? 1 : 0);

            // Camera-relative steering, so "forward" means forward on screen when the rig is
            // allowed to swing round behind the lizard. Fixed-yaw rigs report 0 and this is
            // the identity.
            float cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);
            float dx = rawX * cos - rawY * sin;
            float dy = rawX * sin + rawY * cos;

            float maxSpeed = (PMatching ? Tuning.ClimbMaskedMaxSpeed : Tuning.ClimbMaxSpeed) * pace;
            float accel = (PMatching ? Tuning.ClimbMaskedAccel : Tuning.ClimbAccel) * pace;

            float mag = TTMath.Hypot(dx, dy);
            if (mag > 0.0001f)
            {
                pvx = TTMath.MoveToward(pvx, dx / mag * maxSpeed, accel * dt);
                pvy = TTMath.MoveToward(pvy, dy / mag * maxSpeed, accel * dt);
                if (Mathf.Abs(dx) > 0.0001f) PFacing = dx > 0f ? 1 : -1;
            }
            else
            {
                pvx = TTMath.MoveToward(pvx, 0f, Tuning.ClimbFriction * dt);
                pvy = TTMath.MoveToward(pvy, 0f, Tuning.ClimbFriction * dt);
            }

            pvx += env.wind * dt;
            pvx = TTMath.Clamp(pvx, -Tuning.ClimbSpeedClamp, Tuning.ClimbSpeedClamp);
            pvy = TTMath.Clamp(pvy, -Tuning.ClimbSpeedClamp, Tuning.ClimbSpeedClamp);

            float sp = TTMath.Hypot(pvx, pvy);
            PTemp = TTMath.Clamp(PTemp + sp / Tuning.ClimbHeatDivisor * Tuning.ClimbHeatScale * dt, 0f, 100f);

            // ---- the hop ----
            if (TTInput.JumpQueued)
            {
                PJumpBuffer = Tuning.JumpBufferTime;
                TTInput.JumpQueued = false;
            }
            else PJumpBuffer = Mathf.Max(0f, PJumpBuffer - dt);

            if (PGrounded) PCoyote = Tuning.CoyoteTime;
            else PCoyote = Mathf.Max(0f, PCoyote - dt);

            if (PJumpBuffer > 0f && PCoyote > 0f)
            {
                // JumpImpulse is authored negative for a canvas whose y grows down; PLift
                // grows up, so the sign flips and every other jump constant still applies.
                PLiftVel = -Tuning.JumpImpulse;
                PJumpBuffer = 0f;
                PCoyote = 0f;
                PGrounded = false;
                PTemp = TTMath.Clamp(PTemp + Tuning.JumpHeat, 0f, 100f);
            }

            // ---- move across the plane first, so the ground test reads where we now are ----
            float half = Tuning.ClimbHalfExtent;
            float nx = px + pvx * dt;
            if (!GroundBlocked(nx, py, half, PLift)) px = nx; else pvx = 0f;
            float ny = py + pvy * dt;
            if (!GroundBlocked(px, ny, half, PLift)) py = ny; else pvy = 0f;

            px = TTMath.Clamp(px, Tuning.ClimbEdgePad, Mathf.Max(Tuning.ClimbEdgePad, Settings.Width - Tuning.ClimbEdgePad));
            py = TTMath.Clamp(py, Tuning.ClimbEdgePad, Mathf.Max(Tuning.ClimbEdgePad, Settings.Height - Tuning.ClimbEdgePad));

            // ---- then gravity, and land only from above ----
            float floor = SurfaceTopAt(px, py);
            float wasAt = PLift;
            PLiftVel = Mathf.Max(-Tuning.TerminalFall, PLiftVel - Tuning.Gravity * dt);
            PLift += PLiftVel * dt;

            // Negative infinity is open air, so the test below is false there and the fall
            // continues. The `wasAt` condition is what makes a gap lethal rather than
            // forgiving: drop past a deck's top and you cannot pop back up through it, you
            // can only come down onto one from above. Walking off an edge is a fall; the
            // way across is the hop, with coyote time to leave late.
            if (!float.IsNegativeInfinity(floor) && PLift <= floor && wasAt >= floor - 0.01f)
            {
                PLift = floor;
                PLiftVel = 0f;
                PGrounded = true;
            }
            else PGrounded = false;

            if (sp > 10f) PAngle = TTMath.AngleToward(PAngle, Mathf.Atan2(pvy, pvx), Tuning.ClimbAngleEase * dt);

            PGroundIndex = -1;

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.ThermalGate) continue;
                GateStates[i] = GateOpen(o);
            }
        }

        /// <summary>
        /// Ground-play blocking. Same AABB sweep as ClimbBlocked, except a solid you are
        /// currently hopping over stops counting. The clearance factor is deliberately under
        /// 1 so clearing a ledge means arcing over it rather than grazing the exact top.
        /// </summary>
        /// <summary>
        /// Height of the ground under a point, in source pixels, or negative infinity where
        /// there is nothing to stand on.
        ///
        /// A plan-authored level is a solid floor with walls on it, so the answer is always
        /// zero. A side-authored level is a set of decks over open air: you stand on a deck
        /// or you are falling. The test is the lizard's centre rather than its whole square,
        /// so walking off an edge drops you instead of leaving you hovering.
        /// </summary>
        public float SurfaceTopAt(float x, float y)
        {
            if (!GroundPlay || PlanAuthored) return 0f;

            float top = float.NegativeInfinity;
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.Platform && o.type != TTObjectType.MovingPlatform) continue;
                var r = ObjectRect(o, LevelTime);
                if (x >= r.x && x <= r.x + r.width && y >= r.y && y <= r.y + r.height)
                    top = Mathf.Max(top, TTView.DeckTopPixels);
            }
            return top;
        }

        /// <summary>True where the lizard would have nothing under it at all.</summary>
        public bool OverVoid(float x, float y) => float.IsNegativeInfinity(SurfaceTopAt(x, y));

        /// <summary>
        /// Pull a spawn or checkpoint onto the nearest deck. The authored starts sit just
        /// clear of the shelf the lizard used to be standing on, which was right in a side
        /// view and is a hole in plan - without this, every side level opens by falling.
        /// </summary>
        void SnapToSurface(ref float x, ref float y)
        {
            if (!GroundPlay || PlanAuthored || !OverVoid(x, y)) return;

            float inset = Tuning.ClimbHalfExtent + 4f;
            float best = float.MaxValue, bx = x, by = y;
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.Platform && o.type != TTObjectType.MovingPlatform) continue;
                var r = ObjectRect(o, LevelTime);
                if (r.width < inset * 2f || r.height < inset * 2f) continue;

                float cx = Mathf.Clamp(x, r.x + inset, r.x + r.width - inset);
                float cy = Mathf.Clamp(y, r.y + inset, r.y + r.height - inset);
                float d = TTMath.Hypot(cx - x, cy - y);
                if (d < best) { best = d; bx = cx; by = cy; }
            }
            if (best < float.MaxValue) { x = bx; y = by; }
        }

        bool GroundBlocked(float x, float y, float half, float lift)
        {
            var list = ClimbSolids();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i].rect;
                if (x + half <= r.x || x - half >= r.x + r.width ||
                    y + half <= r.y || y - half >= r.y + r.height) continue;

                // Every overlap gets tested, not just the first: hopping a low ledge must
                // not smuggle you through a gate that happens to overlap the same square.
                float topPixels = TTView.StandingHeight(list[i].type, PlanAuthored) * TTCoord.PixelsPerUnit;
                if (lift < topPixels * 0.85f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- flow

        public void ShowToast(string text, float duration)
        {
            Toast = text;
            ToastTimer = duration;
            LastEvent = text;
            if (LogEvents) Debug.Log("[ThermalTail] " + text);
        }

        public void CatchPlayer(string reason)
        {
            if (Mode != GameMode.Playing || Grace > 0f) return;
            LevelCatches++;
            TTSession.Lives--;
            TTSession.Score = Mathf.Max(0, TTSession.Score - (int)Tuning.CatchScorePenalty);
            Combo = 0; ComboTimer = 0f;
            CatchReason = reason;
            Mode = GameMode.Respawning;
            TransitionTimer = Tuning.RespawnTransition;
            MaxDetection = 1f;
            TTInput.Clear();
            ShowToast("COMPROMISED BY " + reason.ToUpperInvariant(), 1.5f);
        }

        public void Respawn()
        {
            if (TTSession.Lives <= 0)
            {
                Mode = GameMode.Failed;
                TransitionTimer = 0f;
                ShowToast("OPERATION FAILED - PRESS R", 3f);
                return;
            }
            float rx = CheckpointX, ry = CheckpointY;
            SnapToSurface(ref rx, ref ry);
            px = rx; py = ry;
            pvx = pvy = 0f;
            PLiftVel = 0f;
            PLift = GroundPlay ? Mathf.Max(0f, SurfaceTopAt(rx, ry)) : 0f;
            PAngle = -Mathf.PI * 0.5f;
            PGrounded = false;
            PGroundIndex = -1;
            PCoyote = Tuning.CoyoteTime;
            PAmbient = AmbientAt(px, py);
            PTemp = TTMath.Clamp(PAmbient + Tuning.RespawnTempOffset, 0f, 100f);
            PFocus = Mathf.Max(Tuning.RespawnFocusFloor, PFocus);
            PMatching = PMaskLocked = PHidden = false;
            for (int i = 0; i < CameraMeters.Length; i++) { CameraMeters[i] = 0f; CameraCooldowns[i] = 0f; }
            MaxDetection = 0f;
            DangerCamera = -1;
            Grace = Tuning.GraceRespawn;
            Mode = GameMode.Playing;
            CatchReason = "";
            ResetEnemies();
            StrikeCooldown = 0f; StrikeFlash = 0f;
            ExecutionReady = false;
            _blendLatched = _hiddenLatched = false;
            TTInput.Clear();
            ShowToast("Safehouse restored", 1.5f);
        }

        public void RestartLevel()
        {
            TTSession.Score = TTSession.LevelBaseScore;
            TTSession.Lives = Tuning.StartingLives;
            LoadLevel();
        }

        public void FinishLevel()
        {
            if (Mode != GameMode.Playing) return;
            bool clean = LevelCatches == 0;
            bool silent = AlertCount == 0;
            int bonus = Mathf.RoundToInt(
                Tuning.BonusBase +
                TTSession.Lives * Tuning.BonusPerLife +
                PFocus * Tuning.BonusPerFocus +
                Mathf.Max(0f, Tuning.BonusTimeBase - LevelTime * Tuning.BonusTimeRate) +
                (clean ? Tuning.BonusClean : 0f) +
                (silent ? Tuning.BonusSilent : 0f) +
                LevelExecutions * Tuning.BonusPerExecution);
            LastBonus = bonus;
            TTSession.Score += bonus;
            Mode = GameMode.LevelComplete;
            TransitionTimer = Tuning.LevelCompleteTransition;
            pvx = pvy = 0f;
            PLift = PLiftVel = 0f;
            PMatching = false;
            TTInput.Clear();
            ShowToast(Settings.LevelName + " COMPLETE - BONUS " + bonus, 2.5f);
        }

        void GoNext()
        {
            int next = Settings.LevelIndex + 1;
            if (!LevelCatalog.HasLevel(next))
            {
                Mode = GameMode.Victory;
                TransitionTimer = 0f;
                ShowToast("MISSION COMPLETE", 5f);
                return;
            }
            TTSession.Lives = Mathf.Min(Tuning.StartingLives, TTSession.Lives + 1);
            TTSession.LevelBaseScore = TTSession.Score;
            LevelCatalog.Load(next);
        }
    }
}
