using System;
using UnityEngine;

namespace SodLolCaitlyn;

public sealed class ModEntry : ModBehaviour
{
    public static ModEntry Instance { get; private set; }

    internal CaitlynContent Content { get; private set; }

    private bool _harmonyPatched;

    private void Awake()
    {
        Instance = this;
        Content = new CaitlynContent();
    }

    private void Start()
    {
        instance.isAlteringGameplay = true;

        try
        {
            harmony.PatchAll();
            _harmonyPatched = true;
            Content.Register();
            Debug.Log($"[{mod.metadata.id}] Caitlyn identity experiment loaded.");
        }
        catch (Exception exception)
        {
            Content?.Unregister();
            UnpatchHarmony();
            Debug.LogError($"[{mod.metadata.id}] Caitlyn identity experiment failed to load: {exception}");
            throw;
        }
    }

    private void Update()
    {
        Content?.Tick(Time.unscaledTime);
    }

    private void OnDestroy()
    {
        Content?.Unregister();
        UnpatchHarmony();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void UnpatchHarmony()
    {
        if (!_harmonyPatched)
        {
            return;
        }

        harmony.UnpatchAll(harmony.Id);
        _harmonyPatched = false;
    }
}
