using System.Collections.Generic;

namespace SodLolCaitlyn;

internal sealed class CaitlynIdentityGate
{
    private bool? _lastEnabled;

    public void Refresh(bool force = false)
    {
        LootManager lootManager = NetworkedManagerBase<LootManager>.softInstance;
        if (lootManager == null || !lootManager.isServer)
        {
            // Do not cache state before the authoritative server loot manager
            // exists. Client copies never mutate shared loot-pool membership.
            return;
        }

        bool enabled = IsHeadshotEquippedByAnyHumanPlayer();
        if (!force && _lastEnabled == enabled)
        {
            return;
        }

        foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.ExclusiveActives)
        {
            SetMembership(lootManager.poolSkills, definition.TypeName, enabled);

            if (lootManager.poolSkillsByRarity.TryGetValue(Rarity.Unique, out List<string> uniqueSkills))
            {
                SetMembership(uniqueSkills, definition.TypeName, enabled);
            }
        }

        _lastEnabled = enabled;
    }

    public void RemoveFromPools()
    {
        LootManager lootManager = NetworkedManagerBase<LootManager>.softInstance;
        if (lootManager != null && lootManager.isServer)
        {
            foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.ExclusiveActives)
            {
                SetMembership(lootManager.poolSkills, definition.TypeName, false);
                foreach (List<string> skills in lootManager.poolSkillsByRarity.Values)
                {
                    SetMembership(skills, definition.TypeName, false);
                }
                foreach (List<string> skills in lootManager.poolSkillsByTag.Values)
                {
                    SetMembership(skills, definition.TypeName, false);
                }
            }
        }

        _lastEnabled = null;
    }

    public static bool IsHeadshotEquippedByAnyHumanPlayer()
    {
        foreach (DewPlayer player in DewPlayer.gamePlayers)
        {
            Hero hero = player?.hero;
            HeroSkill heroSkill = hero?.GetComponent<HeroSkill>();
            if (heroSkill?.Identity is St_D_CaitlynHeadshot)
            {
                return true;
            }
        }

        return false;
    }

    private static void SetMembership(List<string> list, string value, bool present)
    {
        if (list == null)
        {
            return;
        }

        if (present)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }
        else
        {
            while (list.Remove(value))
            {
            }
        }
    }
}
