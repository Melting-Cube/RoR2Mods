using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Rewired.Utils;
using RoR2;
using UnityEngine;
using Console = System.Console;

namespace VengeanceSwarmFix;

public class SwarmSpawn
{
    public SwarmSpawn()
    {

        IL.RoR2.Artifacts.SwarmsArtifactManager.OnSpawnCardOnSpawnedServerGlobal += (il) =>
        {
            VengeanceSwarmFix.ConsoleLogger.LogInfo(typeof(RoR2.DirectorSpawnRequest));
            VengeanceSwarmFix.ConsoleLogger.LogInfo(nameof(RoR2.DirectorSpawnRequest));
            VengeanceSwarmFix.ConsoleLogger.LogInfo(nameof(SpawnCard.SpawnResult.spawnRequest));
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
                VengeanceSwarmFix.ConsoleLogger.LogMessage("found the il");
                c.Index += 1;
                c.RemoveRange(4);
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate<Action<SpawnCard.SpawnResult, CharacterMaster>>(SpawnCharacter);
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
        //spawn clone
        if (cm.IsNullOrDestroyed())
        {
            VengeanceSwarmFix.ConsoleLogger.LogWarning("unable to spawn clone, parrent is null or destroyed");
            return;
        }

        CharacterMaster sm;
        try
        {
            sr.spawnRequest.ignoreTeamMemberLimit = true;
            sm = DirectorCore.instance.TrySpawnObject(sr.spawnRequest).GetComponent<CharacterMaster>();
        }
        catch (Exception e)
        {
            VengeanceSwarmFix.ConsoleLogger.LogWarning("clone is unable to spawn ");
            return;
        }

        //get spawn's charcter master
        //CharacterMaster sm = go.GetComponent<CharacterMaster>();
        //CharacterMaster sm = go.GetComponent<CharacterMaster>();

        try
        {
            //that is all unless doppelganger
            if (!IsDoppelganger(cm)) return;
        }
        catch
        {
            VengeanceSwarmFix.ConsoleLogger.LogWarning("unable to test if doppelganger for" + cm.name);
        }

        try
        {
            //copy items from original
            sm.inventory = cm.inventory;
        }
        catch
        {
            VengeanceSwarmFix.ConsoleLogger.LogError("unable to copy inventory for " + sm.name);
        }

        try
        {
            //remove double stacks of cuthp
            setHPLoss(cm);
            setHPLoss(sm);
        }
        catch (Exception e)
        {
            VengeanceSwarmFix.ConsoleLogger.LogWarning("unable to set hp loss for " + sm.name + " and " + cm.name);
        }
    }
}