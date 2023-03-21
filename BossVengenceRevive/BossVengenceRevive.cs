using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Configuration;
using Rewired.Utils;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine.Networking;

#pragma warning disable CS0618
[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]
#pragma warning restore CS0618


namespace BossVengenceRevive
{
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class BossVengenceRevive : BaseUnityPlugin
    {
        //metadata
        private const string PluginGUID = "com." + PluginAuthor + "." + PluginName;
        private const string PluginAuthor = "Melting-Cube";
        private const string PluginName = "BossVengenceRevive";
        private const string PluginVersion = "2.0.4";
        
        //add config entries
        private static ConfigEntry<bool> Enabled { get; set; } = null!;
        private static ConfigEntry<bool> TeleporterEvent { get; set; } = null!;
        private static ConfigEntry<bool> TeleporterBoss { get; set; } = null!;
        private static ConfigEntry<bool> MiscBoss { get; set; } = null!;
        private static ConfigEntry<bool> Doppelganger { get; set; } = null!;

        public void Awake()
        {
            //make config entries
            Enabled = Config.Bind<bool>(
                "Mod Status",
                "Enabled",
                true,
                "Turn the mod on or off."
                );
            TeleporterEvent = Config.Bind<bool>(
                "Boss Types",
                "Teleporter Event",
                true,
                "Revive dead players after you complete a teleporter event."
            );
            TeleporterBoss = Config.Bind<bool>(
                "Boss Types",
                "Teleporter Boss",
                false,
                "Revive dead players after you defeat a teleporter boss."
                );
            Doppelganger = Config.Bind<bool>(
                "Boss Types",
                "Doppelganger",
                true,
                "Revive dead players after you defeat a Doppelganger." +
                "\nDoppelgangers are enabled by the Vengeance Artifact." +
                "\nWhen used together Vengeance and Swarms Artifacts have known weird interactions and only " +
                "one clone will be considered a Doppelganger, the other one will be a special boss"
                );
            MiscBoss = Config.Bind<bool>(
                "Boss Types",
                "Special Bosses",
                false,
                "Revive dead players after you defeat a Special Boss." +
                "\nSpecial bosses are ones such as Aurelionite or Alloy Worship Unit. " +
                "This will trigger a revive after stages 2 and 4 of the Mithrix fight. " +
                "After each stage of Voidling a revive will trigger."
            );
            
            //add config options in-game
            ModSettingsManager.AddOption(new CheckBoxOption(Enabled));
            ModSettingsManager.AddOption((new CheckBoxOption(TeleporterEvent)));
            ModSettingsManager.AddOption((new CheckBoxOption(TeleporterBoss)));
            ModSettingsManager.AddOption((new CheckBoxOption(MiscBoss)));
            ModSettingsManager.AddOption(new CheckBoxOption(Doppelganger));

            //On.RoR2.Networking.GameNetworkManager.OnClientConnect += (self, user, t) => { };
            Logger.LogInfo("Loaded BossVengenceRevive");

            //every portal completion
            On.RoR2.TeleporterInteraction.ChargedState.OnEnter += (orig, self) =>
            {
                //call original method
                orig(self);

                if (!TeleporterEvent.Value || !Enabled.Value) return;
                RespawnChar();
                Logger.LogInfo("teleporter finished charging, reviving dead players");
            };
            
            //everytime a character dies
            On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) => 
            {
                //is a boss and mod is enabled
                if (damageReport.victimIsBoss && Enabled.Value &&
                    damageReport.victimMaster.inventory.GetItemCount(RoR2Content.Items.ExtraLife) == 0 &&
                    damageReport.victimMaster.inventory.GetItemCount(DLC1Content.Items.ExtraLifeVoid) == 0 &&
                    !BossGroup.FindBossGroup(damageReport.victimBody).IsNullOrDestroyed())
                {
                    //is boss last boss, if not return
                    if (BossGroup.FindBossGroup(damageReport.victimBody).combatSquad.memberCount > 1)
                    {
                        //call original method
                        orig(self, damageReport);

                        return;
                    }

                    //get the boss group type
                    int group;
                    try
                    {
                        group = DetermineGroup(damageReport.victimMaster);
                    }
                    catch
                    {
                        Logger.LogError("error in DetermineGroup");
                        group = -2;
                    }

                    switch (group)
                    {
                        case 1:
                            Logger.LogInfo("Doppelganger killed, revived dead players");
                            RespawnChar();
                            break;
                        case 2:
                            Logger.LogInfo("Teleporter Boss killed, revived dead players");
                            RespawnChar();
                            break;
                        case 3:
                            Logger.LogInfo("Special Boss killed, revived dead players");
                            RespawnChar();
                            break;
                        default:
                            Logger.LogInfo("Boss not valid, doing nothing");
                            break;
                    }
                }
                
                //call original method
                orig(self, damageReport);
            };
        }

        private int DetermineGroup(CharacterMaster master)
        {
            // get bossgroup
            BossGroup bossGroup;
            try
            {
                bossGroup = BossGroup.FindBossGroup(master.GetBody());
            }
            catch
            {
                Logger.LogError("unable to obtain bossgroup");
                return -1;
            }

            
            //is defeated boss group a doppelganger
            if (master.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) > 0 )
            {
                return Doppelganger.Value ? 1 : 0;
            }
            //is defeated bossgroup a teleporter boss
            if (bossGroup.GetComponent<TeleporterInteraction>())
            {
                return TeleporterBoss.Value ? 2 : 0;
            }

            //any other bose
            return MiscBoss.Value ? 3 : 0;
        }
        
        //respawn method to call
        private void RespawnChar()
        {
            //see if they are playing with others
            var solo = RoR2.RoR2Application.isInSinglePlayer || !NetworkServer.active;
            if (solo) return;
            //loop through every player and res the ones that are dead
            foreach (var playerCharacterMasterController
                     in RoR2.PlayerCharacterMasterController.instances)
            {
                var playerConnected = playerCharacterMasterController.isConnected;
                var isDead = !playerCharacterMasterController.master.GetBody()
                             || playerCharacterMasterController.master.IsDeadAndOutOfLivesServer()
                             || !playerCharacterMasterController.master.GetBody().healthComponent.alive;
                if (playerConnected && isDead)
                    playerCharacterMasterController.master.RespawnExtraLife();
            }
            return;
        }
    }
}