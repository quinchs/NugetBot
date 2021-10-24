using NugetBot.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;

namespace NugetBot
{
    public partial class ImageGenerator
    {
        private static Color White = Color.White;
        private static Color Background = Color.FromArgb(44, 47, 51);
        private static Color Primary = Color.FromArgb(unchecked((int)0xff004880));

        private static Color[] Colors = new Color[]
        {
            Color.FromArgb(unchecked((int)0xff55acee)),
            Color.FromArgb(unchecked((int)0xffc1694f)),
            Color.FromArgb(unchecked((int)0xff78b059)),
            Color.FromArgb(unchecked((int)0xfff4900c)),
            Color.FromArgb(unchecked((int)0xffab8ed8)),
            Color.FromArgb(unchecked((int)0xffdd2e44)),
            Color.FromArgb(unchecked((int)0xfffdcb58)),
        };

        private static Pen WhitePen => new Pen(White, 3);
        private static Pen GreyPenThin => new Pen(Color.Gray, 1);
        private static Pen DashedPrimaryPen
        {
            get
            {
                var pen = new Pen(Primary, 5);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                pen.DashCap = System.Drawing.Drawing2D.DashCap.Round;
                return pen;
            }
        }

        private static Font SmallFont = new Font("Arial", 10, FontStyle.Regular);
        private static Font LargeFont = new Font("Arial", 25, FontStyle.Regular);
        private static Font MediumFont = new Font("Arial", 20, FontStyle.Regular);

        private static Pen PrimaryPen
            => new Pen(Primary, 5);

        public static MultiGraphResult CreateGraph(IEnumerable<NugetPackageTracker> packages, DateTime? from = null, DateTime? to = null)
        {
            var toDate = to ?? DateTime.UtcNow;
            var fromDate = from ?? packages.Min(x => x.Stats.Min(x => x.DateReleased));
            var dataset = packages.Select(x => (x, x.Stats.Where(x => x.DateReleased >= fromDate && x.DateReleased <= toDate).OrderBy(y => y.DateReleased.Ticks).ToArray())).ToArray();

            Dictionary<string, long> downloadsAtFrom = new();

            foreach (var item in dataset)
            {
                downloadsAtFrom.Add(item.x.PackageId, 0);
                foreach (var stat in item.x.Stats.OrderBy(x => x.DateReleased))
                {
                    if (stat.DateReleased >= fromDate)
                        break;

                    downloadsAtFrom[item.x.PackageId] += stat.Downloads;
                }
            }

            var bitmap = new Bitmap(1920, 1080);
            var graphics = Graphics.FromImage(bitmap);

            graphics.Clear(Color.FromArgb(0x2C, 0x2F, 0x33));
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float paddingLeft = 100;
            float paddingTop = 70;
            float paddingBottom = 100;
            float paddingRight = 35;

            var yMax = dataset.Max(x => x.x.Stats.Sum(y => y.Downloads));
            yMax += (long)Math.Ceiling(yMax * 0.1);
            var yMin = downloadsAtFrom.Min(x => x.Value);

            double xOffset = (bitmap.Width - paddingLeft - paddingRight) / (toDate - fromDate).TotalSeconds;
            double yOffset = (bitmap.Height - paddingTop - paddingBottom) / (yMax - yMin);

            graphics.DrawLine(WhitePen, (int)paddingLeft, bitmap.Height - (int)paddingBottom, (int)bitmap.Width - (int)paddingRight, bitmap.Height - (int)paddingBottom);
            graphics.DrawLine(WhitePen, bitmap.Width - paddingRight, (int)paddingTop, bitmap.Width - paddingRight, (int)bitmap.Height - (int)paddingBottom);

            var yLineSpacing = (bitmap.Height - paddingBottom - paddingTop) / 6;
            var xLineSpacing = (bitmap.Width - paddingLeft - paddingRight) / 6;

            for (int i = 0; i != 6; i++)
            {
                var x1 = bitmap.Width - paddingRight;
                var x2 = paddingLeft - 15;
                var y = paddingTop + yLineSpacing + (yLineSpacing * i);

                graphics.DrawLine(GreyPenThin, x1, y, x2, y);

                var downloads = ((yMax - yMin) - ((yLineSpacing + yLineSpacing * i)) / yOffset) + yMin;

                graphics.DrawString($"{(int)downloads}", SmallFont, new SolidBrush(Color.White), x2 - 5, y, new StringFormat()
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                });
            }

