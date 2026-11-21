using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Railroader.Modding;
using UI.Builder;
using UI.Extensions;


namespace CaseyJones
{
    // settings.json beside the dll, also edited in game (Preferences > Mods, SettingsSection.cs held by SettingsUi).
    internal sealed class Settings
    {
        // "1x", "1.5x", "2x" or "Unlimited" (Logic.Options); anything else reads as 1x. The host's choice rules (HostSetting.cs).
        public string curveSpeed = Logic.Options[Logic.Default];
        // Cars take any curve at speed (default off). The host's choice rules (HostSetting.cs).
        public bool ignoreCarCurveLimits = false;
    }

    // Railroader 0.6 entry point: the Curve speed setting (Hooks.cs), its world sharing (HostSetting.cs) and its feed (Api.cs).
    //
    // The class-load rule: Mono loads every class a method names when it compiles that method, before the
    // method's own try runs, so the lifecycle methods name only the BCL, Harmony and the loader's types and this mod's
    // game-free Settings, Logic, Live, Api and ModFeeds. Hooks, HostSetting and SettingsUi (the game's types) are reached only through the
    // NoInlining helpers below, each called inside a try. tools/nogame proves it with Railroader.dll unloadable.
    // Railroader.Modding's IModEntryPoint, IWorldLifecycle, IModContext (ModDirectory, Log) and IModLogger, as the
    // other house mods use them (map 2.18: OnModLoad at boot, OnWorldLoad after the restore, unloads in reverse order).
    public sealed class Main : IModEntryPoint, IWorldLifecycle
    {
        internal const string Version = "0.1.0";
        private const string HarmonyId = "ashenmare.casey-jones";
        private const string Name = "Casey Jones";
        internal static IModLogger Log;
        internal static Settings Settings = new Settings();
        private Harmony _harmony;
        private static bool _ready;   // the patch is in: the world callbacks may run

        public void OnModLoad(IModContext context)
        {
            // Assemblies never unload and statics survive a disable/enable: reset everything here.
            Log = context.Log;
            _ready = false;
            Live.Reset();
            Api.Reset();
            string settingsPath = Path.Combine(context.ModDirectory, "settings.json");
            Settings = LoadSettings(settingsPath);
            _harmony = new Harmony(HarmonyId);
            try { _harmony.UnpatchAll(HarmonyId); } catch (Exception) { }   // no double patch
            string missing;
            try { missing = ResolveGame(); }
            catch (Exception e)
            {
                Log.Warning(Name + ": the Curve speed setting isn't available on this game version (" + PathClean.CleanText(e.ToString(), 4000)
                            + "); it is ignored.");
                return;
            }
            if (missing != null)
            {
                Log.Warning(Name + ": the Curve speed setting isn't available on this game version (not found: " + missing
                            + "); it is ignored.");
                return;
            }
            try { PatchGame(_harmony); }
            catch (Exception e)
            {
                try { _harmony.UnpatchAll(HarmonyId); } catch (Exception) { }
                Log.Warning(Name + ": the Curve speed setting isn't available on this game version (the hook wouldn't go on: "
                            + PathClean.CleanText(e.ToString(), 4000) + "); it is ignored.");
                return;
            }
            _ready = true;
            Live.SetPatched(true);
            Live.SetOwn(Logic.Parse(Settings.curveSpeed));   // the saved choice, at load
            Live.SetOwnIgnore(Settings.ignoreCarCurveLimits);   // at load too
            // The feed goes on the shared list (ModFeeds.cs) so any mod can find and read it. ModFeeds and Api name no game type,
            // so this is safe here; a refusal (another mod holds the id) is one log line.
            try { if (!PublishFeed()) Log.Warning(Name + ": another mod already publishes the feed id \"" + Api.Feed + "\", so this one isn't listed"); }
            catch (Exception) { }
            Log.Info(Name + " " + Version + " loaded: curve speed " + Logic.Options[Live.Own] + ", ignore car curve limits " + (Live.OwnIgnore ? "on" : "off") + " (a host's settings rule in multiplayer)");
            try { StartSettings(_harmony, settingsPath); }
            catch (Exception e)
            {
                StopSettings();
                Log.Warning(Name + ": the setting isn't in Preferences > Mods on this game version (the mod works without it): "
                            + PathClean.CleanText(e.Message));
            }
        }

        // The off state first, so the hook is inert from here; then the patch off, in its own try.
        public void OnModUnload()
        {
            Live.Reset();
            try { ModFeeds.Withdraw(Api.Feed); } catch (Exception) { }
            _ready = false;
            try { UnregisterWorld(); } catch (Exception) { }
            StopSettings();
            try { _harmony?.UnpatchAll(HarmonyId); }
            catch (Exception e) { Log?.Warning(Name + ": couldn't take its hook off: " + PathClean.CleanText(e.Message)); }
            _harmony = null;
        }

        // After the save is restored: the host's value goes into the world, a client reads it.
        public void OnWorldLoad()
        {
            if (!_ready) return;
            try { RegisterWorld(); }
            catch (Exception e)
            {
                Live.LeaveWorld();   // no world value: 1x on this computer
                Log?.Warning(Name + ": couldn't share the setting with this world, so curve speed stays 1x here: " + PathClean.CleanText(e.Message));
            }
        }

        public void OnWorldUnload()
        {
            try { UnregisterWorld(); } catch (Exception) { }
            Live.LeaveWorld();
        }

