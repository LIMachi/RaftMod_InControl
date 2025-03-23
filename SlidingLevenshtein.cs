using System;

namespace InControl
{
    public class SlidingLevenshtein //TODO: replace with better/human matching algorithm (ex: bater does not match battery)
    {
        private readonly string _target;
        private readonly int[] _costs;

        public SlidingLevenshtein(string target)
        {
            if (target != null) {
                _target = target;
                _costs = new int[target.Length];
            }
            else
            {
                _target = "";
                _costs = Array.Empty<int>();
            }
        }

        public int Distance(string against)
        {
            if (string.IsNullOrEmpty(against))
                return _costs.Length == 0 ? 0 : int.MaxValue;
            if (_costs.Length == 0 || _costs.Length > against.Length)
                return int.MaxValue;
            var best = int.MaxValue;
            var delta = against.Length - _costs.Length;
            for (var w = 0; w <= delta; ++w)
            {
                var window = against.Substring(w, _costs.Length);
                for (var i = 0; i < _costs.Length;)
                    _costs[i] = ++i;
                for (var i = 0; i < window.Length; i++)
                {
                    var topCost = i;
                    var previousCost = i;
                    var c = window[i];
                    for (var j = 0; j < _target.Length; j++)
                    {
                        var cost = topCost;
                        topCost = _costs[j];
                        if (c != _target[j])
                        {
                            if (previousCost < cost)
                                cost = previousCost;
                            if (topCost < cost)
                                cost = topCost;
                            ++cost;
                        }

                        _costs[j] = cost;
                        previousCost = cost;
                    }
                }
                if (_costs[_costs.Length - 1] >= best) continue;
                best = _costs[_costs.Length - 1];
                if (best == 0)
                    break;
            }
            return best + delta / 2;
        }

        public static int Distance(string s1, string s2)
        {
            if (s1 == null || s2 == null)
                return int.MaxValue;
            return s1.Length <= s2.Length ? new SlidingLevenshtein(s1).Distance(s2) : new SlidingLevenshtein(s2).Distance(s1);
        }
    }
}