            for (int i = 0; i != 7; i++)
            {
                var x = paddingLeft + (xLineSpacing * i);
                var y1 = paddingTop + yLineSpacing / 2;
                var y2 = bitmap.Height - paddingBottom + 15;

                graphics.DrawLine(GreyPenThin, x, y1, x, y2);

                var seconds = ((xLineSpacing * i)) / xOffset;

                var date = fromDate.AddSeconds(seconds);

                if (i == 0)
                    x -= 15;

                if (i == 6)
                    x += 15;

                graphics.DrawString($"{date:M} {date.Year}", SmallFont, new SolidBrush(Color.White), x, y2 + 20, new StringFormat()
                {
                    Alignment = i == 6 ? StringAlignment.Far : i == 0 ? StringAlignment.Near : StringAlignment.Center,
                    LineAlignment = StringAlignment.Far
                });
            }

            int clr = 0;

            // List<(List<PointF> Points, Color Color)> totalPoints = new();

            Dictionary<string, Color> colors = new Dictionary<string, Color>();

            foreach(var package in dataset)
            {
                //List<PointF> points = new();

                var color = Colors[clr];

                colors.Add(package.x.PackageId, color);

                clr++;
                var dashedPen = new Pen(color, 5);
                dashedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                dashedPen.DashCap = System.Drawing.Drawing2D.DashCap.Round;

                var data = package.Item2.ToArray();

                for (int i = 0; i != data.Length - 1; i++)
                {
                    var cur = data[i];
                    downloadsAtFrom[package.x.PackageId] += cur.Downloads;
                    var next = data[i + 1];

                    var x1 = (float)((cur.DateReleased - fromDate).TotalSeconds * xOffset) + paddingLeft;
                    var x2 = (float)((next.DateReleased - fromDate).TotalSeconds * xOffset) + paddingLeft;
                    var y1 = (float)((bitmap.Height - paddingBottom) - downloadsAtFrom[package.x.PackageId] * yOffset);
                    var y2 = (float)((bitmap.Height - paddingBottom) - (downloadsAtFrom[package.x.PackageId] + next.Downloads) * yOffset);

                    if (i == data.Length - 2)
                    {
                        x2 = bitmap.Width - paddingRight;
                    }
                    else
                        graphics.FillEllipse(new SolidBrush(color), x2 - 7, y2 - 7, 14, 14);


                    //points.Add(new PointF(x1, y1));
                    //points.Add(new PointF(x2, y2));

                    graphics.FillEllipse(new SolidBrush(color), x1 - 7, y1 - 7, 14, 14);

                    if (next.Inferred)
                    {
                        graphics.DrawLine(dashedPen, x1, y1, x2, y2);
                    }
                }

                //points.Add(new PointF(bitmap.Width - paddingRight, bitmap.Height - paddingBottom));
                //points.Add(new PointF(paddingLeft, bitmap.Height - paddingBottom));

                //graphics.FillPolygon(new SolidBrush(Color.FromArgb(74, color)), points.ToArray());

                //totalPoints.Add((points, color));
            }

            //var poly = GetPolygons(totalPoints);
            //foreach(var item in poly)
            //{
            //    graphics.FillPolygon(new SolidBrush(Color.FromArgb(74, item.Color)), item.Points.ToArray());
            //}

            bitmap.Save("test.png");

            return new MultiGraphResult(bitmap, graphics, colors);
        }

