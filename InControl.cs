using HarmonyLib;
using HMLLibrary;
using UnityEngine;

namespace InControl
{
    public class InControl : Mod
    {
        public static readonly RecipeManager RecipeManager = new RecipeManager("recipes.json");
        public static readonly DropperManager DropperManager = new DropperManager("random_drops.json");
        
        public static Harmony harmony;
        
        public void Start()
        {
            (harmony = new Harmony("LIMachi.InControl")).PatchAll();
            RecipeManager.Load();
            DropperManager.Load();
            RecipeManager.Enable(true);
            DropperManager.Enable(true);
            Debug.Log("InControl: Mod loaded");
        }

        public void OnModUnload()
        {
            RecipeManager.Enable(false);
            DropperManager.Enable(false);
            harmony.UnpatchAll(harmony.Id);
            Debug.Log("InControl: Mod unloaded");
        }
        
        public class HarmonyTests
        {
            [HarmonyPatch(typeof(YieldHandler))]
            [HarmonyPrefix]
            [HarmonyPatch("CollectYield")]
            static bool Prefix_CollectYield(YieldHandler __instance, object[] __args, ref bool __result)
            {
                Debug.Log("InControl: Collecting yields: " + (Network_Player)__args[0]);
                Debug.Log(__instance + " -> " + __instance.Yield[0].item);
                return true;
            }
        }
    }
}