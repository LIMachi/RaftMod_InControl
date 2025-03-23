using RaftModLoader;
using UnityEngine;

namespace InControl
{
    public class Commands
    {
        [ConsoleCommand(name: "inControl_toggleCustomRecipes")]
        public static void ToggleCustomRecipes(string[] args)
        {
            InControl.RecipeManager.Enable(!InControl.RecipeManager.Active);
            Debug.Log("Custom Recipes: " + (InControl.RecipeManager.Active ? "Enabled" : "Disabled"));
        }

        [ConsoleCommand(name: "inControl_reloadCustomRecipes")]
        public static void ReloadCustomRecipes(string[] args)
        {
            var active = InControl.RecipeManager.Active;
            InControl.RecipeManager.Load();
            InControl.RecipeManager.Enable(active);
        }

        [ConsoleCommand(name: "inControl_dumpCurrentRecipes")]
        public static void DumpCurrentRecipes(string[] args)
        {
            FileUtils.WriteEntireFile("Recipes.dump.json", InControl.RecipeManager.Dump());
            Debug.Log("Recipes saved to: " + FileUtils.ModDataPath("Recipes.dump.json"));
        }
        
        [ConsoleCommand(name: "inControl_toggleCustomDroppers")]
        public static void ToggleCustomDroppers(string[] args)
        {
            InControl.DropperManager.Enable(!InControl.DropperManager.Active);
            Debug.Log("Custom Drops: " + (InControl.DropperManager.Active ? "Enabled" : "Disabled"));
        }

        [ConsoleCommand(name: "inControl_reloadCustomDroppers")]
        public static void ReloadCustomDroppers(string[] args)
        {
            var active = InControl.DropperManager.Active;
            InControl.DropperManager.Load();
            InControl.DropperManager.Enable(active);
        }
        
        [ConsoleCommand(name: "inControl_logRandomDroppers")]
        public static void LogRandomDroppers(string[] args)
        {
            InControl.DropperManager.Logging = !InControl.DropperManager.Logging;
            Debug.Log("Random droppers loot logger: " + (InControl.DropperManager.Logging ? "Enabled" : "Disabled"));
        }
        
        [ConsoleCommand(name: "inControl_searchItem")]
        public static void SearchItem(string[] args)
        {
            if (args.Length == 0)
                Debug.Log("require at least one item name");
            else
                foreach (var n in args)
                {
                    var f = ItemUtils.BestItemMatch(n);
                    if (f != null)
                        Debug.Log(n + " -> " + f.UniqueName + " / " + f.settings_Inventory.DisplayName);
                    else
                        Debug.Log(n + " -> Could not find item!");
                }
        }
    }
}