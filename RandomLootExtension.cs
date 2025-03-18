using HarmonyLib;
using HMLLibrary;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

[HarmonyPatch(typeof(RandomDropper))]
public class RandomLootExtension : Mod
{
    static Dictionary<string, ERandomizer> RandomExtension = new Dictionary<string, ERandomizer>();

    public string jsonPath = HLib.path_modsFolder + "\\ModData\\RandomLootExtension.json";
    public string examplePath = HLib.path_modsFolder + "\\ModData\\RandomLootExtension.example.json";

    Harmony harmony;

    public void Start()
    {
        (harmony = new Harmony("LIMachi.RandomLootExtension")).PatchAll();

        ERandomizer.Start();

        if (!File.Exists(jsonPath))
        {
            var f = new StreamWriter(new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None));
            f.Write(
 @"{
    ""Pickup_Floating_Barrel"": {
        ""items"": {
            ""Sand"": 5,
            ""Clay"": 5
        }
    }
}");
            f.Flush();
            f.Close();
        }

        if (!File.Exists(examplePath))
        {
            var f = new StreamWriter(new FileStream(examplePath, FileMode.Create, FileAccess.Write, FileShare.None));
            f.Write(
 @"{
    ""Pickup_Floating_Barrel"": { //the name of the pickup to modify, can only be a random loot (you can't change the pickup of plastic)
        ""amount"": { ""min"": 8, ""max"": 12 }, //replace the default random amount with this new range (will generate between 8 and 12 items, instead of the default 4 to 6 items)
        ""replace"": true, //if present and set to true, remove the default loot table and replace it with ours (if not present or false, keep the default loot table and extend it with ours)
        ""items"": { //map of item names and weight (weight are not persent chance of getting an item, but the chance relative to other items, if there is 2 items at weight 0.2, 1 item at weight 0.1 and we add ours with a weight of 0.1, we have 1 in 6 chance to be selected, not 10%)
            ""Sand"": 5, //the default total weight of a barrel is 212, so to have around %50 chance to get our item, it would need a weight of 106! (the Raw_Potato as a weight of 7 so about 3% for example)
            ""Clay"": 5 //adding 5 sand and 5 clay means we have a total weight of 222, so we have about 2.25% chance of clay/sand per roll of the barrel
        }
    },
    ""Pickup_Floating_Box"": {}, //all the fields are optional, this would do nothing to the floating crate (the default total weight is the same as the barrel, so 212)
    ""Pickup_Landmark_LandmarkCrateRaft"": { //this is the name of the chest you can find on drifting rafts (the default total weight is 159)
        ""amount"": { ""min"": 20, ""max"": 30 },
        ""items"": {
            ""Battery"": 1
        }
    }
}");
            f.Flush();
            f.Close();
        }

        var dict = JsonSerializer.Create().Deserialize<Dictionary<string, JRandomizer>>(new JsonTextReader(new StringReader(File.ReadAllText(jsonPath))));

        RandomExtension.Clear();

        foreach (var e in dict)
        {
            var t = new ERandomizer(e.Value);
            if (!t.discard())
                RandomExtension.Add(e.Key, t);
        }
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(harmony.Id);
    }

    class Range
    {
        public int? min;
        public int? max;
    }

    class JRandomizer
    {
        public Range amount; //optional, if not set use the same values as the vanilla dropper
        public bool replace; //if true, replace the loot table of the dropper by this one, if false, keep the original items and add the items listed
        public Dictionary<string, float> items; //list of pairs of item name and weight to be dropped (contrary to a percent chance, weight is relative to other weigths, a weight of 1 if there is another item with a weight of 0.5 means there is 2/3 chance to get the first item)
    }

    class ERandomizer
    {
        Range amount;
        bool extend;
        List<RandomItem> items;

        public ERandomizer(JRandomizer json)
        {
            amount = json.amount;
            extend = !json.replace;
            items = new List<RandomItem>();
            if (json.items != null)
                foreach (var ri in json.items)
                {
                    if (ri.Value > 0) {
                        var item = ItemManager.GetItemByName(ri.Key);
                        if (item != null)
                        {
                            items.Add(new RandomItem
                            {
                                weight = ri.Value,
                                obj = item
                            });
                        }
                    }
                }
        }

        public bool discard()
        {
            return extend && (items == null || items.Count == 0);
        }

        static FieldInfo IntervalField;
        static FieldInfo RandomDropperField;

        public static void Start()
        {
            IntervalField = AccessTools.Field(typeof(RandomDropper), "amountOfItems");
            RandomDropperField = AccessTools.Field(typeof(RandomDropper), "randomDropperAsset");
        }

        public Item_Base[] patch(RandomDropper dropper)
        {
            if (IntervalField.GetValue(dropper) is Interval_Int interval)
            {
                if (IntervalField != null && amount != null && (amount.min != null || amount.max != null))
                {
                    if (amount.min != null)
                        interval.minValue = (int)amount.min;
                    if (amount.max != null)
                        interval.maxValue = (int)amount.max;
                }

                if (RandomDropperField != null && RandomDropperField.GetValue(dropper) is SO_RandomDropper asset && asset.randomizer is Randomizer randomizer)
                {
                    Randomizer o = new Randomizer();
                    if (extend)
                    {
                        var l = randomizer.items.ToList();
                        l.AddRange(items);
                        o.items = l.ToArray();
                    }
                    else
                        o.items = items.ToArray();
                    o.lastRandomizedIndex = randomizer.lastRandomizedIndex;
                    return o.GetRandomItems<Item_Base>(interval.GetRandomValue());
                }
            }
            return null;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetRandomItems")]
    static bool Prefix_GetRandomItems(RandomDropper __instance, ref Item_Base[] __result)
    {
        if (__instance != null && __instance.gameObject != null && __instance.gameObject.GetInstanceID() != 0)
            foreach (var p in RandomExtension)
                if (__instance.gameObject.name.Contains(p.Key)) {
                    var t = p.Value.patch(__instance);
                    if (t != null)
                    {
                        __result = t;
                        return false;
                    }
                }
        return true;
    }
}