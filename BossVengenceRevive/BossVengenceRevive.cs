using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Configuration;
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
        private const string PluginVersion = "2.0.2";
        
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
                    damageReport.victimMaster.inventory.GetItemCount(DLC1Content.Items.ExtraLifeVoid) == 0)
                {
                    //is character last member
                    if (BossGroup.FindBossGroup(damageReport.victimBody).combatSquad.memberCount <= 1)
                    {
                        //is defeated boss group a doppelganger
                        if (damageReport.victimMaster.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) > 0 )
                        {
                            if (Doppelganger.Value)
                            {
                                Logger.LogInfo("Doppelganger killed, revived dead players");
                                RespawnChar();
                            }
                        }
                        //is defeated bossgroup a teleporter boss
                        else if (BossGroup.FindBossGroup(damageReport.victimBody).GetComponent<TeleporterInteraction>())
                        {
                            if (TeleporterBoss.Value)
                            {
                                Logger.LogInfo("Teleporter Boss killed, revived dead players");
                                RespawnChar();
                            }
                        }
                        //any other bose
                        else if(MiscBoss.Value)
                        {
                            Logger.LogInfo("Special Boss killed, revived dead players");
                            RespawnChar();
                        }
                        
                    }
                }
                
                //call original method
                orig(self, damageReport);
            };
        }
        
        //respawn method to call
        public void RespawnChar()
        {
            //see if they are playing with others
            Logger.LogInfo("boss event cleared");
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