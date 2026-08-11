using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Every tuning number in the simulation, copied from the shipping browser build
    /// (pinned bytes at output/pit-thermal-tail-pin-20260806/kv_genblob_...).
    /// Units are source pixels and seconds, exactly as authored there.
    /// </summary>
    [CreateAssetMenu(menuName = "Thermal Tail/Thermal Tuning", fileName = "ThermalTuning")]
    public class ThermalTuning : ScriptableObject
    {
        [Header("Simulation step")]
        public float FixedStep = 1f / 60f;
        public float MaxFrameElapsed = 0.05f;

        [Header("Thermal mask")]
        public float MaskPullRate = 32f;              // moveToward(temp, ambient, 32*dt)
        public float MaskFocusDrain = 14f;            // focus -= dt*(14 + delta*.13)
        public float MaskFocusDrainPerDelta = 0.13f;
        public float MaskDepleteFocus = 0.25f;        // locks below this
        public float MaskRechargedFocus = 24f;        // unlocks at this
        public float MaskMinFocus = 0.2f;             // matching requires focus > .2
        public float MaskReleaseFocus = 4f;           // releasing the key clears the lock at >= 4
        public float BlendLatchGap = 8.5f;            // "THERMAL MASK LOCKED"
        public float BlendBreakGap = 11.5f;

        [Header("Passive thermal drift")]
        public float DriftRate = 4.2f;                // toward ambient + 10 + min(5,|vx|/45)
        public float DriftAmbientOffset = 10f;
        public float DriftSpeedBonusMax = 5f;
        public float DriftSpeedDivisor = 45f;

        [Header("Focus regeneration")]
        public float FocusRegenHidden = 20f;
        public float FocusRegenOpen = 2.5f;
        public float FocusBonusCool = 8f;
        public float FocusBonusWarm = 7f;
        public float FocusBonusShelter = 14f;
        public float MothFocusGain = 20f;

        [Header("Environment volumes")]
        public float CoolRateDefault = 24f;
        public float WarmRateDefault = 25f;
        public float GateTolerance = 6.5f;            // |temp - gate.value| <= 6.5 opens
        public float GoalThermalTolerance = 10f;      // |temp - ambient| <= 10 to exfil

        [Header("Pace (temperature drives speed)")]
        public float PaceBase = 0.55f;                // clamp(.55 + temp*.0095, .55, 1.4)
        public float PacePerTemp = 0.0095f;
        public float PaceMin = 0.55f;
        public float PaceMax = 1.4f;

        [Header("Player body")]
        public float PlayerWidth = 72f;
        public float PlayerHeight = 48f;
        public float ClimbHalfExtent = 27f;

        [Header("Side movement")]
        public float Gravity = 1250f;
        public float TerminalFall = 780f;
        public float JumpImpulse = -535f;
        public float GroundAccel = 940f;
        public float AirAccel = 620f;
        public float GroundFriction = 1050f;
        public float AirFriction = 220f;
        public float MaxSpeed = 205f;
        public float MaskedMaxSpeed = 112f;
        public float SpeedClamp = 300f;
        public float CoyoteTime = 0.12f;
        public float JumpBufferTime = 0.14f;
        public float LandingTopTolerance = 10f;       // oldBottom <= r.y + 10
        public float LandingHalfWidthFactor = 0.36f;
        public float JumpHeat = 1.2f;
        public float MoveHeatDivisor = 205f;
        public float MoveHeatScale = 0.5f;
        public float LevelEdgePad = 18f;
        public float FallDeathDepth = 220f;

        [Header("Climb movement")]
        public float ClimbMaxSpeed = 196f;
        public float ClimbMaskedMaxSpeed = 118f;
        public float ClimbAccel = 1320f;
        public float ClimbMaskedAccel = 1000f;
        public float ClimbFriction = 1500f;
        public float ClimbSpeedClamp = 320f;
        public float ClimbHeatDivisor = 196f;
        public float ClimbHeatScale = 0.55f;
        public float ClimbAngleEase = 9f;
        public float ClimbEdgePad = 30f;

        [Header("Surveillance lens - side")]
        public float LensPeriodFast = 3.55f;
        public float LensPeriodSlow = 6.2f;
        public float LensPeriodNormal = 5f;
        public float LensActiveFast = 2.7f;
        public float LensActiveSlow = 4.75f;
        public float LensActiveNormal = 3.75f;
        public float LensPhasePerIndex = 0.713f;
        public float LensRangeMin = 80f;
        public float LensHeightMin = 100f;
        public float LensThresholdDefault = 9f;
        public float LensThresholdMin = 2f;
        public float BeamApexOffset = 25f;
        public float BeamTopFactor = 0.34f;
        public float BeamPad = 12f;

        [Header("Surveillance lens - climb")]
        public float ClimbLensPeriodFast = 4.4f;
        public float ClimbLensPeriodSlow = 8.2f;
        public float ClimbLensPeriodNormal = 6.2f;
        public float ClimbLensRadiusMin = 140f;
        public float ClimbLensRadiusDefault = 420f;
        public float ClimbSpreadDefaultDeg = 70f;
        public float ClimbSpreadMinDeg = 24f;
        public float ClimbSpreadMaxDeg = 150f;
        public float SwingWide = 0.95f;
        public float SwingNarrow = 0.35f;
        public float SwingNormal = 0.66f;
        public float ClimbLensPhasePerIndex = 0.83f;
        public float ClimbPulsePhasePerIndex = 0.53f;
        public float ClimbPulseDuty = 0.62f;

        [Header("Detection integrator")]
        public float DetectGainBase = 0.3f;           // dt*(.3 + (mismatch-threshold)*.034)
        public float DetectGainPerMismatch = 0.034f;
        public float DetectDecaySeen = 0.85f;
        public float DetectDecayUnseen = 0.5f;
        public float DetectMeterMax = 1.2f;
        public float DetectCooldownDecay = 1.6f;
        public float LensCooldown = 3.4f;
        public float WarnThreshold = 0.28f;
        public float WarnCooldown = 0.56f;

        [Header("Alarm propagation")]
        public float AlarmRadius = 1700f;
        public float AlarmDuration = 8.5f;
        public int MaxAlarms = 3;
        public float AlarmScorePenalty = 60f;
        public float AlarmAimRangeFactor = 0.4f;
        public float AlarmAimRangeMax = 220f;
        public float AlarmAimHeightFactor = 0.86f;
        public float AlarmAimClimbFactor = 0.45f;

        [Header("Wardens")]
        public float GuardSpanDefault = 220f;
        public float GuardSpanMin = 60f;
        public float GuardSpanMax = 1400f;
        public float GuardSpeedFast = 76f;
        public float GuardSpeedSlow = 44f;
        public float GuardSpeedNormal = 58f;
        public float GuardSizeMin = 34f;
        public float GuardSizeDefault = 54f;
        public float SightNormal = 172f;
        public float SightAlerted = 250f;
        public float SideSightHalfHeight = 126f;
        public float SideSightBehind = -34f;
        public float ClimbSightCone = 1.2f;
        public float SpotRateAlerted = 1.35f;
        public float SpotRateNormal = 0.85f;
        public float SpotBlendMultiplier = 0.55f;
        public float SpotRushDivisor = 300f;
        public float SpotRushMax = 0.5f;
        public float SpotMax = 1.3f;
        public float SpotChaseThreshold = 0.34f;
        public float SpotWarnThreshold = 0.4f;
        public float SpotWarnCooldown = 0.75f;
        public float SpotDecay = 0.8f;
        public float GuardCatchDistance = 40f;
        public float ChaseSpeedMultiplier = 1.9f;
        public float ReturnSpeedMultiplier = 1.5f;
        public float SearchSpeedMultiplier = 1.05f;
        public float SearchWanderFrequency = 1.6f;
        public float ArriveDistance = 54f;
        public float SearchDuration = 3.4f;
        public float AlertOnSpot = 5.5f;
        public float AlertOnAlarm = 9f;
        public float GuardAngleEase = 8f;
        public float PlatformBoundsInset = 38f;

        [Header("Silent takedown")]
        public float StrikeReachX = 118f;
        public float StrikeReachY = 84f;
        public float ClimbStrikeReach = 98f;
        public float FacesAwayBehind = -20f;
        public float ClimbFacesAwayAngle = 1.75f;
        public float DetectedMeterThreshold = 0.25f;
        public float DetectedSpotThreshold = 0.3f;
        public float ExecutionScore = 150f;
        public float ExecutionHeat = 5f;
        public float StrikeCooldownHit = 0.55f;
        public float StrikeCooldownBlown = 0.7f;
        public float StrikeCooldownWhiff = 0.34f;

        [Header("Moving ledges")]
        public float LedgeSpeedFast = 1.55f;
        public float LedgeSpeedDefault = 0.82f;
        public float LedgeAmplitudeMax = 600f;
        public float LedgePhasePerIndex = 1.137f;

        [Header("Level flow")]
        public int StartingLives = 3;
        public float GraceInitial = 3.35f;
        public float GraceRespawn = 3.25f;
        public float RespawnFocusFloor = 65f;
        public float RespawnTempOffset = 4f;
        public float CatchScorePenalty = 80f;
        public float RespawnTransition = 1.05f;
        public float LevelCompleteTransition = 2.45f;
        public float CheckpointScore = 40f;
        public int ComboMax = 5;
        public float ComboWindow = 4.3f;
        public float MothDefaultValue = 100f;
        public float MothHoverAmplitude = 11f;
        public float MothHoverFrequency = 3f;
        public float MothHoverPhasePerIndex = 1.7f;
        public float PickupPad = 14f;                 // playerRect(14)
        public float EnvironmentPad = 8f;             // playerRect(8)
        public float HiddenPad = 2f;                  // playerRect(2)
        public float GoalNearX = 82f;
        public float GoalNearY = 145f;
        public float GoalNearClimb = 95f;
        public float GoalHintCooldown = 1.3f;

        [Header("Score bonus")]
        public float BonusBase = 220f;
        public float BonusPerLife = 80f;
        public float BonusPerFocus = 1.5f;
        public float BonusTimeBase = 450f;
        public float BonusTimeRate = 3f;
        public float BonusClean = 300f;
        public float BonusSilent = 200f;
        public float BonusPerExecution = 60f;

        [Header("View")]
        public float CameraLeadSide = 180f;
        public float CameraLiftSide = 95f;
        public float CameraFollowKx = 6.5f;
        public float CameraFollowKy = 4.2f;

        /// <summary>clamp(.55 + temp*.0095, .55, 1.4)</summary>
        public float PaceScale(float temp)
            => TTMath.Clamp(PaceBase + temp * PacePerTemp, PaceMin, PaceMax);
    }
}
