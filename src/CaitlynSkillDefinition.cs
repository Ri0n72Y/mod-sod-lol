using System;

namespace SodLolCaitlyn;

internal sealed class CaitlynSkillDefinition
{
    public CaitlynSkillDefinition(
        string guid,
        string typeName,
        Type runtimeType,
        HeroSkillLocation slot,
        string vanillaTemplateTypeName,
        bool identity = false)
    {
        Guid = guid;
        TypeName = typeName;
        RuntimeType = runtimeType;
        Slot = slot;
        VanillaTemplateTypeName = vanillaTemplateTypeName;
        IsIdentity = identity;
    }

    public string Guid { get; }
    public string TypeName { get; }
    public Type RuntimeType { get; }
    public HeroSkillLocation Slot { get; }
    public string VanillaTemplateTypeName { get; }
    public bool IsIdentity { get; }

    // Phase 0 uses vanilla TriggerConfig objects as castable proxies. The custom
    // gameplay implementations will replace these templates one ability at a time.
    public static readonly CaitlynSkillDefinition Headshot = new(
        "c1a17a10000000000000000000000001",
        nameof(St_D_CaitlynHeadshot),
        typeof(St_D_CaitlynHeadshot),
        HeroSkillLocation.Identity,
        "St_D_DoubleTap",
        identity: true);

    public static readonly CaitlynSkillDefinition PiltoverPeacemaker = new(
        "c1a17a10000000000000000000000002",
        nameof(St_Q_CaitlynPiltoverPeacemaker),
        typeof(St_Q_CaitlynPiltoverPeacemaker),
        HeroSkillLocation.Q,
        "St_Q_HandCannon");

    public static readonly CaitlynSkillDefinition YordleSnapTrap = new(
        "c1a17a10000000000000000000000003",
        nameof(St_W_CaitlynYordleSnapTrap),
        typeof(St_W_CaitlynYordleSnapTrap),
        HeroSkillLocation.W,
        "St_Q_HandCannon");

    public static readonly CaitlynSkillDefinition NinetyCaliberNet = new(
        "c1a17a10000000000000000000000004",
        nameof(St_E_Caitlyn90CaliberNet),
        typeof(St_E_Caitlyn90CaliberNet),
        HeroSkillLocation.E,
        "St_R_QuickTrigger");

    public static readonly CaitlynSkillDefinition AceInTheHole = new(
        "c1a17a10000000000000000000000005",
        nameof(St_R_CaitlynAceInTheHole),
        typeof(St_R_CaitlynAceInTheHole),
        HeroSkillLocation.R,
        "St_R_PrecisionShot");

    public static readonly CaitlynSkillDefinition[] All =
    {
        Headshot,
        PiltoverPeacemaker,
        YordleSnapTrap,
        NinetyCaliberNet,
        AceInTheHole
    };

    public static readonly CaitlynSkillDefinition[] ExclusiveActives =
    {
        PiltoverPeacemaker,
        YordleSnapTrap,
        NinetyCaliberNet,
        AceInTheHole
    };
}
