using CounterStrikeSharp.API.Core;

namespace Store;

public partial class Store
{
    public static void Health_OnPluginStart()
    {
        Item.RegisterType("health", Health_OnMapStart, Health_OnEquip, Health_OnUnequip, false, true);
    }
    public static void Health_OnMapStart()
    {
    }
    public static bool Health_OnEquip(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (!int.TryParse(item["healthValue"], out int health))
        {
            return false;
        }

        CCSPlayerPawn? playerPawn = player.PlayerPawn.Value;

        if (playerPawn == null)
        {
            return false;
        }

        if (!int.TryParse(Instance.Config.Settings["max_health"], out int maxhealth))
        {
            return false;
        }

        int currentHealth = playerPawn.GetHealth();
        int pawnMaxHealth = playerPawn.MaxHealth;

        if (maxhealth > 0)
        {
            if (currentHealth >= maxhealth)
            {
                return false;
            }

            if (currentHealth + health > maxhealth)
            {
                health = maxhealth - currentHealth;
            }
        }

        if (maxhealth == -1)
        {
            if (currentHealth >= pawnMaxHealth)
            {
                return false;
            }

            if (currentHealth + health > pawnMaxHealth)
            {
                health = pawnMaxHealth - currentHealth;
            }
        }

        player.SetHealth(currentHealth + health);

        return true;
    }

    public static bool Health_OnUnequip(CCSPlayerController player, Dictionary<string, string> item)
    {
        return true;
    }
}