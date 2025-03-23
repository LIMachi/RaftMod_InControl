using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace InControl
{
    [HarmonyPatch(typeof(RandomDropper))]
    public class DropperManager
    {
        public bool Logging = false;
        protected Dictionary<string, DropperEx> Extensions = new Dictionary<string, DropperEx>();
        private bool _active = false;
        public bool Active => _active;
        public static string Defaultdroppers =  @"{
    ""Pickup_Floating_Barrel"": {
        ""items"": {
            ""Sand"": 5,
            ""Clay"": 5
        }
    },
    ""Pickup_Floating_Box"": {
        ""items"": {
            ""Sand"": 5,
            ""Clay"": 5
        }
    }
}";
        public static string Example = @"{
    ""Pickup_Floating_Barrel"": { //the name of the pickup to modify, can only be a random loot (you can't change the pickup of plastic)
        ""min"": 8, //replace the default random amount with this new range (will generate between 8 and 12 items, instead of the default 4 to 6 items)
        ""max"": 12,
        ""replace"": true, //if present and set to true, remove the default loot table and replace it with ours (if not present or false, keep the default loot table and extend it with ours)
        ""items"": { //map of item names and weight (weight are not persent chance of getting an item, but the chance relative to other items, if there is 2 items at weight 0.2, 1 item at weight 0.1 and we add ours with a weight of 0.1, we have 1 in 6 chance to be selected, not 10%)
            ""Sand"": 5, //the default total weight of a barrel is 212, so to have around %50 chance to get our item, it would need a weight of 106! (the Raw_Potato as a weight of 7 so about 3% for example)
            ""Clay"": 5 //adding 5 sand and 5 clay means we have a total weight of 222, so we have about 2.25% chance of clay/sand per roll of the barrel
        }
    },
    ""Pickup_Floating_Box"": {}, //all the fields are optional, this would do nothing to the floating crate (the default total weight is the same as the barrel, so 212)
    ""Pickup_Landmark_LandmarkCrateRaft"": { //this is the name of the chest you can find on drifting rafts (the default total weight is 159)
        ""max"": 30,
        ""items"": {
            ""Battery"": 1
        }
    }
}";
        public readonly string File;

        public DropperManager(string file)
        {
            File = file;
            FileUtils.CreateIfMissing(File.Replace(".json", ".example.json"), Example);
        }
        
        public void Load()
        {
            Extensions = JsonUtils.Deserialize<Dictionary<string, DropperEx>>(FileUtils.ReadOrCreateDefault(File, Defaultdroppers));
        }

        public void Enable(bool state)
        {
            _active = state;
        }
        
        public class DropperEx
        {
            public int? min; //minimum amount of item to generate, default to overriden dropper
            public int? max; //maximum amount of item to generate, default to overriden dropper
            public bool? replace; //should this override the default behavior completely, default to false
            public Dictionary<Item_Base, float> items; //transformed to List<RandomItem>

            [JsonIgnore] public List<RandomItem> weights;

            [OnDeserialized]
            public void OnDeserializedMethod(StreamingContext context)
            {
                RecalculateItems();
            }

            public void RecalculateItems()
            {
                if (items == null)
                    items = new Dictionary<Item_Base, float>();
                weights = new List<RandomItem>();
                foreach (var w in items)
                    weights.Add(new RandomItem()
                    {
                        weight = w.Value,
                        obj = w.Key,
                    });
            }

            public static DropperEx FromDropper(RandomDropper dropper)
            {
                var o = new DropperEx();
                var range = Traverse.Create(dropper).Field("amountOfItems").GetValue<Interval_Int>();
                o.min = range.minValue;
                o.max = range.maxValue;
                o.replace = true;
                o.items = new Dictionary<Item_Base, float>();
                var drops = Traverse.Create(dropper).Field("randomDropperAsset").GetValue<SO_RandomDropper>();
                foreach (var ri in drops.randomizer.items)
                    o.items.Add((Item_Base)ri.obj, ri.weight);
                o.RecalculateItems();
                return o;
            }

            public Item_Base[] Patch(RandomDropper dropper)
            {
                Randomizer o = new Randomizer();
                var dr = Traverse.Create(dropper).Field("amountOfItems").GetValue<Interval_Int>();
                var range = new Interval_Int
                {
                    minValue = min ?? dr.minValue,
                    maxValue = max ?? dr.maxValue
                };
                var drops = Traverse.Create(dropper).Field("randomDropperAsset").GetValue<SO_RandomDropper>();
                if (replace != null && (bool)replace)
                    o.items = weights.ToArray();
                else
                {
                    var l = drops.randomizer.items.ToList();
                    l.AddRange(weights);
                    o.items = l.ToArray();
                }

                o.lastRandomizedIndex = drops.randomizer.lastRandomizedIndex;
                return o.GetRandomItems<Item_Base>(range.GetRandomValue());
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch("GetRandomItems")]
        static bool Prefix_GetRandomItems(RandomDropper __instance, ref Item_Base[] __result)
        {
            if (InControl.DropperManager.Logging)
                Debug.Log(__instance.gameObject.name + ": " + JsonUtils.Serialize(DropperEx.FromDropper(__instance)));
            if (!InControl.DropperManager.Active || __instance == null || __instance.gameObject == null || __instance.gameObject.GetInstanceID() == 0)
                return true;
            foreach (var p in InControl.DropperManager.Extensions)
                if (__instance.gameObject.name.Contains(p.Key))
                {
                    var t = p.Value.Patch(__instance);
                    if (t == null) continue;
                    foreach (var item in t)
                    {
                        Debug.Log(item);
                    }
                    __result = t;
                    return false;
                }
            return true;
        }
    }
}