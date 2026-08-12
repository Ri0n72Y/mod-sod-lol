using System;
using UnityEngine;

namespace SodLolCaitlyn;

internal sealed class CaitlynContent
{
    private readonly CaitlynResourceRegistry _resources = new();
    private readonly CaitlynProfileRegistry _profile = new();
    private readonly CaitlynIdentityGate _identityGate = new();
    private readonly CaitlynHeadshotController _headshot = new();

    private CaitlynLoadoutInstaller _loadout;
    private float _nextRefreshTime;
    private bool _registered;

    public CaitlynResourceRegistry Resources => _resources;
    public CaitlynIdentityGate IdentityGate => _identityGate;
    public CaitlynHeadshotController Headshot => _headshot;

    public void Register()
    {
        if (_registered)
        {
            return;
        }

        try
        {
            _resources.Register();
            _profile.RegisterAll();

            _loadout = new CaitlynLoadoutInstaller(_resources);
            _loadout.ApplyToLoadedLacertas();
            _identityGate.Refresh(force: true);
            _headshot.Refresh();

            _registered = true;
        }
        catch
        {
            Unregister();
            throw;
        }
    }

    public void Tick(float time)
    {
        if (!_registered || time < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = time + 0.5f;
        _loadout?.ApplyToLoadedLacertas();
        _identityGate.Refresh();
        _headshot.Refresh();
    }

    public void Unregister()
    {
        TryCleanup(_headshot.Cleanup, "Headshot state");
        TryCleanup(_identityGate.RemoveFromPools, "loot-pool gate");
        TryCleanup(() => _loadout?.Restore(), "Lacerta loadout");
        TryCleanup(_profile.UnregisterAll, "profile/content registration");
        TryCleanup(_resources.Unregister, "runtime resources");

        _loadout = null;
        _registered = false;
        _nextRefreshTime = 0f;
    }

    private static void TryCleanup(Action cleanup, string name)
    {
        try
        {
            cleanup?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SodLolCaitlyn] Cleanup failed for {name}: {exception}");
        }
    }
}