        private static (List<PointF> Points, Color Color)[] GetPolygons(IEnumerable<(List<PointF> Points, Color Color)> inputs)
        {
            int intersectSampleSize = 5;

            var highestY = inputs.Min(x => x.Points.Min(x => x.Y));
            var highestLine = inputs.First(x => x.Points.Any(y => y.Y == highestY));
            var highestPoint = highestLine.Points.FirstOrDefault(x => x.Y == highestY);

            var others = inputs.Where(x => x != highestLine);

            highestLine.Points.Reverse();

            List<PointF> final = new();

            foreach (var point in highestLine.Points.Skip(1))
            {
                // first point is the highest, get the closest point on the x axis thats not apart of our line

                PointF p = new PointF(0,10000);

                foreach(var other in others)
                {
                    foreach(var pt in other.Points)
                    {
                        if (pt.X < point.X && (point.X - p.X) > (point.X - pt.X) && pt.Y <= p.Y)
                            p = pt;
                    }
                }

                final.Add(p);
                //p.Min()
            }

            return new (List<PointF> Points, Color Color)[] { (final, highestLine.Color)  };
        }

        public static GraphResult CreateGraph(NugetPackageTracker tracker, DateTime? from = null, DateTime? to = null)
        {
            var toDate = to ?? DateTime.UtcNow;
            var fromDate = from ?? tracker.Stats.Min(x => x.DateReleased);
            var dataset = tracker.Stats.Where(x => x.DateReleased >= fromDate && x.DateReleased <= toDate);

            long downloadsAtFrom = 0;

            foreach(var item in tracker.Stats.OrderBy(x => x.DateReleased))
            {
                if (item.DateReleased >= fromDate)
                    break;

                downloadsAtFrom += item.Downloads;
            }

            var bitmap = new Bitmap(1920, 1080);
            var graphics = Graphics.FromImage(bitmap);

            graphics.Clear(Color.FromArgb(0x2C, 0x2F, 0x33));

            float paddingLeft = 100;
            float paddingTop = 70;
            float paddingBottom = 100;
            float paddingRight = 35;

            var yMax = dataset.Sum(x => x.Downloads);
            yMax += (long)Math.Ceiling(yMax * 0.1);
            var yMin = downloadsAtFrom;

            double xOffset = (bitmap.Width - paddingLeft - paddingRight) / (toDate - fromDate).TotalSeconds;
            double yOffset = (bitmap.Height - paddingTop - paddingBottom) / (yMax - yMin);

            graphics.DrawLine(WhitePen, (int)paddingLeft, bitmap.Height - (int)paddingBottom, (int)bitmap.Width - (int)paddingRight, bitmap.Height - (int)paddingBottom);
            graphics.DrawLine(WhitePen, bitmap.Width - paddingRight, (int)paddingTop, bitmap.Width - paddingRight, (int)bitmap.Height - (int)paddingBottom);

            graphics.DrawLine(PrimaryPen, paddingLeft - 10, 40, paddingLeft + 40, 40);
            graphics.DrawLine(DashedPrimaryPen, paddingLeft - 10, 60, paddingLeft + 40, 60);

            graphics.DrawString("Tracked", SmallFont, new SolidBrush(Color.White), paddingLeft + 50, 40, new StringFormat()
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            });

