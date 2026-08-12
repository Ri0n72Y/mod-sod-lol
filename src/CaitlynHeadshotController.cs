using System.Globalization;
using UnityEngine;

namespace SodLolCaitlyn;

/// <summary>
/// Server-owned Phase-0 Headshot implementation.
///
/// The counter is advanced only from Actor.DoBasicAttackHit, which the official
/// API documents as the basic-attack-only hit path. Secondary/non-main hits are
/// ignored so one attack cannot generate multiple Headshot stacks.
/// </summary>
internal sealed class CaitlynHeadshotController
{
    internal const string CounterKey = "sodlol.caitlyn.headshot.count";

    private const int AttacksPerHeadshot = 5;
    private const float BaseAttackDamageRatio = 0.60f;
    private const float CritChanceAttackDamageRatio = 1.50f;

    public void Refresh()
    {
        foreach (DewPlayer player in DewPlayer.gamePlayers)
        {
            Hero hero = player?.hero;
            if (hero == null || !hero.isServer)
            {
                continue;
            }

            if (HasHeadshotIdentity(hero))
            {
                if (!hero.persistentSyncedData.ContainsKey(CounterKey))
                {
                    SetCounter(hero, 0);
                }
            }
            else
            {
                RemoveCounter(hero);
            }
        }
    }

    public void Cleanup()
    {
        foreach (DewPlayer player in DewPlayer.gamePlayers)
        {
            Hero hero = player?.hero;
            if (hero != null && hero.isServer)
            {
                RemoveCounter(hero);
            }
        }
    }

    public void OnBasicAttackHit(Hero hero, Entity target, bool isMain)
    {
        if (hero == null ||
            target == null ||
            !hero.isServer ||
            !isMain ||
            !HasHeadshotIdentity(hero))
        {
            return;
        }

        int count = GetCounter(hero) + 1;
        if (count < AttacksPerHeadshot)
        {
            SetCounter(hero, count);
            return;
        }

        // This callback runs after the vanilla basic-hit method. A killing blow
        // may already have destroyed the target by the time the postfix executes.
        // The fifth hit still consumes Headshot, but we never send another damage
        // request to an Actor that has entered its destruction lifecycle.
        if (target.isDestroyed)
        {
            SetCounter(hero, 0);
            Debug.Log($"[SodLolCaitlyn] Headshot consumed on killing blow against {target.name}; target was already destroyed.");
            return;
        }

        float attackDamage = Mathf.Max(0f, hero.Status.attackDamage);
        float rawCritChance = Mathf.Max(0f, hero.Status.critChance);
        float critChanceRatio = NormalizeProbability(rawCritChance);
        float ratio = BaseAttackDamageRatio + CritChanceAttackDamageRatio * critChanceRatio;
        float bonusDamage = attackDamage * ratio;

        // Consume the ready Headshot only after the bonus-damage call returns.
        // If the damage path throws, the counter remains ready and the failure
        // is visible instead of silently eating the passive proc.
        if (bonusDamage > 0f)
        {
            hero.DealDamage(hero.DefaultDamage(bonusDamage, 0f), target);
        }

        SetCounter(hero, 0);

        Debug.Log(
            $"[SodLolCaitlyn] Headshot hit {target.name} for experimental bonus {bonusDamage:0.##} " +
            $"(AD={attackDamage:0.##}, critRaw={rawCritChance:0.###}, critRatio={critChanceRatio:0.###}).");
    }

    internal static float NormalizeProbability(float rawValue)
    {
        if (float.IsNaN(rawValue) || float.IsInfinity(rawValue) || rawValue <= 0f)
        {
            return 0f;
        }

        // Shape of Dreams stores crit chance as a normalized decimal internally
        // (for example 0.005 == 0.5%). Clamp abnormal/modded values to the
        // probability domain used by this experimental scaling.
        return Mathf.Clamp01(rawValue);
    }

    private static bool HasHeadshotIdentity(Hero hero)
    {
        HeroSkill heroSkill = hero?.GetComponent<HeroSkill>();
        return heroSkill?.Identity is St_D_CaitlynHeadshot;
    }

    private static int GetCounter(Hero hero)
    {
        if (hero?.persistentSyncedData == null ||
            !hero.persistentSyncedData.TryGetValue(CounterKey, out string value) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            return 0;
        }

        return Mathf.Clamp(count, 0, AttacksPerHeadshot - 1);
    }

    private static void SetCounter(Hero hero, int count)
    {
        hero.persistentSyncedData[CounterKey] =
            Mathf.Clamp(count, 0, AttacksPerHeadshot - 1)
                .ToString(CultureInfo.InvariantCulture);
    }

    private static void RemoveCounter(Hero hero)
    {
        if (hero?.persistentSyncedData != null &&
            hero.persistentSyncedData.ContainsKey(CounterKey))
        {
            hero.persistentSyncedData.Remove(CounterKey);
        }
    }
}
