using System;
using System.Runtime.CompilerServices;
using Game.AccessControl;
using Game.State;
using KeyValue.Runtime;

namespace CaseyJones
{
    // The host's Curve speed, shared through the world. A KeyValue object of this mod's own, registered with the game's
    // StateManager the way the game registers its own world objects (a KeyValue object, RegisterPropertyObject with HostOnly
    // writes): the game sends the host's write to every client and into the snapshot a joining client gets. Only the host
    // writes it, from its own settings, at every world load and every save of the setting; a client only reads it, and runs
    // that value (or 1x until it arrives), so a client's own setting changes nothing. The Auto Engineer and the car physics run
    // on the host; the value matters on a client for its tooltip.
    internal static class HostSetting
    {
        internal const string ObjectId = "ashenmare.casey-jones";
        internal const string ModeKey = "curveSpeed";
        internal const string IgnoreKey = Logic.IgnoreKey;   // its own key in the same object, appended: the curve speed key never moves

        private static KeyValueStorage _world;
        private static IDisposable _observer, _observer2;

        // The world value as the host wrote it: an Int that is one of the choices; anything else is none (-1).
        internal static int ModeOf(Value v)
            => v.Type == KeyValue.Runtime.ValueType.Int && v.IntValue >= 0 && v.IntValue < Logic.Options.Length ? v.IntValue : -1;

        // The host's Ignore value: a Bool, 1 on or 0 off; anything else is none (-1).
        internal static int IgnoreOf(Value v) => v.Type == KeyValue.Runtime.ValueType.Bool ? (v.BoolValue ? 1 : 0) : -1;

        // OnWorldLoad. Runs after the game restored the save's or the host's snapshot properties: a value restored before
        // this object registered is handed to it as it registers.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Register()
        {
            Detach();   // only this mod's references: unregistering here would throw away what the game just restored
            var kv = new KeyValueStorage();
            StateManager.Shared.RegisterPropertyObject(ObjectId, kv, AuthorizationRequirement.HostOnly);
            Attach(kv, !StateManager.IsHost);
            Publish(kv, StateManager.IsHost, Live.Own, Live.OwnIgnore);
        }

        // OnWorldUnload (the game also drops every registration when the map unloads).
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Unregister()
        {
            bool had = _world != null;
            Detach();
            if (had) StateManager.Shared?.UnregisterPropertyObject(ObjectId);
        }

        // On the host: write this machine's setting into the world. A no-op on a client (a client's write would be rejected)
        // and when unchanged (KeyValueStorage.Set).
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void PublishNow()
        {
            KeyValueStorage kv = _world;
            if (kv == null) return;
            Publish(kv, StateManager.IsHost, Live.Own, Live.OwnIgnore);
        }

        // The write itself and the attach are split from the game's StateManager so tools/gamecheck can run them on a real
        // KeyValueStorage (the game's own class) as host and as client.
        internal static void Publish(KeyValueStorage kv, bool host, int own, bool ignore)
        {
            if (!host) return;
            kv[ModeKey] = Value.Int(Logic.Clamp(own));
            kv[IgnoreKey] = Value.Bool(ignore);
        }

        internal static void Attach(KeyValueStorage kv, bool client)
        {
            _world = kv;
            Live.EnterWorld(client);
            _observer = kv.Observe(ModeKey, v => Live.SetWorld(v.IsNull ? -1 : ModeOf(v)), true);
            _observer2 = kv.Observe(IgnoreKey, v => Live.SetWorldIgnore(v.IsNull ? -1 : IgnoreOf(v)), true);
        }

        internal static void Detach()
        {
            try { _observer?.Dispose(); } catch (Exception) { }
            try { _observer2?.Dispose(); } catch (Exception) { }
            _observer = null;
            _observer2 = null;
            _world = null;
            Live.LeaveWorld();
        }
    }
}
