using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ThermalTail
{
    /// <summary>
    /// Mirrors the browser build's flat input record. Held state plus two queued
    /// edge-triggered actions, cleared by the simulation the frame it consumes them.
    ///
    /// Scripted overrides exist so play-mode smoke tests can drive the lizard without
    /// a real device; nothing in the shipping path sets them.
    /// </summary>
    public static class TTInput
    {
        public static bool Left, Right, Up, Down, Match;
        public static bool JumpQueued, StrikeQueued;

        /// <summary>When true, hardware polling is ignored and the fields above are driven externally.</summary>
        public static bool Scripted;

        public static void Clear()
        {
            Left = Right = Up = Down = Match = false;
            JumpQueued = StrikeQueued = false;
        }

        /// <summary>
        /// Key map from the shipping build's keyAction(): W/Space jump in side levels,
        /// W climbs and Space masks in climb levels, S masks in side levels.
        ///
        /// Ground play needs a fourth arrangement, because it is the only mode with both a
        /// full 8-way stick and a jump: WASD steers, Space hops, E masks, Shift strikes.
        /// </summary>
        public static void Poll(bool climbMode, bool groundPlay = false)
        {
            if (Scripted) return;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return;

            Left = k.aKey.isPressed || k.leftArrowKey.isPressed;
            Right = k.dKey.isPressed || k.rightArrowKey.isPressed;

            bool wHeld = k.wKey.isPressed || k.upArrowKey.isPressed;
            bool sHeld = k.sKey.isPressed || k.downArrowKey.isPressed;

            if (groundPlay)
            {
                Up = wHeld;
                Down = sHeld;
                Match = k.eKey.isPressed;
                if (k.spaceKey.wasPressedThisFrame) JumpQueued = true;
            }
            else if (climbMode)
            {
                Up = wHeld;
                Down = sHeld;
                Match = k.spaceKey.isPressed || k.eKey.isPressed;
                if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) { /* climb, held */ }
            }
            else
            {
                Up = false;
                Down = false;
                Match = sHeld || k.eKey.isPressed;
                if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                    JumpQueued = true;
            }

            if (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame)
                StrikeQueued = true;
#endif
        }

        public static bool PressedThisFrame(string key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (key)
            {
                case "r": return k.rKey.wasPressedThisFrame;
                case "p": return k.pKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame;
                case "l": return k.lKey.wasPressedThisFrame;
                case "n": return k.nKey.wasPressedThisFrame;
            }
#endif
            return false;
        }
    }
}
