using System.Collections.Generic;
using System.Linq;

namespace OctoMerge
{
    static class Extensions
    {
        public static IEnumerable<Variable> MergeWithOverwrite(this IEnumerable<Variable> @base, IEnumerable<Variable> changes) =>
             @base.Where(x => !changes.Any(y => y.Key == x.Key)).Union(changes);

        public static bool GetGlobalOrFalse(this IDictionary<string, Variable> dictionary, string key) =>
            dictionary.TryGetValue(key, out var ret) ? ret.Global : false;
    }
}
