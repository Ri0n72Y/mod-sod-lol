using System;
using System.Collections.Generic;
using System.Linq;

namespace SodLolCaitlyn;

internal sealed class CaitlynLoadoutInstaller
{
    public const string LacertaHeroTypeName = "Hero_Lacerta";

    private readonly CaitlynResourceRegistry _resources;

    public CaitlynLoadoutInstaller(CaitlynResourceRegistry resources)
    {
        _resources = resources;
    }

    public void ApplyToLoadedLacertas()
    {
        Hero prefab = DewResources.GetByShortTypeName<Hero>(LacertaHeroTypeName);
        Apply(prefab?.GetComponent<HeroSkill>());

        foreach (DewPlayer player in DewPlayer.gamePlayers)
        {
            Hero hero = player?.hero;
            if (hero == null || hero.GetType().Name != LacertaHeroTypeName)
            {
                continue;
            }

            Apply(hero.GetComponent<HeroSkill>());
        }
    }

    public void Restore()
    {
        Hero prefab = DewResources.GetByShortTypeName<Hero>(LacertaHeroTypeName);
        Remove(prefab?.GetComponent<HeroSkill>());

        foreach (DewPlayer player in DewPlayer.gamePlayers)
        {
            Hero hero = player?.hero;
            if (hero == null || hero.GetType().Name != LacertaHeroTypeName)
            {
                continue;
            }

            Remove(hero.GetComponent<HeroSkill>());
        }
    }

    private void Apply(HeroSkill heroSkill)
    {
        if (!IsLacerta(heroSkill))
        {
            return;
        }

        St_D_CaitlynHeadshot identity = _resources.GetPrefab<St_D_CaitlynHeadshot>();
        if (identity == null)
        {
            return;
        }

        string identityGuid = CaitlynSkillDefinition.Headshot.Guid;
        AssetRef<SkillTrigger>[] current = heroSkill.loadoutTrait ?? Array.Empty<AssetRef<SkillTrigger>>();
        if (current.Any(item => string.Equals(item.guid, identityGuid, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AssetRef<SkillTrigger>[] extended = new AssetRef<SkillTrigger>[current.Length + 1];
        current.CopyTo(extended, 0);
        extended[current.Length] = new AssetRef<SkillTrigger>(identity);
        heroSkill.loadoutTrait = extended;
    }

    private static void Remove(HeroSkill heroSkill)
    {
        if (!IsLacerta(heroSkill) || heroSkill.loadoutTrait == null)
        {
            return;
        }

        string identityGuid = CaitlynSkillDefinition.Headshot.Guid;
        AssetRef<SkillTrigger>[] filtered = heroSkill.loadoutTrait
            .Where(item => !string.Equals(item.guid, identityGuid, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (filtered.Length != heroSkill.loadoutTrait.Length)
        {
            heroSkill.loadoutTrait = filtered;
        }
    }

    private static bool IsLacerta(HeroSkill heroSkill)
    {
        Hero hero = heroSkill?.GetComponent<Hero>();
        return hero != null && hero.GetType().Name == LacertaHeroTypeName;
    }
}
