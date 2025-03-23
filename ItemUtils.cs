namespace InControl
{
    public static class ItemUtils
    {
        public static Item_Base BestItemMatch(string name)
        {
            var all = ItemManager.GetAllItems();
            var bestDist = int.MaxValue;
            Item_Base best = null;
            var tester = new SlidingLevenshtein(name.ToLower().Replace(' ',  '_'));
            foreach (var item in all)
            {
                var t = tester.Distance(item.UniqueName.ToLower().Replace(' ', '_'));
                if (t < bestDist)
                {
                    best = item;
                    bestDist = t;
                }
                t = tester.Distance(item.settings_Inventory.DisplayName.ToLower().Replace(' ',  '_'));
                if (t < bestDist)
                {
                    best = item;
                    bestDist = t;
                }
                if (t == 0)
                    break;
            }
            return bestDist <= name.Length / 2 ? best : null;
        }
    }
}