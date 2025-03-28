using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace InControl
{
    public class ProcessingManager
    {
        protected Dictionary<Item_Base, ItemInstance_Cookable> Overrides = new Dictionary<Item_Base, ItemInstance_Cookable>();
        protected Dictionary<Item_Base, (CookingModel, CookingModel)> Models = new Dictionary<Item_Base, (CookingModel, CookingModel)>();
        protected Dictionary<Item_Base, ProcessingKind> ProcessingKinds = new Dictionary<Item_Base, ProcessingKind>();
        
        protected static List<ProcessingKind> Processors = new List<ProcessingKind>();
        protected static Dictionary<Item_Base, ProcessingKind> KindFromSlot = new Dictionary<Item_Base, ProcessingKind>();

        public static readonly ProcessingKind UnknownProcessor = new ProcessingKind()
        {
            id = 0,
            name = "Unknown",
            name_contains = null,
            has_custom_processing_models = false
        };
        
        private bool _active = false;
        public static string DefaultRecipes = @"{}";
        
        public static string Example = @"{
    ""CircuitBoard"": { //name of the item to process
        ""cook_time"": 90, //time (in seconds) it takes to process the item
        ""result"": ""CopperIngot"", //what item is created by the processing
        ""amount"": 1, //how much of that item
        ""raw_model"": {
            ""raw"": ""SeaVine"", //'model' to use while processing (will load the model associated with this item)
            ""cooked"": true //if 'cooked' is true, it will use the cooked model instead
        },
      ""cooked_model"": {
            ""raw"": ""CopperOre""
            //if cooked is omitted, it will default to true for ""cooked_model"" and false for ""raw_model""
      },
      ""processing_kind"": ""Smelting"" //should match the name declared in the ProcessingBlocks file
    }
}";

        public static string DefaultProcessingBlocks = @"{
    ""Smelting"": { ""name_contains"": ""Placeable_CookingStand_Smelter"" },
    ""Purifying"": { ""name_contains"": ""Placeable_CookingStand_Purifier"", ""has_custom_processing_models"": false },
    ""Roasting"": { ""name_contains"": ""Placeable_CookingStand_Food"" },
    ""Liquefying"": { ""name_contains"": ""Placeable_CookingStand_PaintMill"" }
}";
        
        public readonly string File;
        public readonly string ProcessingBlocksFile;

        public static ProcessingKind KindOfCookingStand(string cookingStandName)
        {
            foreach (var kind in Processors)
                if (kind.id != 0 && cookingStandName.Contains(kind.name_contains))
                    return kind;
            return Processors[0];
        }

        public ProcessingManager(string file, string processingBlocksFile)
        {
            File = file;
            ProcessingBlocksFile = processingBlocksFile;
            FileUtils.CreateIfMissing(File.Replace(".json", ".example.json"), Example);
        }
        
        public void Load()
        {
            if (Active)
                Enable(false);
            Overrides.Clear();
            Models.Clear();
            ProcessingKinds.Clear();
            Processors.Clear();
            Processors.Add(UnknownProcessor);
            foreach (var e in JsonUtils.Deserialize<Dictionary<string, Processor>>(FileUtils.ReadOrCreateDefault(ProcessingBlocksFile, DefaultProcessingBlocks)))
                Processors.Add(new ProcessingKind()
                {
                    id = Processors.Count,
                    name = e.Key,
                    name_contains = e.Value.name_contains,
                    has_custom_processing_models = e.Value.has_custom_processing_models ?? true
                });
            foreach (var e in JsonUtils.Deserialize<Dictionary<Item_Base, SimplifiedProcessing>>(
                         FileUtils.ReadOrCreateDefault(File, DefaultRecipes)))
            {
                Overrides.Add(e.Key, e.Value.Transform());
                if (e.Key.settings_cookable.CookingResult.item != null && KindOfCookingStand(e.Key).name.Contains("Purif"))
                    Debug.LogWarning("Replacing default processing for " + e.Key);
                ProcessingKinds.Add(e.Key, e.Value._processing_kind);
                if (e.Value._processing_kind.has_custom_processing_models)
                    Models.Add(e.Key, (e.Value.raw_model, e.Value.cooked_model));
            }
        }
        
        public class Processor
        {
            public string name_contains;
            public bool? has_custom_processing_models;
        }

        public class ProcessingKind
        {
            public int id;
            public string name;
            public string name_contains;
            public bool has_custom_processing_models;
        }

        public static ProcessingKind KindOfCookingStand(Item_Base item)
        {
            if (KindFromSlot.Count == 0)
            {
                var set = new HashSet<Block_CookingStand>();
                foreach (var test in Resources.FindObjectsOfTypeAll<Block_CookingStand>())
                {
                    if (test.name.Contains("(Clone)") || set.Contains(test))
                        continue;
                    ProcessingKind kind = KindOfCookingStand(test.name);
                    set.Add(test);
                    foreach (var slot in test.gameObject.GetComponentsInChildren<CookingSlot>())
                    {
                        foreach (var connection in Traverse.Create(slot).Field("itemConnections").GetValue<List<CookItemConnection>>())
                            if (KindFromSlot.ContainsKey(connection.cookableItem))
                            {
                                if (kind != KindFromSlot[connection.cookableItem])
                                    Debug.LogError("unexpected double connection for item: " + connection.cookableItem);
                            } else 
                                KindFromSlot.Add(connection.cookableItem, kind);
                    }
                }
            }
            if (KindFromSlot.ContainsKey(item))
                return KindFromSlot[item];
            return UnknownProcessor;
        }
        
        public bool Active { get => _active; }

        public string Dump()
        {
            var d = new Dictionary<Item_Base, SimplifiedProcessing>();
            foreach (var item in ItemManager.GetAllItems())
            {
                var t = SimplifiedProcessing.FromItem(item);
                if (!t.Disabled())
                    d.Add(item, t);
            }
            return JsonUtils.Serialize(d);
        }

        public void Enable(bool state)
        {
            if (state != _active)
            {
                ItemInstance_Cookable t;
                foreach (var key in Overrides.Keys.ToArray())
                {
                    t = Overrides[key];
                    Overrides[key] = key.settings_cookable;
                    key.settings_cookable = t;
                }
                _active = !_active;
            }
        }

        public class CookingModel
        {
            public Item_Base raw;
            public bool? cooked; //default to the vanilla
        }
        
        public class SimplifiedProcessing
        {
            public float? cook_time; //default to 60 so 1 minute
            public int? amount; //default to 1
            public Item_Base result;
            public int? slots_required; //default to 1
            public CookingModel raw_model; //default to original (raw) item, unused by purifiers
            public CookingModel cooked_model; //default to original (raw) item, unused by purifiers
            public string processing_kind;
            [JsonIgnore]
            public ProcessingKind _processing_kind;

            [OnDeserialized]
            internal void OnDeserialized(StreamingContext context)
            {
                _processing_kind = UnknownProcessor;
                foreach (var kind in Processors)
                    if (kind.id != 0 && processing_kind == kind.name)
                    {
                        _processing_kind = kind;
                        break;
                    }
            }
            
            public ItemInstance_Cookable Transform()
            {
                ItemInstance_Cookable o;
                if (Disabled())
                    o = new ItemInstance_Cookable(0, 0f, new Cost(null, 0));
                else
                    o = new ItemInstance_Cookable(slots_required ?? 1, cook_time ?? 60f, new Cost(result, amount ?? 1));
                return o;
            }

            public static SimplifiedProcessing FromItem(Item_Base item)
            {
                if (InControl.ProcessingManager.Active && InControl.ProcessingManager.Overrides.ContainsKey(item))
                {
                    var rep = item.settings_cookable;
                    var proc = InControl.ProcessingManager.ProcessingKinds[item];
                    var r = new SimplifiedProcessing
                    {
                        cook_time = rep.CookingTime,
                        amount = null,
                        result = rep.CookingResult.item,
                        slots_required = null,
                        raw_model = null,
                        cooked_model = null,
                        processing_kind = proc.name,
                        _processing_kind = proc
                    };
                    if (rep.CookingResult.amount != 1)
                        r.amount = rep.CookingResult.amount;
                    if (rep.CookingSlotsRequired != 1)
                        r.slots_required = rep.CookingSlotsRequired;
                    if (proc.has_custom_processing_models && InControl.ProcessingManager.Models.ContainsKey(item))
                    {
                        var models = InControl.ProcessingManager.Models[item];
                        r.raw_model = models.Item1;
                        r.cooked_model = models.Item2;
                    }
                    return r;
                }
                var recipe = item.settings_cookable;
                var o = new SimplifiedProcessing();
                o.cook_time = recipe.CookingTime;
                if (recipe.CookingResult.amount != 1)
                    o.amount = recipe.CookingResult.amount;
                if (recipe.CookingSlotsRequired != 1)
                    o.slots_required = recipe.CookingSlotsRequired;
                o.result = recipe.CookingResult.item;
                o._processing_kind = KindOfCookingStand(item);
                o.processing_kind = o._processing_kind.name;
                if (o._processing_kind.has_custom_processing_models)
                {
                    o.raw_model = new CookingModel();
                    o.cooked_model = new CookingModel();
                    o.raw_model.raw = o.cooked_model.raw = item;
                    o.raw_model.cooked = false;
                    o.cooked_model.cooked = true;
                }
                return o;
            }

            public bool Disabled()
            {
                return amount <= 0 || slots_required <= 0 || result == null;
            }
        }
        
        [HarmonyPatch(typeof(CookingSlot))]
        public static class CookingSlotTests
        {
            [HarmonyPrefix]
            [HarmonyPatch("CanCookItem")] //called to add insertion popup too!
            static bool Prefix_CanCookItem(CookingSlot __instance, object[] __args, ref bool __result)
            {
                if (!InControl.ProcessingManager.Active || __instance == null)
                    return true;
                Item_Base input = (Item_Base)__args[0];
                if (InControl.ProcessingManager.Overrides.ContainsKey(input))
                {
                    var stand = KindOfCookingStand(__instance.gameObject.GetComponentInParent<CookingStand>().name);
                    if (InControl.ProcessingManager.ProcessingKinds[input] == stand)
                    {
                        __result = true;
                        return false;
                    }
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("EnableCookedItem")]
            static bool Prefix_EnableCookedItem(CookingSlot __instance, object[] __args)
            {
                if (!InControl.ProcessingManager.Active || __instance == null)
                    return true;
                Item_Base input = (Item_Base)__args[0];
                if (InControl.ProcessingManager.Models.ContainsKey(input))
                {
                    CookingModel raw = InControl.ProcessingManager.Models[input].Item1;
                    CookingModel cooked = InControl.ProcessingManager.Models[input].Item2;
                    foreach (CookItemConnection itemConnection in Traverse.Create(__instance).Field("itemConnections").GetValue<List<CookItemConnection>>())
                    {
                        if (itemConnection.cookableItem == raw.raw)
                        {
                            if (cooked.raw == raw.raw && (cooked.cooked ?? true) == (raw.cooked ?? false))
                                continue;
                            if (raw.cooked ?? false)
                                itemConnection.SetCookedState(false);
                            else
                                itemConnection.SetRawState(false);
                        }
                        if (itemConnection.cookableItem == cooked.raw)
                        {
                            if (cooked.cooked ?? true)
                                itemConnection.SetCookedState(true);
                            else
                                itemConnection.SetRawState(true);
                        }
                    }
                    Action<CookingSlot> onSlotModified = __instance.OnSlotModified;
                    if (onSlotModified == null)
                        return false;
                    onSlotModified(__instance);
                    return false;
                }
                return true;
            }
            
            [HarmonyPrefix]
            [HarmonyPatch("EnableRawItem")]
            static bool Prefix_EnableRawItem(CookingSlot __instance, object[] __args)
            {
                if (!InControl.ProcessingManager.Active || __instance == null)
                    return true;
                Item_Base input = (Item_Base)__args[0];
                if (InControl.ProcessingManager.Models.ContainsKey(input))
                {
                    CookingModel raw = InControl.ProcessingManager.Models[input].Item1;
                    CookingModel cooked = InControl.ProcessingManager.Models[input].Item2;
                    foreach (CookItemConnection itemConnection in Traverse.Create(__instance).Field("itemConnections").GetValue<List<CookItemConnection>>())
                    {
                        if (itemConnection.cookableItem == raw.raw)
                        {
                            if (raw.cooked ?? false)
                                itemConnection.SetCookedState(true);
                            else
                                itemConnection.SetRawState(true);
                        }
                        if (itemConnection.cookableItem == cooked.raw)
                        {
                            if (cooked.raw == raw.raw && (cooked.cooked ?? true) == (raw.cooked ?? false))
                                continue;
                            if (cooked.cooked ?? true)
                                itemConnection.SetCookedState(false);
                            else
                                itemConnection.SetRawState(false);
                        }
                    }
                    Action<CookingSlot> onSlotModified = __instance.OnSlotModified;
                    if (onSlotModified == null)
                        return false;
                    onSlotModified(__instance);
                    return false;
                }
                return true;
            }
        }
    }
}