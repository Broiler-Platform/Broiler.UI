using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Documents.Model;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>Resolves shared edges in segments, including seams beside spanning cells.</summary>
internal static class RichEditTableBorders
{
    internal readonly record struct Edge(bool Vertical, double Axis, double Start, double End, TableBorder Border);

    internal static IEnumerable<Edge> Resolve(IEnumerable<Edge> edges)
    {
        foreach (var group in edges.Where(edge => edge.Border.IsVisible)
                     .GroupBy(edge => (edge.Vertical, Axis: Math.Round(edge.Axis, 4))))
        {
            var candidates = group.ToArray();
            var stops = candidates.SelectMany(edge => new[] { edge.Start, edge.End }).Distinct().Order().ToArray();
            for (int index = 1; index < stops.Length; index++)
            {
                double start = stops[index - 1], end = stops[index];
                TableBorder winner = TableBorder.None;
                foreach (var candidate in candidates)
                    if (candidate.Start <= start && candidate.End >= end)
                        winner = TableBorder.Arbitrate(winner, candidate.Border);
                if (winner.IsVisible)
                    yield return new Edge(group.Key.Vertical, group.Key.Axis, start, end, winner);
            }
        }
    }

    internal static IEnumerable<Edge> Edges(BRect box, CellBorders borders)
    {
        yield return new(false, box.Top, box.Left, box.Right, borders.Top);
        yield return new(false, box.Bottom, box.Left, box.Right, borders.Bottom);
        yield return new(true, box.Left, box.Top, box.Bottom, borders.Left);
        yield return new(true, box.Right, box.Top, box.Bottom, borders.Right);
    }

    internal static void Draw(BRenderList list, Edge edge, double scale)
    {
        if (!edge.Border.IsVisible)
            return;
        double width = edge.Border.Width * scale;
        double length = edge.End - edge.Start;
        switch (edge.Border.Style)
        {
            case TableBorderStyle.Dashed:
            case TableBorderStyle.Dotted:
                double dash = edge.Border.Style == TableBorderStyle.Dotted ? width : width * 3;
                for (double at = 0; at < length; at += dash * 2)
                    Strip(at, Math.Min(dash, length - at), -width / 2, width);
                break;
            case TableBorderStyle.Double:
                Strip(0, length, -width / 2, width / 3);
                Strip(0, length, width / 6, width / 3);
                break;
            case TableBorderStyle.Groove:
            case TableBorderStyle.Ridge:
            case TableBorderStyle.Inset:
            case TableBorderStyle.Outset:
                // Two tones retain the relief distinction at every zoom level.
                bool reverse = edge.Border.Style is TableBorderStyle.Ridge or TableBorderStyle.Outset;
                Strip(0, length, -width / 2, width / 2, reverse ? 1.35 : .65);
                Strip(0, length, 0, width / 2, reverse ? .65 : 1.35);
                break;
            default:
                Strip(0, length, -width / 2, width);
                break;
        }

        void Strip(double at, double extent, double offset, double thickness, double tone = 1)
        {
            var color = edge.Border.Color;
            if (tone != 1)
                color = new Broiler.Graphics.Color.BColor(
                    (byte)Math.Clamp(color.R * tone, 0, 255),
                    (byte)Math.Clamp(color.G * tone, 0, 255),
                    (byte)Math.Clamp(color.B * tone, 0, 255));
            list.FillRect(edge.Vertical
                ? new BRect(edge.Axis + offset, edge.Start + at, thickness, extent)
                : new BRect(edge.Start + at, edge.Axis + offset, extent, thickness), color);
        }
    }
}
