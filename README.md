# QuietNoize's SaveKit for Unity

A save system for Unity that stores the state of your scenes: which objects exist, which were destroyed, and the values of the components you choose. Everything is configured in the Inspector, with no save-data classes to write.

- Scene state is saved and restored automatically on scene transitions
- Per-component, per-field selection of what gets saved
- Destroyed and runtime-spawned objects are tracked
- Custom serializer definitions for components that need special handling
- Async API with progress callbacks

## Installation

**Package Manager → `+` → Add package from git URL:**

```
https://github.com/QuietNoize/SaveKit.git?path=/com.quietnoize.savekit
```

Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.quietnoize.savekit": "https://github.com/QuietNoize/SaveKit.git"
  }
}
```

> **Requirements:** Unity 6 (2023.1+). Uses `JsonUtility` for serialization.

## Setup

For every object whose state you want to save:

1. Add **SaveKit → GameObject Save Handler**.
2. Add an identifier (the **+** button next to *Identifier*, or add the component manually):
   - **Guid Identifier**: globally unique, the default choice for scene objects.
   - **String Identifier**: based on the object name (`Chest`, `Chest (1)`, …).
3. In **Components to Save**, click **+ Add Component** and tick the fields you want to persist. `Transform` is supported out of the box. Any `MonoBehaviour` works too.
4. *(Optional)* Set **Load Order**. Objects are processed in ascending order, so lower values go first.

### Objects spawned at runtime

To recreate an object after loading (or after it was in another scene), set its **Prefab** in the handler. The prefab **must be inside a `Resources` folder**. The `Resources` path is filled in automatically.

For prefabs with a Guid Identifier, enable **Unique Instance** on the identifier so each instance receives its own ID.

## Usage

All calls are `async` and return `Task`. Only one operation can run at a time. Starting another while one is in progress throws `InvalidOperationException`.

### Save / load a game file

```csharp
using QuietNoize.SaveKit;
using System.IO;
using UnityEngine;

public class SaveMenu : MonoBehaviour
{
    string Path => System.IO.Path.Combine(Application.persistentDataPath, "slot1.json");

    public async void Save()
    {
        await SaveManager.SaveGameToFileAsync(Path);
    }

    public async void Load()
    {
        if (!File.Exists(Path)) return;

        await SaveManager.LoadGameFromFileAsync(Path);
    }
}
```

Loading restores the saved set of active scenes, then applies the saved state to every tracked object.

### Progress callback

```csharp
await SaveManager.SaveGameToFileAsync(path, p => progressBar.value = p);
await SaveManager.LoadGameFromFileAsync(path, p => progressBar.value = p);
```

`p` goes from `0` to `1`.

### Error handling

```csharp
try
{
    await SaveManager.LoadGameFromFileAsync(path);
}
catch (System.Exception e)
{
    Debug.LogError($"Load failed: {e.Message}");
}
```

If some objects fail to serialize or deserialize, the error is an `AggregateException` that contains each individual failure.

### Scene transitions

Use `SceneLoader` instead of `SceneManager` so scene state is kept between transitions. Leaving a scene saves its state, and entering it restores it.

```csharp
// Replace the current scene(s)
await SceneLoader.LoadSceneAsync("Level2", p => loadingBar.value = p);

// Load additively; optionally make it the active scene
await SceneLoader.LoadSceneAdditiveAsync("Interior", setActive: true);

// Save the scene's state and unload it
await SceneLoader.UnloadSceneAsync("Interior");
```

`UnloadSceneAsync` throws if the scene isn't loaded or is the last loaded scene.

Objects in `DontDestroyOnLoad` are saved and restored as well.

### Load hooks

Implement these on any saved component to react to loading:

```csharp
using QuietNoize.SaveKit;
using UnityEngine;

public class Health : MonoBehaviour, IPreLoadHandler, IPostLoadHandler
{
    [SerializeField] int m_hp = 100;

    public void OnPreLoad()  { /* before values are applied */ }
    public void OnPostLoad() { /* values applied, refresh UI, etc. */ }
}
```

### Looking up objects by ID

```csharp
using QuietNoize.SaveKit.Identification;

if (IdentifierBase.TryGetIdentifierById<IdentifierBase>(id, out var identifier))
{
    var go = identifier.gameObject;
}
```

## Custom serialization

By default, a component's serializable fields (public, `[SerializeField]`, `[SerializeReference]`) are listed in the Inspector and you pick which ones to save. To control the list yourself, for example to include properties, derive from `ComponentSerializerDefinition<T>`:

```csharp
using QuietNoize.SaveKit.Serialization;
using System;
using UnityEngine;

[Serializable]
public class RigidbodySerializerDefinition : ComponentSerializerDefinition<Rigidbody>
{
    protected override MemberSerializerDefinition[] GetMembers() => new[]
    {
        new MemberSerializerDefinition(nameof(Rigidbody.linearVelocity), typeof(Vector3), isSelected: true),
        new MemberSerializerDefinition(nameof(Rigidbody.angularVelocity), typeof(Vector3), isSelected: true),
        new MemberSerializerDefinition(nameof(Rigidbody.mass), typeof(float))
    };
}
```

The definition is found automatically. Nothing needs to be registered. Properties must have both a getter and a setter. Members can be pre-selected via `isSelected`.

## Notes

- **What can be saved:** field types supported by Unity's `JsonUtility`, plus arrays and generic collections (`List<T>`, `Stack<T>`, …) of them. Dictionaries are not supported. Unity object references are not supported.
- Only values that **changed since `Awake`** are written to the save file.
- Saves are plain JSON text.
- Saved types are stored by assembly-qualified name. Renaming or moving a saved component class will break existing saves.

## License

[CC BY-ND 4.0](LICENSE)
