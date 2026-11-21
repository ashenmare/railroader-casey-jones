# Casey Jones 0.1.0 feed (CaseyJones.Api)

Casey Jones publishes a small read-only feed: `CaseyJones.Api` in `CaseyJones.dll`. Another mod can read it by reflection, with no reference and no load order, to see which Curve speed is in force. This file gives the ashenmare feed convention first, then this feed's schema.
It is also on the shared feed list of the Railroader mod feeds standard (ModFeeds.cs), as `casey-jones`, so another mod can find it with `ModFeeds.List()` and read it with `ModFeeds.Read("casey-jones")`.

## Ashenmare feed convention, v1

A feed is a `public static class` named `Api` in the mod's own namespace with these three members:

| Member | Meaning |
| --- | --- |
| `public const int Version` | The feed's schema version. Within a version, fields are only ever added. Renaming, retyping or dropping one raises the version, so a consumer checks it and reads only the versions it knows. |
| `public const string Feed` | The feed's stable id, in kebab case. It never changes. |
| `public static object Read()` | The data. `"api"` (int, equal to `Version`) and `"feed"` (string, equal to `Feed`) are always at the top level. |

To find feeds generically, scan the loaded assemblies (`AppDomain.CurrentDomain.GetAssemblies()`) for public static classes named `Api` with a literal `int Version`, a literal `string Feed` and a public static parameterless `Read()` returning `object`. Read the constants with `FieldInfo.GetRawConstantValue()`, and bind `Read` once with `Delegate.CreateDelegate(typeof(Func<object>), method)`.

The rules:
- **Plain data only.** `Read()` returns BCL types only (`Dictionary<string, object>`, `List<object>`, `string`, `int`, `float`, `bool`, `null`), never one of the mod's own types, so any JSON serializer can write it as is. Floats are always finite.
- **camelCase keys.**
- **Main thread.** Call feeds on Unity's main thread (from a `MonoBehaviour`, or a game HTTP handler that runs there).
- **Never throws.** On a failure it returns `{ "api": <Version>, "feed": "<Feed>", "error": "<what went wrong>" }` and logs once in the mod's own log. Check for `"error"` first.
- **Read only.** Reading a feed changes nothing in the game, and nothing in this mod.
- **Cheap to poll.** A feed works only when it's called (no timer, no Update loop, no Harmony patch of its own) and is light enough to read every couple of seconds.
- **Don't change the answers.** Copy what you want to keep or edit.

## Casey Jones's members

```csharp
namespace CaseyJones
{
    public static class Api
    {
        public const int Version = 1;
        public const string Feed = "casey-jones";
        public static object Read();
    }
}
```

`Read()` reads the setting in force on this computer. It builds a new dictionary on every call.

## Schema, version 1

| Key | Type | Meaning |
| --- | --- | --- |
| `api` | int | `1` |
| `feed` | string | `"casey-jones"` |
| `active` | bool | The Curve speed hook is on: the mod loaded and the game version allows it. `false` only on a scan of the loaded assemblies when the mod stood down (the feed is not on the shared list then), and after the mod is unticked. |
| `curveSpeed` | string | The setting in force on this game now: `"1x"`, `"1.5x"`, `"2x"` or `"Unlimited"`. Outside a world it is `"1x"` (the game's own figures). On the host it is the host's setting; on a multiplayer client it is the host's value, or `"1x"` until that arrives. |
| `multiplier` | float or null | The factor in force: `1`, `1.5` or `2`; `null` for Unlimited, which is a ceiling and not a factor. |
| `unlimited` | bool | `true` when the setting is Unlimited. |
| `ignoreCarCurveLimits` | bool | The Ignore car curve limits checkbox is in force on this game now (outside a world `false`; on a multiplayer client the host's value, `false` until it arrives): cars take any curve at speed. |
| `capMph` | float or null | The most a curve's scaled rating reaches, in mph: `250` in every setting but 1x, `null` at 1x. The rating then used is the game's rating times `multiplier` (Unlimited: `capMph`), never above `capMph` and never below the game's own, so straight track stays 1000. |

On a failure the answer is instead `{ "api": 1, "feed": "casey-jones", "error": "..." }`, where `error` is a string (an exception's type name only).

Example:

```json
{ "api": 1, "feed": "casey-jones", "active": true, "curveSpeed": "2x", "multiplier": 2.0, "unlimited": false, "capMph": 250.0, "ignoreCarCurveLimits": false }
```
