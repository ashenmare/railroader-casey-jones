using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Game.Mow;
using Model;
using Model.Physics;
using Track;

namespace CaseyJones
{
    // Three postfixes. The first is on TrainMath.MaximumSpeedMphForCurve(float), the curve's rating, so every reader of it follows
    // together (the Auto Engineer's limit, a car's damage and derailment check, the track tooltip). The second is on the two-argument
    // overload, the equipment ramp: past a car's own curve limit the game's slowdown stays as it is at 1x (Logic.Ramp). Game types are
    // named only here and in HostSetting, reached through NoInlining helpers inside a try.
    // Every reader of the rating goes through the one-argument method: the two-argument overload calls it first, and the
    // Auto Engineer's limit, a car's check and the tooltip call one of the two.
    // The Auto Engineer then takes the lowest of that, the segment's limit and the posted class speed, and a car's limit is
    // Min(rating, the class speed). The class limit is outside this patch.
    // The two-argument overload: below the car's limit it returns the one-argument rating; at or past it,
    // Lerp(5, that rating, InverseLerp(limit + 15, limit, degrees)). The second postfix puts the game's UNSCALED rating (asked for by
    // calling the one-argument method with Live.Raw set, so no copy of the formula) into that blend, so a car that cannot take a curve
    // crawls as the game has it at every setting.
    // The third is on Car.CalculateBindingDueToCurvature, the drag a car takes on a curve sharper than its own limit; with Ignore car curve limits on
    // it is 0 (and the ramp postfix gives the scaled rating, no blend). Off, none of that runs: the game's own numbers.
    // Car.CalculateBindingDueToCurvature is a private instance method returning float: 0 while the curve is under the car's
    // MaximumTrackCurvature, else Weight x a speed sigmoid x a sigmoid of the excess; its one caller adds it to the retarding force. It is the
    // same car curve limit as the ramp, so Ignore car curve limits switches it off too. 99 bytes of IL, not AggressiveInlining.
    // The two readers of the rating for TRACK WEAR get the game's own rating at every setting (taking a curve faster than the game's
    // rating wears the track as it does in the game, the faster the more): TrackConditionController.CalculateWear (the wear a car's traffic does) and
    // TrackTrafficCapacity.Estimate(TrackSegment) (the MoW crews' estimate of how much traffic a segment takes). Each gets a prefix and a finalizer
    // that count this thread into Live.Raw around the whole call, so the first and second postfixes stand aside inside it (the game's number, the
    // game's own ramp); the finalizer runs on an exception too, so a throw cannot leave the flag on. A prefix/finalizer pair on the callers, not a call
    // of the game's method with Raw set: the callers are the code that computes wear, so every rating read inside them follows without a copy of
    // their formulas. The car's own curve limit part of wear (CalculateWearPerTon's mismatch multiplier) reads the car's limit and the track's
    // degrees, not the rating, and is untouched.
    // CalculateWear and TrackTrafficCapacity.Estimate are the only wear readers of the rating;
    // TrackWearWidget (a debug overlay) also reads it and shows the scaled one.
    internal static class Hooks
    {
        private static MethodInfo _target, _target2, _target3, _target4, _target5;

