using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassFactory.BillTracker.Domain.Services;

public sealed class PrintColumnWidthInput
{
    public required string Key { get; init; }
    public required double TitleWidth { get; init; }
    public required double MinWidth { get; init; }
    public required double MaxWidth { get; init; }
    public required bool IsNote { get; init; }
}

public static class PrintColumnWidthCalculator
{
    private const string ModelKey = "Model";

    public static IReadOnlyDictionary<string, double> Compute(
        IReadOnlyList<PrintColumnWidthInput> columns,
        double modelMaxDataWidth,
        double printableWidthDip,
        double horizontalMarginsDip,
        double gapDip,
        double paddingDip)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.Ordinal);
        }

        var noteColumn = columns.First(x => x.IsNote);
        var nonNoteColumns = columns.Where(x => !x.IsNote).ToList();
        var titleOnlyColumns = nonNoteColumns.Where(x => !string.Equals(x.Key, ModelKey, StringComparison.Ordinal)).ToList();

        var contentWidth = Math.Max(80d, printableWidthDip - horizontalMarginsDip - (gapDip * Math.Max(0, columns.Count - 1)));
        var widths = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var column in nonNoteColumns)
        {
            var measured = string.Equals(column.Key, ModelKey, StringComparison.Ordinal)
                ? Math.Max(column.TitleWidth, modelMaxDataWidth)
                : column.TitleWidth;

            widths[column.Key] = Math.Clamp(measured + paddingDip, column.MinWidth, column.MaxWidth);
        }

        var nonNoteWidth = nonNoteColumns.Sum(x => widths[x.Key]);
        var noteWidth = contentWidth - nonNoteWidth;

        if (noteWidth < noteColumn.MinWidth)
        {
            var effectivePadding = paddingDip;

            // Reduce shared cell padding before shrinking any title-only columns.
            var paddingDeficit = noteColumn.MinWidth - noteWidth;
            if (paddingDeficit > 0.1d && effectivePadding > 0d)
            {
                var paddingReduction = Math.Min(effectivePadding, paddingDeficit);
                effectivePadding -= paddingReduction;

                foreach (var column in nonNoteColumns)
                {
                    var measured = string.Equals(column.Key, ModelKey, StringComparison.Ordinal)
                        ? Math.Max(column.TitleWidth, modelMaxDataWidth)
                        : column.TitleWidth;
                    widths[column.Key] = Math.Clamp(measured + effectivePadding, column.MinWidth, column.MaxWidth);
                }

                nonNoteWidth = nonNoteColumns.Sum(x => widths[x.Key]);
                noteWidth = contentWidth - nonNoteWidth;
            }

            // If note is still too small, proportionally shrink title-only columns down to MinWidth.
            if (noteWidth < noteColumn.MinWidth)
            {
                var deficit = noteColumn.MinWidth - noteWidth;
                while (deficit > 0.1d)
                {
                    var totalSpare = titleOnlyColumns.Sum(column => Math.Max(0d, widths[column.Key] - column.MinWidth));
                    if (totalSpare <= 0.1d)
                    {
                        break;
                    }

                    foreach (var column in titleOnlyColumns)
                    {
                        var spare = Math.Max(0d, widths[column.Key] - column.MinWidth);
                        if (spare <= 0d)
                        {
                            continue;
                        }

                        var delta = Math.Min(spare, deficit * (spare / totalSpare));
                        widths[column.Key] -= delta;
                        deficit -= delta;
                        if (deficit <= 0.1d)
                        {
                            break;
                        }
                    }
                }

                nonNoteWidth = nonNoteColumns.Sum(x => widths[x.Key]);
                noteWidth = contentWidth - nonNoteWidth;
            }
        }

        widths[noteColumn.Key] = noteWidth >= noteColumn.MinWidth
            ? noteWidth
            : Math.Max(8d, noteWidth);

        var totalWidth = nonNoteColumns.Sum(x => widths[x.Key]) + widths[noteColumn.Key];
        if (totalWidth > contentWidth + 0.1d)
        {
            var overflow = totalWidth - contentWidth;
            widths[noteColumn.Key] = Math.Max(8d, widths[noteColumn.Key] - overflow);
        }

        return widths;
    }
}