            graphics.DrawString("Inferred", SmallFont, new SolidBrush(Color.White), paddingLeft + 50, 60, new StringFormat()
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            });

            graphics.DrawString($"{tracker.PackageName} by {tracker.Author}", LargeFont, new SolidBrush(Color.White), bitmap.Width / 2, 10, new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near
            });

            graphics.DrawString($"Downloads from {fromDate:D} to {toDate:D}", MediumFont, new SolidBrush(Color.White), bitmap.Width / 2, 60, new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near
            });

            var data = dataset.OrderBy(x => x.DateReleased.Ticks).ToArray();

            var yLineSpacing = (bitmap.Height - paddingBottom - paddingTop) / 6;
            var xLineSpacing = (bitmap.Width - paddingLeft - paddingRight) / 6;

            for (int i = 0; i != 6; i++)
            {
                var x1 = bitmap.Width - paddingRight;
                var x2 = paddingLeft - 15;
                var y = paddingTop + yLineSpacing + (yLineSpacing * i);

                graphics.DrawLine(GreyPenThin, x1, y, x2, y);

                var downloads = ((yMax - yMin) - ((yLineSpacing + yLineSpacing * i)) / yOffset) + yMin;

                graphics.DrawString($"{(int)downloads}", SmallFont, new SolidBrush(Color.White), x2 - 5, y, new StringFormat()
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                });
            }

            for (int i = 0; i != 7; i++)
            {
                var x = paddingLeft + (xLineSpacing * i);
                var y1 = paddingTop + yLineSpacing / 2;
                var y2 = bitmap.Height - paddingBottom + 15;

                graphics.DrawLine(GreyPenThin, x, y1, x, y2);

                var seconds = ((xLineSpacing * i)) / xOffset;

                var date = fromDate.AddSeconds(seconds);

                if (i == 0)
                    x -= 15;

                if (i == 6)
                    x += 15;

                graphics.DrawString($"{date:M} {date.Year}", SmallFont, new SolidBrush(Color.White), x, y2 + 20, new StringFormat() 
                {
                    Alignment = i == 6 ? StringAlignment.Far : i == 0 ? StringAlignment.Near : StringAlignment.Center,
                    LineAlignment = StringAlignment.Far
                });
            }

            List<PointF> points = new();

            for (int i = 0; i != data.Length - 1; i++)
            {
                var cur = data[i];
                downloadsAtFrom += cur.Downloads;
                var next = data[i + 1];

                var x1 = (float)((cur.DateReleased - fromDate).TotalSeconds * xOffset) + paddingLeft;
                var x2 = (float)((next.DateReleased - fromDate).TotalSeconds * xOffset) + paddingLeft;
                var y1 = (float)((bitmap.Height - paddingBottom) - downloadsAtFrom * yOffset);
                var y2 = (float)((bitmap.Height - paddingBottom) - (downloadsAtFrom + next.Downloads) * yOffset);

                if (i == data.Length - 2)
                {
                    x2 = bitmap.Width - paddingRight;
                }
                else
                    graphics.FillEllipse(new SolidBrush(Primary), x2 - 7, y2 - 7, 14, 14);


                points.Add(new PointF(x1, y1));
                points.Add(new PointF(x2, y2));

                graphics.FillEllipse(new SolidBrush(Primary), x1 - 7, y1 - 7, 14, 14);

                if (next.Inferred) 
                {
                    graphics.DrawLine(DashedPrimaryPen, x1, y1, x2, y2);
                }

                
            }

            points.Add(new PointF(bitmap.Width - paddingRight, bitmap.Height - paddingBottom));
            points.Add(new PointF(paddingLeft, bitmap.Height - paddingBottom));

            graphics.FillPolygon(new SolidBrush(Color.FromArgb(74, Styles.Primary)), points.ToArray());

            bitmap.Save("test.png");

            return new GraphResult(bitmap, graphics);
        }

        public struct MultiGraphResult
        {
            public Dictionary<string, Color> Colors { get; set; }
            public Bitmap Image { get; set; }
            public Graphics Graphics { get; set; }

            public MultiGraphResult(Bitmap bm, Graphics g, Dictionary<string, Color> colors)
            {
                Image = bm;
                Graphics = g;
                Colors = colors;
            }
        }

        public struct GraphResult
        {
            public Bitmap Image { get; set; }
            public Graphics Graphics { get; set; }

            public GraphResult(Bitmap bm, Graphics g)
            {
                Image = bm;
                Graphics = g;
            }
        }
    }
}
