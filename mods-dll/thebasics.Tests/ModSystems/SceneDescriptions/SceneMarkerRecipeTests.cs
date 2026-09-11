using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneMarkerRecipeTests
{
    [Theory]
    [InlineData("chert", true)]
    [InlineData("chert", false)]
    [InlineData("granite", true)]
    public void TwoStonesMatchAndConsumeExactlyTwo(string rock, bool separate)
    {
        var recipe = LoadRecipe();
        var stone = new Item { Code = new AssetLocation("game:stone-" + rock), ItemId = 1 };
        stone.OnLoadedNative(Substitute.For<ICoreAPI>());
        ItemSlot[] slots = separate
            ? [new DummySlot(new ItemStack(stone, 1)), new DummySlot(new ItemStack(stone, 1))]
            : [new DummySlot(new ItemStack(stone, 3)), new DummySlot()];

        recipe.Match(slots).Should().BeTrue();
        recipe.Consume(slots);
        slots.Sum(slot => slot.Itemstack?.StackSize ?? 0).Should().Be(separate ? 0 : 1);
    }

    [Theory]
    [InlineData("game:stone-chert", 1)]
    [InlineData("game:stick", 2)]
    public void InsufficientOrWrongIngredientDoesNotMatch(string code, int count)
    {
        var item = new Item { Code = new AssetLocation(code), ItemId = 1 };
        LoadRecipe().Match([new DummySlot(new ItemStack(item, count)), new DummySlot()]).Should().BeFalse();
    }

    private static RecipeProbe LoadRecipe()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "mods-dll", "thebasics", "assets", "thebasics", "recipes", "grid", "scene-marker.json")))
            directory = directory.Parent;
        var path = Path.Combine(directory!.FullName, "mods-dll", "thebasics", "assets", "thebasics", "recipes", "grid", "scene-marker.json");
        var recipe = JsonConvert.DeserializeObject<RecipeProbe>(File.ReadAllText(path))!;
        recipe.ResolvedIngredients = recipe.IngredientPattern!.Select(symbol => recipe.Ingredients![symbol.ToString()].Clone()).ToArray();
        foreach (var ingredient in recipe.ResolvedIngredients)
            ingredient!.MatchingType = IRecipeIngredient.GetMatchType(ingredient.Code!.ToString(), ingredient.Name != null);
        return recipe;
    }

    public class RecipeProbe : GridRecipe
    {
        public bool Match(ItemSlot[] slots) => MatchesShapeLess(slots, null!, ResolvedIngredients!);
        public void Consume(ItemSlot[] slots) => ConsumeInputShapeLess(null!, slots, ResolvedIngredients!);
    }
}