        // Finds both methods and checks each can carry a postfix. Returns null when all is well, else what is wrong (for the log).
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string Resolve()
        {
            _target = null;
            _target2 = null;
            _target3 = null;
            _target4 = null;
            _target5 = null;
            MethodInfo m = typeof(TrainMath).GetMethod("MaximumSpeedMphForCurve", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(float) }, null);
            if (m == null || m.ReturnType != typeof(float))
                return "TrainMath.MaximumSpeedMphForCurve(float)";
            string big = TooSmall(m, "TrainMath.MaximumSpeedMphForCurve(float)");
            if (big != null) return big;
            MethodInfo m2 = typeof(TrainMath).GetMethod("MaximumSpeedMphForCurve", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(float), typeof(float) }, null);
            if (m2 == null || m2.ReturnType != typeof(float))
                return "TrainMath.MaximumSpeedMphForCurve(float, float)";
            big = TooSmall(m2, "TrainMath.MaximumSpeedMphForCurve(float, float)");
            if (big != null) return big;
            MethodInfo m3 = typeof(Car).GetMethod("CalculateBindingDueToCurvature", BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (m3 == null || m3.ReturnType != typeof(float))
                return "Car.CalculateBindingDueToCurvature()";
            big = TooSmall(m3, "Car.CalculateBindingDueToCurvature()");
            if (big != null) return big;
            MethodInfo m4 = typeof(TrackConditionController).GetMethod("CalculateWear", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(float), typeof(bool), typeof(float), typeof(TrackSegment), typeof(Car), typeof(float) }, null);
            if (m4 == null || m4.ReturnType != typeof(float))
                return "TrackConditionController.CalculateWear()";
            big = TooSmall(m4, "TrackConditionController.CalculateWear()");
            if (big != null) return big;
            // TrackTrafficCapacity is internal: found by name in the game's assembly
            Type cap = typeof(TrackConditionController).Assembly.GetType("Game.Mow.TrackTrafficCapacity");
            MethodInfo m5 = cap == null ? null : cap.GetMethod("Estimate", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(TrackSegment) }, null);
            if (m5 == null)
                return "TrackTrafficCapacity.Estimate(TrackSegment)";
            big = TooSmall(m5, "TrackTrafficCapacity.Estimate(TrackSegment)");
            if (big != null) return big;
            _target = m;
            _target2 = m2;
            _target3 = m3;
            _target4 = m4;
            _target5 = m5;
            return null;
        }

        private static string TooSmall(MethodInfo m, string name)
        {
            MethodBody body = m.GetMethodBody();
            byte[] il = body == null ? null : body.GetILAsByteArray();
            if (il == null) return "the body of " + name;
            bool aggressive = (m.GetMethodImplementationFlags() & MethodImplAttributes.AggressiveInlining) != 0;
            if (Logic.MayInline(il.Length, aggressive))
                return name + " is small enough to be inlined into its callers (" + il.Length + " bytes of IL)";
            return null;
        }

        // Both or neither: a throw on the second leaves the first on, and Main takes every patch off (UnpatchAll) before it logs.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Patch(Harmony harmony)
        {
            if (_target == null || _target2 == null || _target3 == null || _target4 == null || _target5 == null) throw new InvalidOperationException("the targets weren't resolved");
            harmony.Patch(_target, postfix: new HarmonyMethod(typeof(Hooks), nameof(Postfix)));
            harmony.Patch(_target2, postfix: new HarmonyMethod(typeof(Hooks), nameof(PostfixRamp)));
            harmony.Patch(_target3, postfix: new HarmonyMethod(typeof(Hooks), nameof(PostfixBinding)));
            harmony.Patch(_target4, prefix: new HarmonyMethod(typeof(Hooks), nameof(WearEnter)), finalizer: new HarmonyMethod(typeof(Hooks), nameof(WearExit)));
            harmony.Patch(_target5, prefix: new HarmonyMethod(typeof(Hooks), nameof(WearEnter)), finalizer: new HarmonyMethod(typeof(Hooks), nameof(WearExit)));
        }

        // Runs on every call of the rating: the Auto Engineer's planner, every car each physics step, the tooltip. A volatile
        // read, and at 1x nothing else: the game's number is left alone. No game member is named, nothing allocates.
        internal static void Postfix(ref float __result)
        {
            int mode = Live.Mode;
            if (mode == Logic.X1 || Live.Raw) return;
            try { __result = Logic.Scale(__result, mode); }
            catch (Exception) { }
        }

        // Runs on every call of the equipment ramp. At 1x, and below a car's limit (where the scaled rating is the answer), nothing else.
        // Parameter names are the game's own (Harmony matches them).
        internal static void PostfixRamp(float curveDegrees, float equipmentCurveLimit, ref float __result)
        {
            bool ignore = Live.Ignore;
            if (Live.Mode == Logic.X1 && !ignore) return;
            if (Live.Raw) return;   // inside a wear reader: the game's own ramp
            try
            {
                if (!Logic.PastLimit(curveDegrees, equipmentCurveLimit)) return;
                __result = Logic.RampResult(ignore, Live.Mode, curveDegrees, equipmentCurveLimit, GameRating(curveDegrees));
            }
            catch (Exception) { }
        }

        // Runs for every car each physics step: one volatile read when the setting is off. On: no binding drag on a curve sharper than the car's limit.
        internal static void PostfixBinding(ref float __result)
        {
            if (Live.Ignore) __result = 0f;
        }

        // Around the two wear readers (see the note above the class): this thread wants the game's own rating until the finalizer.
        internal static void WearEnter() { Live.EnterRaw(); }
        internal static void WearExit() { Live.ExitRaw(); }

        // The game's own unscaled rating: the one-argument method, with this thread's count raised so its postfix stands aside.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GameRating(float curveDegrees)
        {
            Live.EnterRaw();
            try { return TrainMath.MaximumSpeedMphForCurve(curveDegrees); }
            finally { Live.ExitRaw(); }
        }
    }
}
