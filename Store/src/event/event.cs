using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using static CounterStrikeSharp.API.Core.Listeners;
using static Store.Store;
using static StoreApi.Store;
using Player = StoreApi.Store.Player;

namespace Store;

public static class Event
{
    public static void Load()
    {
        Instance.RegisterListener<OnMapStart>((mapname) =>
        {
            Instance.GlobalStoreItemTypes.ForEach((type) =>
            {
                type.MapStart();
            });
        });

        Instance.RegisterListener<OnTick>(() =>
        {
            Instance.GlobalTickrate++;

            if (Instance.GlobalTickrate % 10 != 0)
            {
                return;
            }

            foreach (CCSPlayerController player in Utilities.GetPlayers())
            {
                if (player == null || !player.Valid() || !player.PawnIsAlive)
                {
                    continue;
                }

                OnTick_CreateTrail(player);
                OnTick_ColoredSkin(player);
                OnTick_GrenadeTrail();
            }
        });

        Instance.RegisterListener<OnEntityCreated>((entity) =>
        {
            OnEntityCreated_Smoke(entity);
            OnEntityCreated_GrenadeTrail(entity);
            OnEntityCreated_CustomWeapon(entity);
        });

        Instance.RegisterListener<OnMapStart>((mapname) =>
        {
            Database.Execute("DELETE FROM store_items WHERE DateOfExpiration < NOW() AND DateOfExpiration > '0001-01-01 00:00:00';", null);

            List<Store_Item> itemsToRemove = Instance.GlobalStorePlayerItems
            .Where(item => item.DateOfExpiration < DateTime.Now && item.DateOfExpiration > DateTime.MinValue)
            .ToList();

            foreach (Store_Item? item in itemsToRemove)
            {
                Database.Execute("DELETE FROM store_equipment WHERE SteamID == @SteamID AND UniqueId == @UniqueId", new { item.SteamID, item.UniqueId });

                Instance.GlobalStorePlayerItems.Remove(item);
                Instance.GlobalStorePlayerEquipments.RemoveAll(i => i.UniqueId == item.UniqueId);
            }
        });

        Instance.RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            CCSPlayerController? player = @event.Userid;

            if (player == null || !player.Valid())
            {
                return HookResult.Continue;
            }

            Task.Run(() => Database.LoadPlayer(player));

            if (!Instance.GlobalDictionaryPlayer.TryGetValue(player, out Player? value))
            {
                value = new Player();
                Instance.GlobalDictionaryPlayer.Add(player, value);
            }

            value.CreditIntervalTimer = Instance.AddTimer(Instance.Config.Credits["interval_active_inactive"], () =>
            {
                if (GameRules.IgnoreWarmUp())
                {
                    return;
                }

                CsTeam Team = player.Team;

                switch (Team)
                {
                    case CsTeam.Terrorist:
                    case CsTeam.CounterTerrorist:
                        {
                            if (Instance.Config.Credits["amount_active"] > 0)
                            {
                                Credits.Give(player, Instance.Config.Credits["amount_active"]);

                                player.PrintToChatMessage("credits_earned<active>", Instance.Config.Credits["amount_active"]);
                            }

                            break;
                        }
                    case CsTeam.Spectator:
                        {
                            if (Instance.Config.Credits["amount_inactive"] > 0)
                            {
                                Credits.Give(player, Instance.Config.Credits["amount_inactive"]);

                                player.PrintToChatMessage("credits_earned<inactive>", Instance.Config.Credits["amount_inactive"]);
                            }

                            break;
                        }
                }
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

            return HookResult.Continue;
        });

        Instance.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            CCSPlayerController? player = @event.Userid;

            if (player == null || !player.Valid())
            {
                return HookResult.Continue;
            }

            if (!Instance.GlobalDictionaryPlayer.TryGetValue(player, out Player? value))
            {
                return HookResult.Continue;
            }

            value?.CreditIntervalTimer?.Kill();

            Database.SavePlayer(player);

            Instance.GlobalStorePlayers.RemoveAll(p => p.SteamID == player.SteamID);
            Instance.GlobalStorePlayerItems.RemoveAll(i => i.SteamID == player.SteamID);
            Instance.GlobalStorePlayerEquipments.RemoveAll(e => e.SteamID == player.SteamID);

            return HookResult.Continue;
        });

        Instance.RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            if (GameRules.IgnoreWarmUp())
            {
                return HookResult.Continue;
            }

            CCSPlayerController victim = @event.Userid;
            CCSPlayerController attacker = @event.Attacker;

            if (victim == null || attacker == null || !victim.Valid() || !attacker.Valid() || victim == attacker)
            {
                return HookResult.Continue;
            }

            Server.NextFrame(() =>
            {
                Database.SavePlayer(victim);
            });

            if (Instance.Config.Credits["amount_kill"] > 0)
            {
                Credits.Give(attacker, Instance.Config.Credits["amount_kill"]);

                attacker.PrintToChat(Instance.Localizer["Prefix"] + Instance.Localizer["credits_earned<kill>", Instance.Config.Credits["amount_kill"]]);
            }

            return HookResult.Continue;
        });
    }
}
