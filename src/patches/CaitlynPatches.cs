using System;
using HarmonyLib;

namespace SodLolCaitlyn.Patches;

[HarmonyPatch(typeof(DewResources))]
internal static class CaitlynDewResourcesPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(DewResources.Load),
        new Type[]
        {
            typeof(string),
            typeof(ResourceLoadSettings)
        })]
    private static bool LoadPrefix(string guid, ref UnityEngine.Object __result)
    {
        CaitlynResourceRegistry resources = ModEntry.Instance?.Content?.Resources;
        if (resources != null && resources.TryLoad(guid, out UnityEngine.Object result))
        {
            __result = result;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(LootManager))]
internal static class CaitlynLootManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(LootManager.OnStartServer))]
    private static void OnStartServerPostfix()
    {
        ModEntry.Instance?.Content?.IdentityGate.Refresh(force: true);
    }
}

[HarmonyPatch(typeof(Actor))]
internal static class CaitlynBasicAttackPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(
        nameof(Actor.DoBasicAttackHit),
        new Type[]
        {
            typeof(Entity),
            typeof(bool),
            typeof(bool),
            typeof(float),
            typeof(float)
        })]
    private static void DoBasicAttackHitPostfix(
        Actor __instance,
        Entity target,
        bool isMain)
    {
        Hero hero = __instance as Hero ?? __instance?.firstEntity as Hero;
        ModEntry.Instance?.Content?.Headshot.OnBasicAttackHit(hero, target, isMain);
    }
}