        // The feed's entry on the shared list: Api.Read, with a line per top-level key (release/FEEDS.md has the full schema).
        private static bool PublishFeed()
        {
            System.Version v = typeof(Main).Assembly.GetName().Version;
            string version = v == null ? "" : v.Major + "." + v.Minor + "." + v.Build;
            var fields = new Dictionary<string, string>
            {
                { "active", "the Curve speed hook is on (the mod loaded and the game version allows it)" },
                { "curveSpeed", "the setting in force on this game now: 1x, 1.5x, 2x or Unlimited (1x outside a world; a client shows the host's)" },
                { "multiplier", "the factor in force: 1, 1.5 or 2; null for Unlimited (a ceiling, not a factor)" },
                { "unlimited", "true when the setting is Unlimited" },
                { "ignoreCarCurveLimits", "true when Ignore car curve limits is in force on this game now (the host's value on a client): cars take any curve at speed" },
                { "capMph", "the most a curve's scaled rating reaches, in mph (250), in every setting but 1x; null at 1x" },
            };
            return ModFeeds.Publish(Api.Feed, Api.Read, "Casey Jones", "Casey Jones", version, Api.Version, fields);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ResolveGame() => Hooks.Resolve();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchGame(Harmony harmony) => Hooks.Patch(harmony);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterWorld() => HostSetting.Register();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UnregisterWorld() => HostSetting.Unregister();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PublishWorld() => HostSetting.PublishNow();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StartSettings(Harmony harmony, string settingsPath) => SettingsUi.Start(harmony, settingsPath);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StopSettingsNow() => SettingsUi.Stop();

        private static void StopSettings()
        {
            try { StopSettingsNow(); } catch (Exception) { }
        }

        // After a save in Preferences > Mods: the same read as at load, from the file just written.
        internal static void Applied(JObject saved)
        {
            if (!_ready) return;   // the patch isn't in: the setting is ignored
            Settings = saved.ToObject<Settings>() ?? new Settings();
            int mode = Logic.Parse(Settings.curveSpeed);
            Live.SetOwn(mode);
            Live.SetOwnIgnore(Settings.ignoreCarCurveLimits);   // a save in Preferences
            try { PublishWorld(); }
            catch (Exception e) { Log?.Warning(Name + ": couldn't share the setting with this world: " + PathClean.CleanText(e.Message)); }
            Log?.Info(Name + ": curve speed " + Logic.Options[Live.Own] + ", ignore car curve limits " + (Live.OwnIgnore ? "on" : "off") + (Live.Client ? " (a client runs the host's settings)" : ""));
        }

        // Mono puts the file's whole path, and so the player's account name, into an IO exception's message, and Player.log
        // goes with bug reports: the logs below take it through PathClean.
        private static Settings LoadSettings(string path)
        {
            try
            {
                if (File.Exists(path)) return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path)) ?? new Settings();
            }
            catch (Exception e) { Log?.Warning(Name + ": couldn't read settings.json, using 1x: " + PathClean.CleanText(e.Message)); return new Settings(); }
            // No file yet: write one with the defaults. Never in the zip, so an update can't overwrite someone's choice;
            // an existing file (even an unreadable one) is never touched.
            Settings d = new Settings();
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(d, Formatting.Indented));
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Log?.Warning(Name + ": couldn't write a default settings.json (1x applies anyway): " + PathClean.CleanText(e.Message));
                try { File.Delete(path + ".tmp"); } catch (Exception) { }
            }
            return d;
        }
    }

    // Everything typed by SettingsSection.cs (and so by the game's UI) lives here, off Main, behind NoInlining methods called
    // inside a try, so a game update that breaks the section can't take the setting with it (the house pattern).
    internal static class SettingsUi
    {
        private static object _section;   // a SettingsSection
        private static readonly UIState<bool> ClientExpanded = new UIState<bool>(false);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Start(Harmony harmony, string settingsPath)
        {
            Stop();
            var fields = new List<SettingField>
            {
                SettingField.Choice(Logic.Key, "Curve speed", Logic.Options, Logic.Default, true, "Times the game's curve speed limit."),
                SettingField.Bool(Logic.IgnoreKey, "Ignore car curve limits (unrealistic)", false, true, "Long cars take sharp curves at the curve's speed."),
            };
            _section = new SettingsSection("Casey Jones", settingsPath, fields.ToArray(), Main.Applied,
                m => Main.Log?.Warning("Casey Jones: " + m),
                "In multiplayer the host's settings rule.");
            PreferencesModsTab.Add(harmony, Draw, m => Main.Log?.Warning("Casey Jones: " + m));
        }

        // On a client in a world: the host's value, read only. Anywhere else: the editable section.
        private static void Draw(UIPanelBuilder b)
        {
            if (!(Live.InWorld && Live.Client)) { (_section as SettingsSection)?.Build(b, true); return; }
            try
            {
                b.AddExpandable("Casey Jones", ClientExpanded, c =>
                {
                    int w = Live.World;
                    c.AddField("Curve speed", w >= 0 && w < Logic.Options.Length ? Logic.Options[w] + " (the host's setting)" : "1x until the host's setting arrives");
                    int wi = Live.WorldIgnore;
                    c.AddField("Ignore car curve limits", wi == 1 ? "On (the host's setting)" : wi == 0 ? "Off (the host's setting)" : "Off until the host's setting arrives");
                    c.AddLabel("<style=Footnote>The host's settings rule here.</style>");
                }, ExpandableStyle.SectionHeader);
            }
            catch (Exception e) { Main.Log?.Warning("Casey Jones: couldn't draw the host's setting: " + PathClean.CleanText(e.Message)); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Stop()
        {
            PreferencesModsTab.Remove();
            _section = null;
        }
    }
}
