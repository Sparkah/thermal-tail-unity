using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Gameplay policy shared by sensors and UI; physics only supplies observations.</summary>
    public static class StealthRules
    {
        public static float Mismatch(float body, float ambient, float civilian, bool inCrowd, bool inBand)
        {
            float airGap = Mathf.Abs(body - ambient);
            float crowdGap = Mathf.Abs(body - civilian);
            return inCrowd ? crowdGap : inBand ? Mathf.Min(airGap, crowdGap) : airGap;
        }

        public static float IntegrateSuspicion(float current, bool visible, float gap, float dt,
            PrototypeSettings settings)
        {
            return IntegrateSuspicion(current, visible, gap, dt, settings.MatchTolerance,
                settings.SuspicionGain, settings.GainPerDegree, settings.SuspicionDecay);
        }

        public static float IntegrateSuspicion(float current, bool visible, float gap, float dt,
            float tolerance, float gain, float gainPerDegree, float decay)
        {
            float rate = visible && gap > tolerance
                ? gain + (gap - tolerance) * gainPerDegree
                : -decay;
            return Mathf.Clamp01(current + rate * dt);
        }

        public static bool ValidCode(string code)
        {
            if (code == null || code.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                if (code[i] < '0' || code[i] > '9') return false;
                for (int j = 0; j < i; j++) if (code[i] == code[j]) return false;
            }
            return true;
        }
    }
}
