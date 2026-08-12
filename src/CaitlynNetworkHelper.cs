using System;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace SodLolCaitlyn;

internal static class CaitlynNetworkHelper
{
    private static readonly FieldInfo AssetIdField = typeof(NetworkIdentity).GetField(
        "_assetId",
        BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo InitializeNetworkBehavioursMethod = typeof(NetworkIdentity).GetMethod(
        "InitializeNetworkBehaviours",
        BindingFlags.NonPublic | BindingFlags.Instance);

    public static uint GenerateAssetId(string input)
    {
        uint hash = 2166136261;
        foreach (char c in input ?? "SodLolCaitlyn")
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash | 0x80000000;
    }

    public static NetworkIdentity EnsureNetworkIdentity(GameObject gameObject, uint assetId)
    {
        if (gameObject == null)
        {
            throw new ArgumentNullException(nameof(gameObject));
        }

        EnsureMirrorInternalsAvailable();

        NetworkIdentity identity = gameObject.GetComponent<NetworkIdentity>() ??
                                   gameObject.AddComponent<NetworkIdentity>();

        AssetIdField.SetValue(identity, assetId);

        // Current Shape of Dreams Mirror build has no _isSceneObject field.
        // A zero sceneId identifies this runtime-created object as non-scene content.
        identity.sceneId = 0UL;
        return identity;
    }

    public static void RegisterSpawnHandler(uint assetId, GameObject prefab)
    {
        if (prefab == null)
        {
            throw new ArgumentNullException(nameof(prefab));
        }

        EnsureMirrorInternalsAvailable();

        NetworkClient.RegisterSpawnHandler(
            assetId,
            message =>
            {
                GameObject instance = UnityEngine.Object.Instantiate(
                    prefab,
                    message.position,
                    message.rotation);
                instance.transform.localScale = message.scale;
                instance.name = prefab.name;
                instance.SetActive(true);

                NetworkIdentity identity = EnsureNetworkIdentity(instance, assetId);
                InitializeNetworkBehavioursMethod.Invoke(identity, null);
                return instance;
            },
            gameObject =>
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            });
    }

    public static void UnregisterSpawnHandler(uint assetId)
    {
        try
        {
            NetworkClient.UnregisterSpawnHandler(assetId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SodLolCaitlyn] Could not unregister runtime asset {assetId}: {exception.Message}");
        }
    }

    private static void EnsureMirrorInternalsAvailable()
    {
        if (AssetIdField == null)
        {
            throw new MissingFieldException(
                typeof(NetworkIdentity).FullName,
                "_assetId");
        }

        if (InitializeNetworkBehavioursMethod == null)
        {
            throw new MissingMethodException(
                typeof(NetworkIdentity).FullName,
                "InitializeNetworkBehaviours");
        }
    }
}
