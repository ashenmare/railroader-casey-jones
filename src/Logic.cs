using System;

namespace CaseyJones
{
    // The Curve speed decisions, game-free (tests/LogicCheck.cs compiles this file alone).
    internal static class Logic
    {
        // settings.json key and the dropdown's texts, in order; the index is the mode. The saved value is the text, but the host's world value
        // (HostSetting) is the INDEX: reorder or remove a choice and old saves remap silently, so choices may only be appended.
        internal const string Key = "curveSpeed";
        internal static readonly string[] Options = { "1x", "1.5x", "2x", "Unlimited" };
        internal const int X1 = 0, X1_5 = 1, X2 = 2, Unlimited = 3;
        internal const int Default = X1;
        // The second setting, a checkbox (default off): cars take any curve at speed. Its own settings.json key and its own world key.
        internal const string IgnoreKey = "ignoreCarCurveLimits";

        // The most a curve's scaled rating reaches in 1.5x, 2x and Unlimited (mph). Straight track keeps the game's own 1000
        // (TrainMath.MaximumSpeedMphForCurve returns it for 0 degrees), and a rating the game already gave above the cap is never lowered.
        internal const float CapMph = 250f;

        // The saved text as a mode: trimmed, any case, as the section's dropdown reads a choice. Anything else is 1x.
        internal static int Parse(string text)
        {
            if (text == null) return Default;
            string s = text.Trim();
            for (int i = 0; i < Options.Length; i++)
                if (string.Equals(Options[i], s, StringComparison.OrdinalIgnoreCase)) return i;
            return Default;
        }

        // The curve's rating under a mode. 1x: the game's own number, untouched. 1.5x and 2x multiply it, capped at 250 mph;
        // Unlimited is 250. Never below the game's own: straight track (1000) and a curve under about 0.4 degrees keep theirs.
        internal static float Scale(float rating, int mode)
        {
            switch (mode)
            {
                case X1_5: return Math.Max(rating, Math.Min(rating * 1.5f, CapMph));
                case X2: return Math.Max(rating, Math.Min(rating * 2f, CapMph));
                case Unlimited: return Math.Max(rating, CapMph);
                default: return rating;
            }
        }

        // The feed's "multiplier": 1, 1.5, 2; none (null) for Unlimited, which is a ceiling and not a factor. A mode out of range is 1x.
        internal static float? Multiplier(int mode)
        {
            switch (mode)
            {
                case X1_5: return 1.5f;
                case X2: return 2f;
                case Unlimited: return null;
                default: return 1f;
            }
        }

        // The feed's "capMph": the ceiling in every mode but 1x (null there, and for a mode out of range).
        internal static float? CapFor(int mode) => mode == X1_5 || mode == X2 || mode == Unlimited ? CapMph : (float?)null;

        // The equipment ramp, the game's own formula (TrainMath.MaximumSpeedMphForCurve(degrees, limit)): a curve at or past a car's
        // curve limit blends from the game's UNSCALED rating down to 5 mph 15 degrees past the limit. The comparison is the game's
        // (a NaN counts as past), and Lerp and InverseLerp are Unity's Mathf with the same clamping.
        internal static bool PastLimit(float curveDegrees, float equipmentCurveLimit) => !(curveDegrees < equipmentCurveLimit);

        internal static float Ramp(float curveDegrees, float equipmentCurveLimit, float gameRating) =>
            Lerp(5f, gameRating, InverseLerp(equipmentCurveLimit + 15f, equipmentCurveLimit, curveDegrees));

        // What the equipment ramp's postfix answers for a curve at or past a car's limit. Ignore off: the game's blend from its unscaled rating.
        // Ignore on: no blend, the scaled one-argument rating (at 1x that is the game's own rating).
        internal static float RampResult(bool ignore, int mode, float curveDegrees, float equipmentCurveLimit, float gameRating) =>
            ignore ? Scale(gameRating, mode) : Ramp(curveDegrees, equipmentCurveLimit, gameRating);

