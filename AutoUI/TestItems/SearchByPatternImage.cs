﻿using AutoUI.TestItems.Editors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AutoUI.TestItems
{
    [TestItemEditor(Editor = typeof(SearchByPatternEditor))]
    [XmlParse(XmlKey = "searchPattern")]
    public class SearchByPatternImage : AutoTestItem
    {
        public PatternMatchingImage Pattern;

        public bool ClickOnSucceseed { get; set; }

        public string PatternName { get => Pattern.Name; }

        public override void ParseXml(TestSet parent, XElement item)
        {
            if (item.Attribute("clickOnSucceseed") != null)
                ClickOnSucceseed = bool.Parse(item.Attribute("clickOnSucceseed").Value);
            if (item.Attribute("preCheck") != null)
                PreCheckCurrentPosition = bool.Parse(item.Attribute("preCheck").Value);

            var pId = int.Parse(item.Attribute("patternId").Value);
            var p = parent.Pool.Patterns.First(z => z.Id == pId);
            Pattern = p;
            base.ParseXml(parent, item);
        }
        public bool PreCheckCurrentPosition { get; set; } = false;

        public bool NextSearch { get; set; }
        public override TestItemProcessResultEnum Process(AutoTestRunContext ctx)
        {
            var screen = GetScreenshot();

            if (NextSearch)
            {
                int startX = 0;
                int startY = 0;
                if (ctx.LastSearchPosition != null)
                {
                    startX = ctx.LastSearchPosition.Value.X;
                    startY = ctx.LastSearchPosition.Value.Y + 1;
                }
                Point? ret = null;
                foreach (var item in Pattern.Items)
                {
                    int? maxW = null;
                    int? maxH = null;
                    if (PreCheckCurrentPosition)
                    {
                        maxW = item.Bitmap.Width * 2 + 1;
                        maxH = item.Bitmap.Height * 2 + 1;
                        ret = SearchPattern(screen, item.Bitmap, Cursor.Position.X - item.Bitmap.Width, Cursor.Position.Y - item.Bitmap.Height, maxW, maxH);
                    }
                    if (ret == null)
                        ret = SearchPattern(screen, item.Bitmap, startX, startY);
                    if (ret != null)
                    {
                        SetCursorPos(ret.Value.X + item.Bitmap.Width / 2, ret.Value.Y + item.Bitmap.Height / 2);
                        break;
                    }
                }
                //var ret = searchPattern(startX, startY);
                ctx.LastSearchPosition = ret;
            }
            else
            {
                Point? ret = null;
                foreach (var item in Pattern.Items)
                {
                    int? maxW = null;
                    int? maxH = null;
                    if (PreCheckCurrentPosition)
                    {
                        maxW = item.Bitmap.Width * 2 + 1;
                        maxH = item.Bitmap.Height * 2 + 1;
                        ret = SearchPattern(screen, item.Bitmap, Cursor.Position.X - item.Bitmap.Width, Cursor.Position.Y - item.Bitmap.Height, maxW, maxH);
                    }
                    if (ret == null)
                        ret = SearchPattern(screen, item.Bitmap);
                    if (ret != null)
                    {
                        SetCursorPos(ret.Value.X + item.Bitmap.Width / 2, ret.Value.Y + item.Bitmap.Height / 2);
                        break;
                    }
                }
                if (ret == null)
                {
                    return TestItemProcessResultEnum.Failed;
                }
                ctx.LastSearchPosition = ret;
            }

            screen.Dispose();
            if (ClickOnSucceseed)
            {
                var cc = new ClickAutoTestItem();
                cc.Process(ctx);
            }
            return TestItemProcessResultEnum.Success;
        }

        public static Bitmap GetScreenshot()
        {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }

            return bitmap;
        }

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
        public static bool IsPixelsEqual(Color px, Color px2)
        {
            return px.R == px2.R && px.G == px2.G && px.B == px2.B;
        }

        public static Point? SearchPattern(Bitmap screen, Bitmap pattern, int startX = 0, int startY = 0, int? maxWidth = null, int? maxHeight = null)
        {
            DirectBitmap d = new DirectBitmap(screen);
            DirectBitmap d2 = new DirectBitmap(pattern);

            Stopwatch sw = Stopwatch.StartNew();

            Random r = new Random();
            List<Point> points = new List<Point>();
            List<Color> clrs = new List<Color>();
            for (int t = 0; t < 10; t++)
            {
                var rx = r.Next(pattern.Width);
                var ry = r.Next(pattern.Height);
                points.Add(new Point(rx, ry));
                clrs.Add(d2.GetPixel(rx, ry));
            }

            //slide window
            var www = d.Width - pattern.Width;
            var hhh = d.Height - pattern.Height;
            if (maxWidth != null)
                www = Math.Min(maxWidth.Value, www);

            if (maxHeight != null)
                hhh = Math.Min(maxHeight.Value, hhh);

            for (int i = startX; i < www; i++)
            {
                for (int j = startY; j < hhh; j++)
                {
                    bool good = true;
                    //pre check random pixels
                    for (int t = 0; t < points.Count; t++)
                    {
                        var rx = points[t].X;
                        var ry = points[t].Y;
                        var px = d.GetPixel(i + rx, j + ry);
                        if (!IsPixelsEqual(px, clrs[t])) { good = false; break; }
                    }

                    if (!good) continue;
                    //check pattern match

                    for (int i1 = 0; i1 < pattern.Width; i1++)
                    {
                        for (int j1 = 0; j1 < pattern.Height; j1++)
                        {
                            var px = d.GetPixel(i + i1, j + j1);
                            var px2 = d2.GetPixel(i1, j1);
                            if (!IsPixelsEqual(px, px2)) { good = false; break; }
                        }
                        if (!good) break;
                    }

                    if (good)
                    {
                        d.Dispose();
                        d2.Dispose();

                        sw.Stop();

                        return new Point(i, j);
                    }
                }
            }
            d.Dispose();
            d2.Dispose();

            return null;
        }

        internal override string ToXml()
        {
            MemoryStream ms = new MemoryStream();
            //Pattern.Save(ms, ImageFormat.Png);
            //var b64 = Convert.ToBase64String(ms.ToArray());
            return $"<searchPattern patternId=\"{Pattern.Id}\" preCheck=\"{PreCheckCurrentPosition}\" clickOnSucceseed=\"{ClickOnSucceseed}\" ></searchPattern>";
        }

        public bool Assert { get; set; }
    }
}