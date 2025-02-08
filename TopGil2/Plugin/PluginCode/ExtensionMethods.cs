using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopGil;

internal static class ExtensionMethods
{
    public static bool EqualsIgnoreCase(this string s, string other)
    {
        return s.Equals(other, StringComparison.OrdinalIgnoreCase);
    }

    public static bool EqualsIgnoreCaseAny(this string obj, params string[] values)
    {
        return values.Any(x => x.Equals(obj, StringComparison.OrdinalIgnoreCase));
    }
}
