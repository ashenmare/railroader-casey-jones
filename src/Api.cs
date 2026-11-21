using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CaseyJones
{
    // Casey Jones' public feed, in the ashenmare feed shape (release/FEEDS.md, shipped in the zip): other mods read it by
    // reflection or through the shared feed list (ModFeeds.cs), so none depends on this one.
    //  - Version (const int): the schema version. Within a version fields are only added; a change of meaning raises it.
    //  - Feed (const string): the stable feed id, "casey-jones".
    //  - Read(): { "api": 1, "feed": "casey-jones", "active": bool, "curveSpeed": string, "multiplier": float or null,
    //    "unlimited": bool, "capMph": float or null, "ignoreCarCurveLimits": bool }.
    //    Plain BCL data only, a new dictionary on every call, floats always finite. Never throws: on a failure it answers
    //    { "api", "feed", "error" }, logged once. Read only.
    // Read names only BCL types and this mod's game-free Logic and Live; the one log line goes through a NoInlining helper
    // (the house class-load rule: Main names loader types, and a type that fails to load throws at its caller).
    public static class Api
    {
        public const int Version = 1;
        public const string Feed = "casey-jones";

        // One log line per load; reset by Main.OnModLoad (statics survive a mod-set change).
        private static bool _logged;

        internal static void Reset() => _logged = false;

        public static object Read()
        {
            try
            {
                int mode = Live.Mode;
                return new Dictionary<string, object>
                {
                    { "api", Version },
                    { "feed", Feed },
                    { "active", Live.Patched },
                    { "curveSpeed", Logic.Options[mode] },
                    { "multiplier", Logic.Multiplier(mode) },
                    { "unlimited", mode == Logic.Unlimited },
                    { "capMph", Logic.CapFor(mode) },
                    { "ignoreCarCurveLimits", Live.Ignore },
                };
            }
            catch (Exception e)
            {
                string why;
                try { why = e.GetType().Name; }
                catch (Exception) { why = "the feed failed"; }
                if (!_logged)
                {
                    _logged = true;
                    try { LogOnce(why); } catch (Exception) { }
                }
                try { return new Dictionary<string, object> { { "api", Version }, { "feed", Feed }, { "error", why } }; }
                catch (Exception) { return null; }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogOnce(string why) => Main.Log?.Warning("Casey Jones: the feed couldn't answer: " + why);
    }
}
