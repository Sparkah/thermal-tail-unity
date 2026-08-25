using System.Collections.Generic;
using UnityEngine;

namespace ThermalTail
{
    public partial class ThermalDirector
    {
        // ---------------------------------------------------------------- lenses

        /// <summary>
        /// Port of cameraInfo(). The duty cycle, aim and threshold used to hide inside the
        /// label string; they are typed fields on the object now and produce the same numbers.
        /// </summary>
        public LensInfo CameraInfo(Obj o)
        {
            bool fast = o.sweep == SweepRate.Fast;
            bool slow = o.sweep == SweepRate.Slow;
            float period = fast ? Tuning.LensPeriodFast : (slow ? Tuning.LensPeriodSlow : Tuning.LensPeriodNormal);
            float activeFor = fast ? Tuning.LensActiveFast : (slow ? Tuning.LensActiveSlow : Tuning.LensActiveNormal);
            float phase = TTMath.Mod(LevelTime + o.index * Tuning.LensPhasePerIndex, period);
            float threshold = Mathf.Abs(o.value);
            if (threshold == 0f) threshold = Tuning.LensThresholdDefault;
            return new LensInfo
            {
                active = o.alwaysOn || phase < activeFor,
                phase = phase / period,
                direction = (int)o.facing,
                range = Mathf.Max(Tuning.LensRangeMin, Mathf.Abs(o.w)),
                height = Mathf.Max(Tuning.LensHeightMin, Mathf.Abs(o.h)),
                threshold = Mathf.Max(Tuning.LensThresholdMin, threshold)
            };
        }

        /// <summary>Port of climbCameraInfo(): a swinging cone with a base aim and sweep arc.</summary>
        public ClimbLensInfo ClimbCameraInfo(Obj o)
        {
            bool fast = o.sweep == SweepRate.Fast;
            bool slow = o.sweep == SweepRate.Slow;
            float period = fast ? Tuning.ClimbLensPeriodFast : (slow ? Tuning.ClimbLensPeriodSlow : Tuning.ClimbLensPeriodNormal);
            float radius = Mathf.Abs(o.w);
            if (radius == 0f) radius = Tuning.ClimbLensRadiusDefault;
            radius = Mathf.Max(Tuning.ClimbLensRadiusMin, radius);
            float spreadDeg = Mathf.Abs(o.h);
            if (spreadDeg == 0f) spreadDeg = Tuning.ClimbSpreadDefaultDeg;
            float spread = TTMath.Clamp(spreadDeg, Tuning.ClimbSpreadMinDeg, Tuning.ClimbSpreadMaxDeg) * Mathf.PI / 180f;

            float baseAim = Mathf.PI * 0.5f;
            if (o.climbAim == ClimbAim.Up) baseAim = -Mathf.PI * 0.5f;
            else if (o.climbAim == ClimbAim.Left) baseAim = Mathf.PI;
            else if (o.climbAim == ClimbAim.Right) baseAim = 0f;

            float swing = o.arc == SweepArc.Wide ? Tuning.SwingWide
                        : (o.arc == SweepArc.Narrow ? Tuning.SwingNarrow : Tuning.SwingNormal);
            float phase = TTMath.Mod(LevelTime + o.index * Tuning.ClimbLensPhasePerIndex, period) / period;
            float threshold = Mathf.Abs(o.value);
            if (threshold == 0f) threshold = Tuning.LensThresholdDefault;

            return new ClimbLensInfo
            {
                aim = baseAim + Mathf.Sin(phase * Mathf.PI * 2f) * swing,
                spread = spread,
                radius = radius,
                phase = phase,
                active = o.pulse
                    ? TTMath.Mod(LevelTime + o.index * Tuning.ClimbPulsePhasePerIndex, period) < period * Tuning.ClimbPulseDuty
                    : true,
                threshold = Mathf.Max(Tuning.LensThresholdMin, threshold)
            };
        }

        /// <summary>Port of cameraBeamHit(): an expanding wedge from the lens toward the floor.</summary>
        public bool CameraBeamHit(Obj o, LensInfo info, float x, float y, float pad)
        {
            float range = Mathf.Max(1f, info.range);
            float t = (x - o.x) * info.direction / range;
            if (t < 0f || t > 1f) return false;
            float apex = o.y + Tuning.BeamApexOffset;
            float top = apex + t * (o.y + info.height * Tuning.BeamTopFactor - apex);
            float bottom = apex + t * (o.y + info.height - apex);
            return y >= top - pad && y <= bottom + pad;
        }

