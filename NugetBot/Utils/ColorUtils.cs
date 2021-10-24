using Discord;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class ColorUtils
    {
        public static System.Drawing.Color GetNameColor(string name)
        {
            var hash = (name ?? "").GetHashCode();

            var rnd = new Random(hash);

            unchecked
            {
                return System.Drawing.Color.FromArgb(rnd.Next(7) switch
                {
                    0 => (int)0xff55acee,
                    1 => (int)0xffc1694f,
                    2 => (int)0xff78b059,
                    3 => (int)0xfff4900c,
                    4 => (int)0xffab8ed8,
                    5 => (int)0xffdd2e44,
                    6 => (int)0xfffdcb58,
                    _ => 0
                });
            }
        }

        public static Emoji GetColorEmoji(System.Drawing.Color color)
        {
            unchecked
            {
                return color.ToArgb() switch
                {
                    (int)0xff55acee => ":blue_square:",
                    (int)0xffc1694f => ":brown_square:",
                    (int)0xff78b059 => ":green_square:",
                    (int)0xfff4900c => ":orange_square:",
                    (int)0xffab8ed8 => ":purple_square:",
                    (int)0xffdd2e44 => ":red_square:",
                    (int)0xfffdcb58 => ":yellow_square:",
                    _ => throw new ArgumentException()
                };
            }
        }
    }
}
