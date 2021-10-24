using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class StringExtensions
    {
        public static long ParseFormattedNumber(this string str)
        {
            var match = Regex.Match(str, @"(\d+\.\d+|\d+)(\w|)$");

            var exp = match.Groups.Count == 3 ? match.Groups[2].Value : "";

            var baseDigits = double.Parse(match.Groups[1].Value);

            switch (exp.ToLower())
            {
                case "k":
                    baseDigits *= 1000;
                    break;
                case "m":
                    baseDigits *= 1000000;
                    break;
                case "g":
                    baseDigits *= 1000000000;
                    break;
            }
            return (long)Math.Ceiling(baseDigits);
        }
    }
}
