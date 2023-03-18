using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Rewired.Utils;
using RoR2;

namespace VengeanceSwarmFix;

public class SwarmSpawn
{
    public SwarmSpawn()
    {

        IL.RoR2.Artifacts.SwarmsArtifactManager.OnSpawnCardOnSpawnedServerGlobal += (il) =>
        {
            ILCursor c = new ILCursor(il);
            bool found = c.TryGotoNext(
                x => x.MatchCall<RoR2.DirectorCore>("get_instance"),
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<RoR2.SpawnCard.SpawnResult>(nameof(RoR2.SpawnCard.SpawnResult.spawnRequest)),
                x => x.MatchCallvirt<RoR2.DirectorCore>("TrySpawnObject"),
                x => x.MatchPop()
            );
            if (found)
            {
                c.Index += 1;
                c.RemoveRange(4);
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate<Action<SpawnCard.SpawnResult, CharacterMaster>>(SpawnCharacter);
            }
            else
            {
                VengeanceSwarmFix.ConsoleLogger.LogError("Unable to set set hook");
            }
        };
    }

    private static bool IsDoppelganger(CharacterMaster cm)
    {
        return cm.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) > 0;
    }

    private static void setHPLoss(CharacterMaster characterMaster)
    {
        //remove double stacks of cuthp
        int itemCount = characterMaster.inventory.GetItemCount(RoR2Content.Items.CutHp);
        if (itemCount > 0)
        {
            characterMaster.inventory.RemoveItem(RoR2Content.Items.CutHp, (itemCount-1));
        }
        else
        {
            characterMaster.inventory.GiveItem(RoR2Content.Items.CutHp);
        }
    }

    private static void SpawnCharacter (SpawnCard.SpawnResult sr, CharacterMaster cm)
    {
        //test if there is a character to clone
        if (cm.IsNullOrDestroyed())
        {
            VengeanceSwarmFix.ConsoleLogger.LogWarning("unable to spawn clone, parent is null or destroyed");
            return;
        }

        //Spawn clone
        CharacterMaster sm;
        try
        {
            sr.spawnRequest.ignoreTeamMemberLimit = true;
            sm = DirectorCore.instance.TrySpawnObject(sr.spawnRequest).GetComponent<CharacterMaster>();
        }
        catch (Exception e)
        {
            VengeanceSwarmFix.ConsoleLogger.LogWarning("Unable to spawn clone, may be bad position or max team size");
            return;
        }
        
        //set inventory for doppelgangers
        try
        {
            //that is all unless doppelganger
            if (!IsDoppelganger(cm)) return;
            
            //copy items from original
            sm.inventory = cm.inventory;

            //remove double stacks of cuthp
            setHPLoss(cm);
            setHPLoss(sm);

        }
        catch
        {
            VengeanceSwarmFix.ConsoleLogger.LogError("unable to update inventory for " + sm.name + " and " + cm.name);
        }
    }
}