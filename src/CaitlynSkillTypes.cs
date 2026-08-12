namespace SodLolCaitlyn;

// These classes intentionally contain very little gameplay code in phase 0.
// Their first job is to prove that new SkillTrigger types can be registered,
// equipped and network-spawned without replacing a vanilla Dew resource.
//
// MirrorProcessed is retained on each runtime NetworkBehaviour subclass because
// existing Shape of Dreams runtime-skill mods use this marker when their mod
// assembly is not passed through the normal Unity/Mirror Weaver pipeline.
public sealed class St_D_CaitlynHeadshot : SkillTrigger
{
    private void MirrorProcessed() { }
}

public sealed class St_Q_CaitlynPiltoverPeacemaker : SkillTrigger
{
    private void MirrorProcessed() { }
}

public sealed class St_W_CaitlynYordleSnapTrap : SkillTrigger
{
    private void MirrorProcessed() { }
}

public sealed class St_E_Caitlyn90CaliberNet : SkillTrigger
{
    private void MirrorProcessed() { }
}

public sealed class St_R_CaitlynAceInTheHole : SkillTrigger
{
    private void MirrorProcessed() { }
}