        // The Ignore car curve limits setting in force: outside a world off; the host runs its own, a client the host's value (off until it arrives).
        internal static bool EffectiveIgnore(bool own, bool inWorld, bool client, int world) => inWorld && (client ? world == 1 : own);

        private static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;

        // The mode this computer runs. Outside a world: 1x. In a world, the host runs its own setting; a client runs the
        // host's published value, or 1x when none has arrived (a client's own setting changes nothing).
        internal static int Effective(int own, bool inWorld, bool client, int world)
        {
            if (!inWorld) return Default;
            if (!client) return Clamp(own);
            return world >= 0 && world < Options.Length ? world : Default;
        }

        internal static int Clamp(int mode) => mode >= 0 && mode < Options.Length ? mode : Default;

        // Mono inlines a method whose IL is under 20 bytes, or any marked AggressiveInlining (mini's inline_limit, 20). A patched
        // method that could be inlined would never see its patch in the callers that inlined it.
        internal const int InlineLimitBytes = 20;
        internal static bool MayInline(int ilBytes, bool aggressive) => aggressive || ilBytes < InlineLimitBytes;
    }

    // The mode the postfix reads: one volatile int, recomputed on an event (a settings load or save, a world load or unload,
    // the host's value arriving), never on the hook. Game-free, so the lifecycle may name it.
    internal static class Live
    {
        private static volatile int _mode;
        private static volatile int _own;
        private static volatile int _world = -1;
        private static volatile bool _inWorld;
        private static volatile bool _client;
        private static volatile bool _patched;
        private static volatile bool _ownIgnore;
        private static volatile int _worldIgnore = -1;   // the host's value: 1 on, 0 off, -1 none
        private static volatile bool _ignore;
        // Counts, on this thread, the places that want the game's own unscaled rating: the ramp's postfix asking for it (Hooks.GameRating) and the
        // two wear readers (Hooks.WearEnter and WearExit). While it is above 0 the rating's and the ramp's postfixes stand aside. A count and not a
        // flag so an inner exit cannot clear an outer entry.
        [ThreadStatic] private static int _raw;
        internal static bool Raw => _raw > 0;
        internal static void EnterRaw() { _raw++; }
        internal static void ExitRaw() { if (_raw > 0) _raw--; }

        internal static int Mode => _mode;
        internal static int Own => _own;
        internal static int World => _world;
        internal static bool Client => _client;
        internal static bool InWorld => _inWorld;
        internal static bool OwnIgnore => _ownIgnore;
        internal static int WorldIgnore => _worldIgnore;
        internal static bool Ignore => _ignore;   // Ignore car curve limits is in force on this computer
        internal static bool Patched => _patched;   // the rating's postfix is on (the feed's "active")

        internal static void Reset() { _own = Logic.Default; _ownIgnore = false; _worldIgnore = -1; _world = -1; _inWorld = false; _client = false; _patched = false; Recompute(); }
        internal static void SetPatched(bool on) { _patched = on; }
        internal static void SetOwn(int mode) { _own = Logic.Clamp(mode); Recompute(); }
        internal static void SetWorld(int mode) { _world = mode; Recompute(); }
        internal static void SetOwnIgnore(bool on) { _ownIgnore = on; Recompute(); }
        internal static void SetWorldIgnore(int v) { _worldIgnore = v; Recompute(); }
        internal static void EnterWorld(bool client) { _inWorld = true; _client = client; Recompute(); }
        internal static void LeaveWorld() { _inWorld = false; _client = false; _world = -1; _worldIgnore = -1; Recompute(); }
        private static void Recompute()
        {
            _mode = Logic.Effective(_own, _inWorld, _client, _world);
            _ignore = Logic.EffectiveIgnore(_ownIgnore, _inWorld, _client, _worldIgnore);
        }
    }
}
