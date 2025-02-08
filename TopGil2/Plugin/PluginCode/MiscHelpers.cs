using System;

namespace TopGil;

internal static class MiscHelpers
{
	internal static string FormatNumber(uint? number)
	{
		if (number == null)
		{
			return "0";
		}

		return number.Value.ToString("N0");
	}

    internal static string FormatNumber(long? number)
    {
        if (number == null)
        {
            return "0";
        }

        return number.Value.ToString("N0");
    }

    internal static string FormatDateTime(DateTime dateTime)
	{
		return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
	}
}
