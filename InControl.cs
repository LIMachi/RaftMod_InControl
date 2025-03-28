using HarmonyLib;
using HMLLibrary;
using UnityEngine;

namespace InControl
{
    public class InControl : Mod
    {
        public static readonly RecipeManager RecipeManager = new RecipeManager("recipes.json");
        public static readonly DropperManager DropperManager = new DropperManager("random_drops.json");
        public static readonly ProcessingManager ProcessingManager = new ProcessingManager("processing_recipes.json", "processing_blocks.json");
        
        public static Harmony harmony;
        
        public void Start()
        {
            (harmony = new Harmony("LIMachi.InControl")).PatchAll();
            RecipeManager.Load();
            ProcessingManager.Load();
            DropperManager.Load();
            RecipeManager.Enable(true);
            ProcessingManager.Enable(true);
            DropperManager.Enable(true);
            Debug.Log("InControl: Mod loaded");
        }

        public void OnModUnload()
        {
            RecipeManager.Enable(false);
            ProcessingManager.Enable(false);
            DropperManager.Enable(false);
            harmony.UnpatchAll(harmony.Id);
            Debug.Log("InControl: Mod unloaded");
        }
    }
}