        public bool ClimbBeamHit(Obj o, ClimbLensInfo info, float x, float y)
        {
            float dx = x - o.x, dy = y - o.y;
            if (TTMath.Hypot(dx, dy) > info.radius) return false;
            return Mathf.Abs(TTMath.Mod(Mathf.Atan2(dy, dx) - info.aim + Mathf.PI, Mathf.PI * 2f) - Mathf.PI) <= info.spread * 0.5f;
        }

        /// <summary>
        /// Port of updateCameraDetection(). The meter only climbs while the beam is on you
        /// AND you are thermally mismatched past the lens threshold.
        /// </summary>
        void UpdateCameraDetection(float dt)
        {
            MaxDetection = 0f;
            DangerCamera = -1;
            float mismatch = Mathf.Abs(PTemp - PAmbient);

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.Camera) continue;

                if (CameraCooldowns[i] > 0f)
                {
                    CameraCooldowns[i] = Mathf.Max(0f, CameraCooldowns[i] - dt);
                    CameraMeters[i] = TTMath.Clamp(CameraMeters[i] - dt * Tuning.DetectCooldownDecay, 0f, Tuning.DetectMeterMax);
                    continue;
                }

                bool seen = false;
                float threshold;
                if (ClimbMode)
                {
                    var info = ClimbCameraInfo(o);
                    threshold = info.threshold;
                    if (info.active && !PHidden && Grace <= 0f && ClimbBeamHit(o, info, px, py) && !LineBlocked(o.x, o.y, px, py))
                        seen = true;
                }
                else
                {
                    var info = CameraInfo(o);
                    threshold = info.threshold;
                    seen = info.active && !PHidden && Grace <= 0f && CameraBeamHit(o, info, px, py, Tuning.BeamPad);
                }

                if (seen && mismatch > threshold)
                    CameraMeters[i] = TTMath.Clamp(
                        CameraMeters[i] + dt * (Tuning.DetectGainBase + (mismatch - threshold) * Tuning.DetectGainPerMismatch),
                        0f, Tuning.DetectMeterMax);
                else
                    CameraMeters[i] = TTMath.Clamp(
                        CameraMeters[i] - dt * (seen ? Tuning.DetectDecaySeen : Tuning.DetectDecayUnseen),
                        0f, Tuning.DetectMeterMax);

                if (CameraMeters[i] > MaxDetection)
                {
                    MaxDetection = CameraMeters[i];
                    DangerCamera = i;
                }
                if (CameraMeters[i] >= 1f) TriggerCamera(i);
            }

