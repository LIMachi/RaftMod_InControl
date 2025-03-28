using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace InControl
{
    public class RecipeManager
    {
        protected Dictionary<Item_Base, ItemInstance_Recipe> Overrides = new Dictionary<Item_Base, ItemInstance_Recipe>();
        private bool _active = false;
        public static string DefaultRecipes = "{}";

        public static string Example = @"{
    ""PlasticBottle_Empty"": {
        ""require_research"": false, //make the plastic bottle available immediately (does not require to research plastic or vine goo)
        ""amount_to_craft"": 2, //create 2 bottles per craft
        ""category"": ""Tools"", //changes the category of the item (valid: Tools, Other, Resources, Decorations, Equipment, Nothing, FoodWater, Navigation, CreativeMode, Hidden, Weapons, Skin)
        ""sub_category"": ""Axes"", //sub category, too many valid values to list them here, use the command `dumpCurrentRecipes`
        ""sub_category_order"": 3, //if the sub category is the row, then the order is the column
        ""recipe"": [ //a recipe is a list of amounts and list of interchangeable items
            {
                ""items"": [ ""Plastic"" ],
                ""amount"": 6
            },
            {
                ""items"": [ ""VineGoo"", ""Rope"" ], //here the craft will accept either 4 goo or 4 ropes (plus the 6 plastic listed above)
                ""amount"": 4
            }
        ]
    }
}";
        public readonly string File;

        public RecipeManager(string file)
        {
            File = file;
            FileUtils.CreateIfMissing(File.Replace(".json", ".example.json"), Example);
        }
        
        public bool Active { get => _active; }

        public void Load()
        {
            if (Active)
                Enable(false);
            Overrides.Clear();
            foreach (var e in JsonUtils.Deserialize<Dictionary<Item_Base, SimplifiedRecipe>>(FileUtils.ReadOrCreateDefault(File, DefaultRecipes)))
                Overrides.Add(e.Key, e.Value.Transform(e.Key.settings_recipe));
        }

        public string Dump()
        {
            var d = new Dictionary<Item_Base, SimplifiedRecipe>();
            foreach (var item in ItemManager.GetAllItems())
            {
                var t = SimplifiedRecipe.FromItemRecipe(item.settings_recipe);
                if (!t.Disabled())
                    d.Add(item, t);
            }
            return JsonUtils.Serialize(d);
        }

        public void Enable(bool state)
        {
            if (state != _active)
            {
                ItemInstance_Recipe t;
                foreach (var key in Overrides.Keys.ToArray())
                {
                    t = Overrides[key];
                    Overrides[key] = key.settings_recipe;
                    key.settings_recipe = t;
                }
                _active = !_active;
            }
        }
        
        public class SimplifiedRecipe
        {
            public bool? require_research;
            public int? amount_to_craft;
            public CostMultiple[] recipe;
            public CraftingCategory? category;
            public string sub_category;
            public int? sub_category_order;

            public ItemInstance_Recipe Transform(ItemInstance_Recipe original)
            {
                var o = original.Clone();
                if (Disabled())
                {
                    if (o.LearnedFromBeginning)
                        Traverse.Create(o).Field("learnedFromBeginning").SetValue(false);
                    if (o.Learned)
                        o.Learned = false;
                    if (o.AmountToCraft != 0)
                        Traverse.Create(o).Field("amountToCraft").SetValue(0);
                    o.NewCost = new CostMultiple[0];
                } else {
                    if (require_research == null || require_research.Value)
                    {
                        if (o.LearnedFromBeginning) {
                            Traverse.Create(o).Field("learnedFromBeginning").SetValue(false);
                            if (o.Learned)
                                o.Learned = false;
                        }
                    } else
                    {
                        if (!o.Learned)
                            o.Learned = true;
                        if (!o.LearnedFromBeginning)
                            Traverse.Create(o).Field("learnedFromBeginning").SetValue(true);
                    }
                    var tc = amount_to_craft ?? 1;
                    if (o.AmountToCraft != tc)
                        Traverse.Create(o).Field("amountToCraft").SetValue(tc);
                    o.NewCost = recipe;
                    foreach (var c in o.NewCost)
                        if (c.amount <= 0)
                            c.amount = 1;
                    if (category != null && category != o.CraftingCategory)
                        Traverse.Create(o).Field("craftingCategory").SetValue(category);
                    if (sub_category != null && sub_category != o.SubCategory)
                        Traverse.Create(o).Field("subCategory").SetValue(sub_category);
                    if (sub_category_order != null && sub_category_order != o.SubCategoryOrder)
                        Traverse.Create(o).Field("subCategoryOrder").SetValue(sub_category_order);
                }
                return o;
            }

            public static SimplifiedRecipe FromItemRecipe(ItemInstance_Recipe recipe)
            {
                var o = new SimplifiedRecipe();
                if (recipe.LearnedFromBeginning)
                    o.require_research = false;
                if (recipe.AmountToCraft != 1)
                    o.amount_to_craft = recipe.AmountToCraft;
                o.recipe = recipe.NewCost;
                o.category = recipe.CraftingCategory;
                o.sub_category = recipe.SubCategory;
                o.sub_category_order = recipe.SubCategoryOrder;
                return o;
            }

            public bool Disabled()
            {
                return amount_to_craft == 0 || recipe == null || recipe.Length == 0;
            }
        }
    }
}