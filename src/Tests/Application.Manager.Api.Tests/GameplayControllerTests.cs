using System.Text.Json;
using Application.Manager.Api.Controllers;
using Application.Manager.Api.Gameplay;
using Domain.Game.Rules;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Tests;

public sealed class GameplayControllerTests
{
    private static JsonElement PerksFor(GameplayConfig config)
    {
        var store = new GameplayConfigStore();
        store.Update(config);
        var result = Assert.IsType<JsonResult>(new GameplayController(store).Perks());
        return JsonDocument.Parse(JsonSerializer.Serialize(result.Value, GameplayConfig.Json)).RootElement.GetProperty("perks");
    }

    [Fact]
    public void Perks_ShowsThePlayersTheTiersOfAPerk_WithoutTheFirstOne()
    {
        var config = GameplayConfig.Defaults with
        {
            Perks = new PerkOptions
            {
                Perks = [new PerkDefinition { Id = "m", Name = "M", FoodsRequired = 3, Effect = PerkEffect.Magnet, Amount = 10, Tiers = [new PerkTier { FoodsRequired = 9, Multiplier = 1.5 }] }]
            }
        };

        var perk = PerksFor(config).EnumerateArray().Single();

        var tier = perk.GetProperty("tiers").EnumerateArray().Single();
        Assert.Equal((9, 1.5), (tier.GetProperty("foodsRequired").GetInt32(), tier.GetProperty("multiplier").GetDouble()));
    }

    [Fact]
    public void Perks_APerkWithoutTiersHasAnEmptyList()
    {
        var config = GameplayConfig.Defaults with
        {
            Perks = new PerkOptions { Perks = [new PerkDefinition { Id = "m", Name = "M", FoodsRequired = 3, Effect = PerkEffect.Magnet, Amount = 10 }] }
        };

        Assert.Equal(0, PerksFor(config).EnumerateArray().Single().GetProperty("tiers").GetArrayLength());
    }

    [Fact]
    public void Perks_ACooldownPerkSaysHowLongItRechargesAndLasts_AndAnEffectItsTarget()
    {
        var config = GameplayConfig.Defaults with
        {
            Perks = new PerkOptions
            {
                Perks =
                [
                    new PerkDefinition
                    {
                        Id = "s", Name = "S", FoodsRequired = 3, CooldownSeconds = 30, DurationSeconds = 4,
                        Effects = [new PerkEffectEntry { Effect = PerkEffect.ShieldBurst, Amount = 1 }, new PerkEffectEntry { Effect = PerkEffect.EventResistance, Amount = 0.4, Target = "zone" }]
                    }
                ]
            }
        };

        var perk = PerksFor(config).EnumerateArray().Single();

        Assert.Equal((30, 4), (perk.GetProperty("cooldownSeconds").GetInt32(), perk.GetProperty("durationSeconds").GetInt32()));
        Assert.Equal("zone", perk.GetProperty("effects")[1].GetProperty("target").GetString());
    }
}