            if (MaxDetection > Tuning.WarnThreshold && _warningCooldown <= 0f)
                _warningCooldown = Tuning.WarnCooldown;
        }

        /// <summary>
        /// Port of triggerCamera(). With no wardens on the level a trip is an outright catch;
        /// otherwise it drops a beacon and every warden within 1700 units converges on it.
        /// </summary>
        void TriggerCamera(int i)
        {
            var o = Objects[i];
            CameraMeters[i] = 0f;
            CameraCooldowns[i] = Tuning.LensCooldown;

            if (Enemies.Count == 0)
            {
                CatchPlayer("surveillance lens");
                return;
            }

            float ax, ay;
            if (ClimbMode)
            {
                var info = ClimbCameraInfo(o);
                ax = o.x + Mathf.Cos(info.aim) * info.radius * Tuning.AlarmAimClimbFactor;
                ay = o.y + Mathf.Sin(info.aim) * info.radius * Tuning.AlarmAimClimbFactor;
            }
            else
            {
                var info = CameraInfo(o);
                ax = o.x + info.direction * Mathf.Min(info.range * Tuning.AlarmAimRangeFactor, Tuning.AlarmAimRangeMax);
                ay = o.y + info.height * Tuning.AlarmAimHeightFactor;
            }
            ax = TTMath.Clamp(ax, 30f, Mathf.Max(30f, Settings.Width - 30f));
            ay = TTMath.Clamp(ay, 30f, Mathf.Max(30f, Settings.Height - 30f));

            Alarms.Add(new Alarm { x = ax, y = ay, timer = Tuning.AlarmDuration, max = Tuning.AlarmDuration });
            if (Alarms.Count > Tuning.MaxAlarms) Alarms.RemoveAt(0);

            AlertCount++;
            TTSession.Score = Mathf.Max(0, TTSession.Score - (int)Tuning.AlarmScorePenalty);
            Combo = 0; ComboTimer = 0f;

            int alerted = 0;
            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.dead) continue;
                if (TTMath.Hypot(e.x - ax, e.y - ay) < Tuning.AlarmRadius)
                {
                    e.state = GuardState.Chase;
                    e.tx = ax; e.ty = ay;
                    e.alert = Tuning.AlertOnAlarm;
                    alerted++;
                }
            }
            ShowToast("LENS TRIPPED - " + alerted + " WARDEN" + (alerted == 1 ? "" : "S") + " INBOUND", 1.9f);
        }

        // ---------------------------------------------------------------- wardens

        /// <summary>Port of platformBoundsAt(): the platform a warden was placed above.</summary>
        bool PlatformBoundsAt(float x, float y, out float left, out float right, out float top)
        {
            var solids = SolidList(0f);
            bool has = false;
            Rect best = default;
            for (int i = 0; i < solids.Count; i++)
            {
                var r = solids[i].rect;
                if (x >= r.x - 12f && x <= r.x + r.width + 12f && r.y >= y - 40f && r.y <= y + 280f)
                {
                    if (!has || r.y < best.y) { best = r; has = true; }
                }
            }
            if (!has) { left = right = top = 0f; return false; }
            left = best.x + Tuning.PlatformBoundsInset;
            right = Mathf.Max(best.x + Tuning.PlatformBoundsInset, best.x + best.width - Tuning.PlatformBoundsInset);
            top = best.y;
            return true;
        }

        void BuildEnemies()
        {
            Enemies.Clear();
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.Guard) continue;

                float span = Mathf.Abs(o.value);
                if (span == 0f) span = Tuning.GuardSpanDefault;
                span = TTMath.Clamp(span, Tuning.GuardSpanMin, Tuning.GuardSpanMax);
                bool vertical = ClimbMode && o.patrol == PatrolAxis.Vertical;
                float speed = o.guardSpeed == GuardSpeed.Fast ? Tuning.GuardSpeedFast
                            : (o.guardSpeed == GuardSpeed.Slow ? Tuning.GuardSpeedSlow : Tuning.GuardSpeedNormal);

                float w = Mathf.Abs(o.w); if (w == 0f) w = Tuning.GuardSizeDefault;
                float h = Mathf.Abs(o.h); if (h == 0f) h = Tuning.GuardSizeDefault;

                var e = new Enemy
                {
                    index = i,
                    homeX = o.x, homeY = o.y, x = o.x, y = o.y,
                    w = Mathf.Max(Tuning.GuardSizeMin, w),
                    h = Mathf.Max(Tuning.GuardSizeMin, h),
                    span = span,
                    vertical = vertical,
                    speed = speed,
                    dir = 1, face = 1,
                    angle = vertical ? Mathf.PI * 0.5f : 0f,
                    state = GuardState.Patrol,
                    tx = o.x, ty = o.y,
                    phase = _rng.Next() * 6.28f,
                    anim = _rng.Next() * 4f,
                    tf = o.tf
                };

                if (!ClimbMode)
                {
                    if (PlatformBoundsAt(o.x, o.y, out float l, out float r, out float t))
                    {
                        e.hasBounds = true;
                        e.boundsLeft = l; e.boundsRight = r; e.boundsTop = t;
                        e.y = t - e.h * 0.5f - 2f;
                        e.x = TTMath.Clamp(e.x, l, r);
                        e.homeX = e.x;
                        e.homeY = e.y;
                    }
                }
                Enemies.Add(e);
            }
        }

        void ResetEnemies()
        {
            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                e.x = e.homeX; e.y = e.homeY;
                e.vx = e.vy = 0f;
                e.dir = 1;
                e.state = GuardState.Patrol;
                e.spot = 0f;
                e.alert = 0f;
                e.searchTimer = 0f;
            }
            Alarms.Clear();
        }

        /// <summary>Port of updateEnemies(): patrol / search / chase / return, plus the spot meter.</summary>
        void UpdateEnemies(float dt)
        {
            if (Enemies.Count == 0) return;
            float pxx = px, pyy = py;
            bool blend = Mathf.Abs(PTemp - PAmbient) <= Tuning.BlendLatchGap;
            float rush = TTMath.Hypot(pvx, pvy);

            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.dead)
                {
                    if (e.fade > 0f) e.fade = Mathf.Max(0f, e.fade - dt);
                    continue;
                }
                e.anim += dt;

                if (e.state == GuardState.Chase || e.state == GuardState.Search)
                {
                    e.alert = Mathf.Max(0f, e.alert - dt);
                    if (e.alert == 0f)
                    {
                        e.state = GuardState.Return;
                        e.tx = e.homeX; e.ty = e.homeY;
                    }
                }

                float vx = 0f, vy = 0f;
                if (e.state == GuardState.Patrol)
                {
                    float along = e.vertical ? e.y - e.homeY : e.x - e.homeX;
                    if (along > e.span * 0.5f) e.dir = -1;
                    else if (along < -e.span * 0.5f) e.dir = 1;
                    if (e.vertical) vy = e.dir * e.speed; else vx = e.dir * e.speed;
                }
                else if (e.state == GuardState.Search)
                {
                    e.searchTimer -= dt;
                    float a = e.anim * Tuning.SearchWanderFrequency + e.phase;
                    vx = Mathf.Cos(a) * e.speed * Tuning.SearchSpeedMultiplier;
                    vy = ClimbMode ? Mathf.Sin(a * 1.3f) * e.speed * Tuning.SearchSpeedMultiplier : 0f;
                    if (e.searchTimer <= 0f)
                    {
                        e.state = GuardState.Return;
                        e.tx = e.homeX; e.ty = e.homeY;
                    }
                }
                else
                {
                    float ddx = e.tx - e.x;
                    float ddy = ClimbMode ? e.ty - e.y : 0f;
                    float d = Mathf.Max(1f, TTMath.Hypot(ddx, ddy));
                    float sp = e.speed * (e.state == GuardState.Chase ? Tuning.ChaseSpeedMultiplier : Tuning.ReturnSpeedMultiplier);
                    vx = ddx / d * sp;
                    vy = ddy / d * sp;
                    if (d < Tuning.ArriveDistance)
                    {
                        if (e.state == GuardState.Chase)
                        {
                            e.state = GuardState.Search;
                            e.searchTimer = Tuning.SearchDuration;
                        }
                        else
                        {
                            e.state = GuardState.Patrol;
                            e.spot = Mathf.Min(e.spot, 0.25f);
                        }
                    }
                }

                e.vx = vx; e.vy = vy;

                if (ClimbMode)
                {
                    float half = e.w * 0.5f;
                    float nx = e.x + vx * dt;
                    if (!ClimbBlocked(nx, e.y, half, out _)) e.x = nx;
                    else if (e.state == GuardState.Patrol && !e.vertical) e.dir *= -1;
                    float ny = e.y + vy * dt;
                    if (!ClimbBlocked(e.x, ny, half, out _)) e.y = ny;
                    else if (e.state == GuardState.Patrol && e.vertical) e.dir *= -1;
                    e.x = TTMath.Clamp(e.x, 30f, Mathf.Max(30f, Settings.Width - 30f));
                    e.y = TTMath.Clamp(e.y, 30f, Mathf.Max(30f, Settings.Height - 30f));
                }
                else
                {
                    e.x += vx * dt;
                    if (e.hasBounds)
                    {
                        if (e.x < e.boundsLeft) { e.x = e.boundsLeft; e.dir = 1; }
                        if (e.x > e.boundsRight) { e.x = e.boundsRight; e.dir = -1; }
                        e.y = e.boundsTop - e.h * 0.5f - 2f;
                    }
                    else e.x = TTMath.Clamp(e.x, 30f, Mathf.Max(30f, Settings.Width - 30f));
                }

                if (vx > 4f) e.face = 1;
                else if (vx < -4f) e.face = -1;
                if (ClimbMode && (Mathf.Abs(vx) > 2f || Mathf.Abs(vy) > 2f))
                    e.angle = TTMath.AngleToward(e.angle, Mathf.Atan2(vy, vx), Tuning.GuardAngleEase * dt);

                float dx = pxx - e.x, dy = pyy - e.y;
                float dist = TTMath.Hypot(dx, dy);
                bool alerted = e.state == GuardState.Chase || e.state == GuardState.Search;
                float sight = alerted ? Tuning.SightAlerted : Tuning.SightNormal;
                bool sees = false;
                if (Mode == GameMode.Playing && !PHidden && Grace <= 0f && dist < sight)
                {
                    if (ClimbMode)
                    {
                        float diff = Mathf.Abs(TTMath.Mod(Mathf.Atan2(dy, dx) - e.angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI);
                        sees = diff < Tuning.ClimbSightCone && !LineBlocked(e.x, e.y, pxx, pyy);
                    }
                    else sees = Mathf.Abs(dy) < Tuning.SideSightHalfHeight && dx * e.face > Tuning.SideSightBehind;
                }

                if (sees)
                {
                    float rate = (alerted ? Tuning.SpotRateAlerted : Tuning.SpotRateNormal)
                               * (blend ? Tuning.SpotBlendMultiplier : 1f)
                               * (1f + TTMath.Clamp(rush / Tuning.SpotRushDivisor, 0f, Tuning.SpotRushMax));
                    e.spot = TTMath.Clamp(e.spot + dt * rate, 0f, Tuning.SpotMax);
                    if (e.spot > Tuning.SpotWarnThreshold && _spotWarnCooldown <= 0f)
                        _spotWarnCooldown = Tuning.SpotWarnCooldown;
                    if (e.spot > Tuning.SpotChaseThreshold)
                    {
                        e.state = GuardState.Chase;
                        e.tx = pxx;
                        if (ClimbMode) e.ty = pyy;
                        e.alert = Mathf.Max(e.alert, Tuning.AlertOnSpot);
                    }
                }
                else e.spot = TTMath.Clamp(e.spot - dt * Tuning.SpotDecay, 0f, Tuning.SpotMax);

                if (Mode == GameMode.Playing && Grace <= 0f && !PHidden &&
                    (e.spot >= 1f || dist < Tuning.GuardCatchDistance))
                {
                    CatchPlayer(ClimbMode ? "wall warden" : "patrol warden");
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- takedown

        public bool StrikeReach(Enemy e)
        {
            float dx = px - e.x, dy = py - e.y;
            if (ClimbMode) return TTMath.Hypot(dx, dy) <= Tuning.ClimbStrikeReach;
            return Mathf.Abs(dx) <= Tuning.StrikeReachX && Mathf.Abs(dy) <= Tuning.StrikeReachY;
        }

        public bool FacesAway(Enemy e)
        {
            float dx = px - e.x, dy = py - e.y;
            if (ClimbMode)
            {
                float diff = Mathf.Abs(TTMath.Mod(Mathf.Atan2(dy, dx) - e.angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI);
                return diff > Tuning.ClimbFacesAwayAngle;
            }
            return dx * (e.face == 0 ? 1 : e.face) < Tuning.FacesAwayBehind;
        }

        /// <summary>Port of detected(): any live alarm, a hot meter, or a warden chasing/searching/suspicious.</summary>
        public bool Detected()
        {
            if (Enemies.Count == 0 && Alarms.Count == 0) return MaxDetection > Tuning.DetectedMeterThreshold;
            if (Alarms.Count > 0) return true;
            if (MaxDetection > Tuning.DetectedMeterThreshold) return true;
            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.dead) continue;
                if (e.state == GuardState.Chase || e.state == GuardState.Search) return true;
                if (e.spot > Tuning.DetectedSpotThreshold) return true;
            }
            return false;
        }

        /// <summary>The nearest legal takedown target: in reach, facing away, and you unseen.</summary>
        public Enemy ExecutionTargetAway()
        {
            if (Detected()) return null;
            Enemy best = null;
            float bestD = 1e9f;
            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.dead || !StrikeReach(e)) continue;
                if (!FacesAway(e)) continue;
                float d = TTMath.Hypot(px - e.x, py - e.y);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        Enemy ExecutionTargetAny()
        {
            Enemy any = null;
            float anyD = 1e9f;
            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.dead || !StrikeReach(e)) continue;
                float d = TTMath.Hypot(px - e.x, py - e.y);
                if (d < anyD) { anyD = d; any = e; }
            }
            return any;
        }

        public void AttemptExecution()
        {
            if (Mode != GameMode.Playing || StrikeCooldown > 0f) return;
            var away = ExecutionTargetAway();
            var any = ExecutionTargetAny();
            StrikeFlash = 0.34f;

            if (away != null)
            {
                away.dead = true;
                away.fade = 0.85f;
                away.spot = 0f;
                away.alert = 0f;
                away.state = GuardState.Patrol;
                away.vx = away.vy = 0f;
                LevelExecutions++;
                TTSession.Score += (int)Tuning.ExecutionScore;
                PTemp = TTMath.Clamp(PTemp + Tuning.ExecutionHeat, 0f, 100f);
                StrikeCooldown = Tuning.StrikeCooldownHit;
                ShowToast("WARDEN DOWN - SILENT TAKEDOWN +150", 1.35f);
            }
            else if (any != null)
            {
                StrikeCooldown = Tuning.StrikeCooldownBlown;
                ShowToast("COVER BLOWN - BREAK CONTACT TO TAKE DOWN", 1.3f);
            }
            else
            {
                StrikeCooldown = Tuning.StrikeCooldownWhiff;
                ShowToast("NO TARGET IN REACH", 0.7f);
            }
        }

        // ---------------------------------------------------------------- pickups and goal

        /// <summary>Port of checkpointSpawnFor(): snap the respawn onto the nearest platform top.</summary>
        void SetCheckpointFrom(Obj o)
        {
            if (ClimbMode)
            {
                CheckpointX = TTMath.Clamp(o.x + o.w * 0.5f, 30f, Mathf.Max(30f, Settings.Width - 30f));
                CheckpointY = TTMath.Clamp(o.y + o.h * 0.5f, 30f, Mathf.Max(30f, Settings.Height - 30f));
                return;
            }
            var solids = SolidList(LevelTime);
            var candidates = new List<Solid>();
            for (int i = 0; i < solids.Count; i++)
                if (solids[i].type == TTObjectType.Platform) candidates.Add(solids[i]);
            if (candidates.Count == 0) candidates = solids;

            float cx = o.x + o.w * 0.5f;
            float cy = o.y + o.h;
            bool has = false;
            float bx = 0f, by = 0f, bestCost = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var r = candidates[i].rect;
                float half = pw * 0.58f;
                float sx = r.width > half * 2f + 4f ? TTMath.Clamp(cx, r.x + half, r.x + r.width - half) : r.x + r.width * 0.5f;
                float distance = Mathf.Abs(sx - cx);
                float cost = distance + Mathf.Abs(r.y - cy) * 0.2f + (distance > 650f ? 10000f : 0f);
                if (!has || cost < bestCost)
                {
                    has = true; bestCost = cost;
                    bx = sx; by = r.y - ph * 0.5f - 2f;
                }
            }
            if (has)
            {
                CheckpointX = TTMath.Clamp(bx, 18f, Mathf.Max(18f, Settings.Width - 18f));
                CheckpointY = by;
            }
            else { CheckpointX = px; CheckpointY = py; }
        }

        void UpdateCollectiblesAndGoal()
        {
            var p = PlayerRect(Tuning.PickupPad);

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.Moth || Collected.Contains(i)) continue;
                var r = ObjectRect(o, LevelTime);
                float hover = Mathf.Sin(LevelTime * Tuning.MothHoverFrequency + i * Tuning.MothHoverPhasePerIndex) * Tuning.MothHoverAmplitude;
                float cx = r.x + r.width * 0.5f;
                float cy = r.y + r.height * 0.5f + hover;
                if (PointIn(cx, cy, p))
                {
                    Collected.Add(i);
                    Combo = ComboTimer > 0f ? Mathf.Min(Tuning.ComboMax, Combo + 1) : 1;
                    ComboTimer = Tuning.ComboWindow;
                    float baseValue = o.value == 0f ? Tuning.MothDefaultValue : o.value;
                    int gain = Mathf.RoundToInt(baseValue * Combo);
                    TTSession.Score += gain;
                    PFocus = TTMath.Clamp(PFocus + Tuning.MothFocusGain, 0f, 100f);
                    bool quotaReached = RequiredMoths > 0 && Collected.Count == RequiredMoths;
                    if (quotaReached) ShowToast("QUOTA MET - MATCH AIR AT THE DEN", 1.8f);
                    else ShowToast(Combo > 1 ? "GLOWMOTH CHAIN x" + Combo : "Glowmoth +" + gain, 1.35f);
                }
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.type != TTObjectType.Checkpoint || ActivatedCheckpoint == i) continue;
                var r = ObjectRect(o, LevelTime);
                if (Intersects(p, r))
                {
                    ActivatedCheckpoint = i;
                    SetCheckpointFrom(o);
                    TTSession.Score += (int)Tuning.CheckpointScore;
                    ShowToast("Scent mark set", 1.5f);
                }
            }

            float gdx = px - Settings.Goal.x;
            float gdy = (ClimbMode ? py : py + ph * 0.5f) - Settings.Goal.y;
            bool near = ClimbMode
                ? TTMath.Hypot(gdx, gdy) < Tuning.GoalNearClimb
                : (Mathf.Abs(gdx) < Tuning.GoalNearX && Mathf.Abs(gdy) < Tuning.GoalNearY);

            if (near)
            {
                bool mothsReady = Collected.Count >= RequiredMoths;
                bool thermalReady = Mathf.Abs(PTemp - PAmbient) <= Tuning.GoalThermalTolerance;
                if (mothsReady && thermalReady) FinishLevel();
                else if (_goalHintCooldown <= 0f)
                {
                    _goalHintCooldown = Tuning.GoalHintCooldown;
                    if (!mothsReady) ShowToast("DEN LOCKED - " + (RequiredMoths - Collected.Count) + " GLOWMOTHS NEEDED", 1.25f);
                    else ShowToast("MATCH AIR TO OPEN THE DEN", 1.25f);
                }
            }
        }

        // ---------------------------------------------------------------- view

        public float ViewTargetX()
        {
            if (ClimbMode) return px;
            return px + Tuning.CameraLeadSide;
        }

        public float ViewTargetY()
        {
            if (ClimbMode) return py;
            return py - Tuning.CameraLiftSide;
        }

        /// <summary>Drive Unity transforms from the simulation. Presentation only.</summary>
        public void SyncView()
        {
            if (PlayerView != null)
                PlayerView.localPosition = TTCoord.Point(px, py, pz);

            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.tf == null) continue;

                if (o.type == TTObjectType.MovingPlatform)
                {
                    var r = ObjectRect(o, LevelTime);
                    o.tf.localPosition = TTCoord.RectCenter(r.x, r.y, r.width, r.height, o.comp.ViewDepth * 0.5f);
                }
                else if (o.type == TTObjectType.Moth)
                {
                    bool taken = Collected.Contains(i);
                    if (o.tf.gameObject.activeSelf == taken) o.tf.gameObject.SetActive(!taken);
                    if (!taken)
                    {
                        float hover = Mathf.Sin(LevelTime * Tuning.MothHoverFrequency + i * Tuning.MothHoverPhasePerIndex) * Tuning.MothHoverAmplitude;
                        o.tf.localPosition = TTCoord.RectCenter(o.x, o.y + hover, o.w, o.h, o.comp.ViewDepth * 0.5f);
                    }
                }
                else if (o.type == TTObjectType.ThermalGate)
                {
                    bool open = i < GateStates.Length && GateStates[i];
                    if (o.rend != null) o.rend.enabled = !open;
                }
            }

            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.tf == null) continue;
                if (e.dead)
                {
                    if (e.tf.gameObject.activeSelf) e.tf.gameObject.SetActive(false);
                    continue;
                }
                e.tf.localPosition = TTCoord.RectCenter(e.x - e.w * 0.5f, e.y - e.h * 0.5f, e.w, e.h, 0.3f);
            }
        }
    }
}
