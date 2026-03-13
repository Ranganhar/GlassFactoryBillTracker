using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Controls;
using System.Globalization;
using System.Diagnostics;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.App.Services;

public sealed class PrintService : IPrintService
{
    private const int NoteColumnIndex = 8;
    private static readonly FontFamily PrintFontFamily = new("Microsoft YaHei");
    private static readonly double MeasurementPixelsPerDip = GetPixelsPerDip();

    private const double DotMatrixPageWidthMm = 216d;
    private const double DotMatrixPageHeightMm = 93d;
    private const double DotMatrixMarginMm = 5d;
    private const double DotMatrixTableHeaderMm = 7d;
    private const double DotMatrixBaseRowMm = 6.5d;
    private const double DotMatrixHorizontalPaddingDip = 3d;
    private const double DotMatrixVerticalPaddingDip = 2d;
    private const double DotMatrixInnerPaddingDip = 6d;
    private const double DotMatrixCellBorderDip = 0.5d;
    private const double DotMatrixOuterBorderDip = 0.8d;
    private const double DotMatrixHeaderGapDip = 1.5d;
    private const double DotMatrixMinNoteWidthDip = 40d;

    private sealed class DotMatrixRowLayout
    {
        public required string[] Values { get; init; }
        public required double Height { get; init; }
    }

    private sealed class DotMatrixPageLayout
    {
        public required IReadOnlyList<DotMatrixRowLayout> Rows { get; init; }
        public required bool IncludeFooter { get; init; }
    }

    private sealed class PrintColumnDefinition
    {
        public required string Key { get; init; }
        public required string Title { get; init; }
        public required double MinWidth { get; init; }
        public required double MaxWidth { get; init; }
        public required bool IsNote { get; init; }
    }

    public FixedDocument RenderDotMatrixTriplicate(IReadOnlyList<OrderExportDto> orders, PrintBillOptions options)
    {
        options ??= new PrintBillOptions();
        var document = new FixedDocument();
        if (orders is null || orders.Count == 0)
        {
            return document;
        }

        var pageWidth = MmToDip(DotMatrixPageWidthMm);
        var pageHeight = MmToDip(DotMatrixPageHeightMm);
        var margin = MmToDip(DotMatrixMarginMm);
        var tableHeaderHeight = MmToDip(DotMatrixTableHeaderMm);
        var baseFontSize = Math.Max(8d, options.FontSize);
        var tableFontSize = Math.Max(8d, baseFontSize - 1d);
        var baseRowHeight = MmToDip(DotMatrixBaseRowMm);

        var contentWidth = pageWidth - (margin * 2d);
        var contentHeight = pageHeight - (margin * 2d);
        var tableWidth = Math.Max(80d, contentWidth - (DotMatrixInnerPaddingDip * 2d));
        foreach (var order in orders)
        {
            var columns = CreatePrintColumns();
            var headers = columns.Select(x => x.Title).ToArray();
            var sourceRows = (order.Items ?? Array.Empty<OrderExportItemDto>())
                .Where(item => item is not null)
                .Select(item => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Model"] = item.Model ?? string.Empty,
                    ["Length"] = FormatInt(item.GlassLengthMm),
                    ["Width"] = FormatInt(item.GlassWidthMm),
                    ["Quantity"] = item.Quantity.ToString(CultureInfo.InvariantCulture),
                    ["UnitPrice"] = FormatInt(item.GlassUnitPricePerM2),
                    ["HoleFee"] = FormatInt(item.HoleFee),
                    ["OtherFee"] = FormatInt(item.OtherFee),
                    ["Amount"] = FormatMoney2(item.Amount),
                    ["Note"] = item.Note ?? string.Empty
                })
                .ToList();

            var columnWidthMap = ComputeColumnWidthsForPrint(
                columns,
                sourceRows,
                new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                tableFontSize,
                MeasurementPixelsPerDip,
                pageWidth,
                (margin * 2d) + (DotMatrixInnerPaddingDip * 2d),
                0d,
                6d);
            var columnWidths = columns.Select(column => columnWidthMap[column.Key]).ToList();
            var noteColumnWidth = columnWidths[NoteColumnIndex];
            var noteTextWidth = Math.Max(8d, noteColumnWidth - (DotMatrixHorizontalPaddingDip * 2d));

            var headerHeight = MeasureHeaderBlockHeight(order, options, tableWidth);
            var pageRowsHeightLimit = Math.Max(
                baseRowHeight,
                contentHeight - (DotMatrixInnerPaddingDip * 2d) - headerHeight - DotMatrixHeaderGapDip - tableHeaderHeight);

            var typeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var maxLinesPerPhysicalRow = Math.Max(
                1,
                (int)Math.Floor((pageRowsHeightLimit - (DotMatrixVerticalPaddingDip * 2d)) / Math.Max(1d, MeasureSingleLineHeight(typeface, tableFontSize))));

            var rowLayouts = BuildRowLayouts(
                columns,
                sourceRows,
                typeface,
                tableFontSize,
                noteTextWidth,
                baseRowHeight,
                maxLinesPerPhysicalRow);

            var footerHeight = MeasureFooterRowHeight(baseFontSize);
            var pageLayouts = PaginateRows(rowLayouts, pageRowsHeightLimit, footerHeight, order.OrderNo);

            foreach (var page in pageLayouts)
            {
                var pageContent = new PageContent();
                var fixedPage = CreateDotMatrixPage(
                    order,
                    options,
                    pageWidth,
                    pageHeight,
                    margin,
                    headerHeight,
                    tableHeaderHeight,
                    headers,
                    columnWidths,
                    page.Rows,
                    page.IncludeFooter,
                    footerHeight,
                    noteColumnWidth);
                ((IAddChild)pageContent).AddChild(fixedPage);
                document.Pages.Add(pageContent);
            }
        }

        return document;
    }

    public FixedDocument RenderA4(IReadOnlyList<OrderExportDto> orders, PrintBillOptions options)
    {
        options ??= new PrintBillOptions();
        var document = new FixedDocument();
        if (orders is null || orders.Count == 0)
        {
            return document;
        }

        var pageWidth = MmToDip(210);
        var pageHeight = MmToDip(297);

        foreach (var order in orders)
        {
            var pageContent = new PageContent();
            var fixedPage = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            var copyPanel = CreateBillCopyPanel(order, options, pageWidth, pageHeight, isDotMatrix: false);
            fixedPage.Children.Add(copyPanel);

            ((IAddChild)pageContent).AddChild(fixedPage);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static IReadOnlyList<DotMatrixRowLayout> BuildRowLayouts(
        IReadOnlyList<PrintColumnDefinition> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string>> sourceRows,
        Typeface typeface,
        double tableFontSize,
        double noteTextWidth,
        double baseRowHeight,
        int maxLinesPerPhysicalRow)
    {
        var rows = new List<DotMatrixRowLayout>();

        foreach (var source in sourceRows)
        {
            var wrappedLines = WrapTextToLines(source[columns[NoteColumnIndex].Key], noteTextWidth, typeface, tableFontSize);
            if (wrappedLines.Count == 0)
            {
                wrappedLines.Add(string.Empty);
            }

            var offset = 0;
            var chunkIndex = 0;
            while (offset < wrappedLines.Count)
            {
                var linesInChunk = Math.Min(maxLinesPerPhysicalRow, wrappedLines.Count - offset);
                var noteChunk = string.Join("\n", wrappedLines.Skip(offset).Take(linesInChunk));
                var values = new string[columns.Count];
                if (chunkIndex == 0)
                {
                    for (var i = 0; i < columns.Count; i++)
                    {
                        values[i] = source.TryGetValue(columns[i].Key, out var value) ? value : string.Empty;
                    }
                }
                else
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = string.Empty;
                    }
                }

                values[NoteColumnIndex] = noteChunk;

                var noteHeight = MeasureWrappedTextHeight(noteChunk, noteTextWidth, typeface, tableFontSize);
                var rowHeight = Math.Max(baseRowHeight, noteHeight + (DotMatrixVerticalPaddingDip * 2d));
                rows.Add(new DotMatrixRowLayout { Values = values, Height = rowHeight });

                offset += linesInChunk;
                chunkIndex++;
            }
        }

        return rows;
    }

    private static IReadOnlyList<DotMatrixPageLayout> PaginateRows(
        IReadOnlyList<DotMatrixRowLayout> rows,
        double pageRowsHeightLimit,
        double footerHeight,
        string? orderNo)
    {
        var pages = new List<DotMatrixPageLayout>();
        var nextIndex = 0;
        var pageNo = 1;

        while (nextIndex < rows.Count)
        {
            var pageRows = new List<DotMatrixRowLayout>();
            var usedHeight = 0d;

            while (nextIndex < rows.Count)
            {
                var row = rows[nextIndex];
                var rowHeight = Math.Min(row.Height, pageRowsHeightLimit);
                var isLastRow = nextIndex == rows.Count - 1;
                var requiredHeight = rowHeight + (isLastRow ? footerHeight : 0d);
                if ((usedHeight + requiredHeight) <= (pageRowsHeightLimit + 0.1d))
                {
                    pageRows.Add(new DotMatrixRowLayout { Values = row.Values, Height = rowHeight });
                    usedHeight += rowHeight;
                    nextIndex++;
                    continue;
                }

                if (pageRows.Count == 0)
                {
                    pageRows.Add(new DotMatrixRowLayout { Values = row.Values, Height = rowHeight });
                    usedHeight += rowHeight;
                    nextIndex++;
                }

                break;
            }

            var isFinalPageCandidate = nextIndex >= rows.Count;
            var includeFooter = false;
            if (isFinalPageCandidate)
            {
                includeFooter = (usedHeight + footerHeight) <= (pageRowsHeightLimit + 0.1d);
            }

            pages.Add(new DotMatrixPageLayout
            {
                Rows = pageRows,
                IncludeFooter = includeFooter
            });

            TracePaginationDecision(orderNo, pageNo, rows.Count - nextIndex, pageRows.Count, includeFooter, usedHeight, pageRowsHeightLimit, footerHeight);
            pageNo++;

            if (isFinalPageCandidate && !includeFooter)
            {
                pages.Add(new DotMatrixPageLayout
                {
                    Rows = Array.Empty<DotMatrixRowLayout>(),
                    IncludeFooter = true
                });
                TracePaginationDecision(orderNo, pageNo, 0, 0, true, 0d, pageRowsHeightLimit, footerHeight);
                break;
            }
        }

        if (rows.Count == 0)
        {
            pages.Add(new DotMatrixPageLayout
            {
                Rows = Array.Empty<DotMatrixRowLayout>(),
                IncludeFooter = true
            });
        }

        return pages;
    }

    private static FixedPage CreateDotMatrixPage(
        OrderExportDto order,
        PrintBillOptions options,
        double pageWidth,
        double pageHeight,
        double margin,
        double measuredHeaderHeight,
        double tableHeaderHeight,
        IReadOnlyList<string> headers,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<DotMatrixRowLayout> pageRows,
        bool includeFooter,
        double footerHeight,
        double noteColumnWidth)
    {
        options ??= new PrintBillOptions();
        order ??= new OrderExportDto();
        var baseFontSize = Math.Max(8d, options.FontSize);
        var fixedPage = new FixedPage
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White
        };

        var contentBorder = new Border
        {
            Width = pageWidth - (margin * 2d),
            Height = pageHeight - (margin * 2d),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(DotMatrixOuterBorderDip),
            Padding = new Thickness(DotMatrixInnerPaddingDip),
            SnapsToDevicePixels = true
        };
        FixedPage.SetLeft(contentBorder, margin);
        FixedPage.SetTop(contentBorder, margin);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(measuredHeaderHeight + DotMatrixHeaderGapDip) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, DotMatrixHeaderGapDip)
        };

        var companyText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(options.HeaderText) ? "亿达夹丝玻璃" : options.HeaderText,
            FontWeight = FontWeights.Bold,
            FontSize = baseFontSize + 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
        headerPanel.Children.Add(companyText);

        var contactPhone = options.UseCustomerPhone ? (order.CustomerPhone ?? string.Empty) : (options.CustomPhone ?? string.Empty);
        var metaText = new TextBlock
        {
            Text = $"订单号: {order.OrderNo ?? string.Empty}    日期: {order.DateTime:yyyy-MM-dd HH:mm:ss}    客户: {order.CustomerName ?? string.Empty}    电话: {contactPhone}",
            FontSize = baseFontSize - 1,
            TextWrapping = TextWrapping.Wrap
        };
        headerPanel.Children.Add(metaText);

        Grid.SetRow(headerPanel, 0);
        root.Children.Add(headerPanel);

        var table = BuildPageTable(
            headers,
            columnWidths,
            pageRows,
            includeFooter ? $"合计：{FormatMoney2(order.TotalAmount)}" : null,
            baseFontSize,
            tableHeaderHeight,
            footerHeight,
            noteColumnWidth);
        Grid.SetRow(table, 1);
        root.Children.Add(table);

        contentBorder.Child = root;
        fixedPage.Children.Add(contentBorder);
        return fixedPage;
    }

    private static Border BuildPageTable(
        IReadOnlyList<string> headers,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<DotMatrixRowLayout> pageRows,
        string? totalText,
        double baseFontSize,
        double tableHeaderHeight,
        double footerHeight,
        double noteColumnWidth)
    {
        var table = new Grid
        {
            Width = columnWidths.Sum(),
            HorizontalAlignment = HorizontalAlignment.Left,
            SnapsToDevicePixels = true
        };

        foreach (var width in columnWidths)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        }

        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tableHeaderHeight) });
        for (var col = 0; col < headers.Count; col++)
        {
            var cell = CreateCell(headers[col], bold: true, baseFontSize, TextAlignment.Left, wrap: false, trim: true, maxTextWidth: null);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, col);
            table.Children.Add(cell);
        }

        var rowIndex = 1;
        foreach (var row in pageRows)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(row.Height) });
            var values = row.Values;
            for (var col = 0; col < values.Length; col++)
            {
                var isNote = col == NoteColumnIndex;
                var cell = CreateCell(
                    values[col],
                    bold: false,
                    baseFontSize,
                    TextAlignment.Left,
                    wrap: isNote,
                    trim: !isNote,
                    maxTextWidth: isNote ? Math.Max(8d, noteColumnWidth - (DotMatrixHorizontalPaddingDip * 2d)) : null);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        if (!string.IsNullOrWhiteSpace(totalText))
        {
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(footerHeight) });
            var totalCell = CreateCell(totalText, bold: true, baseFontSize, TextAlignment.Right, wrap: false, trim: false, maxTextWidth: null);
            Grid.SetRow(totalCell, rowIndex);
            Grid.SetColumn(totalCell, 0);
            Grid.SetColumnSpan(totalCell, headers.Count);
            table.Children.Add(totalCell);
        }

        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(DotMatrixOuterBorderDip),
            SnapsToDevicePixels = true,
            Child = table
        };
    }

    private static Border CreateBillCopyPanel(OrderExportDto order, PrintBillOptions options, double pageWidth, double pageHeight, bool isDotMatrix)
    {
        options ??= new PrintBillOptions();
        order ??= new OrderExportDto();
        var baseFontSize = Math.Max(8d, options.FontSize);
        var horizontalPadding = isDotMatrix ? 8d : 20d;
        var cellPadding = 6d;
        const double tableWidthEpsilon = 0.5d;

        var outerBorder = new Border
        {
            Width = pageWidth,
            Height = pageHeight,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.8),
            Padding = new Thickness(horizontalPadding, isDotMatrix ? 8d : 20d, horizontalPadding, isDotMatrix ? 8d : 20d),
            Margin = new Thickness(0)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(options.HeaderText) ? "亿达夹丝玻璃" : options.HeaderText,
            FontWeight = FontWeights.Bold,
            FontSize = isDotMatrix ? baseFontSize + 4 : baseFontSize + 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var contactPhone = options.UseCustomerPhone ? (order.CustomerPhone ?? string.Empty) : (options.CustomPhone ?? string.Empty);
        var meta = new TextBlock
        {
            Text = $"订单号: {order.OrderNo ?? string.Empty}    日期: {order.DateTime:yyyy-MM-dd HH:mm:ss}    客户: {order.CustomerName ?? string.Empty}    电话: {contactPhone}",
            FontSize = baseFontSize,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(meta, 1);
        root.Children.Add(meta);

        var rows = (order.Items ?? Array.Empty<OrderExportItemDto>()).Where(item => item is not null).ToList();
        var columns = CreatePrintColumns();
        var headers = columns.Select(x => x.Title).ToArray();
        var valuesByRow = rows.Select(item => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Model"] = item.Model ?? string.Empty,
            ["Length"] = FormatInt(item.GlassLengthMm),
            ["Width"] = FormatInt(item.GlassWidthMm),
            ["Quantity"] = item.Quantity.ToString(CultureInfo.InvariantCulture),
            ["UnitPrice"] = FormatInt(item.GlassUnitPricePerM2),
            ["HoleFee"] = FormatInt(item.HoleFee),
            ["OtherFee"] = FormatInt(item.OtherFee),
            ["Amount"] = FormatMoney2(item.Amount),
            ["Note"] = item.Note ?? string.Empty
        }).ToList();

        var table = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        var contentWidth = Math.Max(120d, pageWidth - (horizontalPadding * 2d) - tableWidthEpsilon);
        var tableFontSize = Math.Max(8d, baseFontSize - 1d);
        var columnWidthMap = ComputeColumnWidthsForPrint(
            columns,
            valuesByRow,
            new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            tableFontSize,
            MeasurementPixelsPerDip,
            pageWidth,
            horizontalPadding * 2d,
            0d,
            cellPadding);
        var columnWidths = columns.Select(column => columnWidthMap[column.Key]).ToList();
        TraceTableCoordinates(horizontalPadding, columnWidths, pageWidth, horizontalPadding);
        foreach (var width in columnWidths)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        }
        table.Width = Math.Min(contentWidth, columnWidths.Sum());

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = CreateCell(headers[col], bold: true, baseFontSize, TextAlignment.Left, wrap: false, trim: true, maxTextWidth: null);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, col);
            table.Children.Add(cell);
        }

        var rowIndex = 1;
        foreach (var values in valuesByRow)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var col = 0; col < columns.Count; col++)
            {
                var trim = col == 8;
                double? maxTextWidth = col == 8 ? Math.Max(8d, columnWidths[8] - (DotMatrixHorizontalPaddingDip * 2d)) : null;
                var cellValue = values.TryGetValue(columns[col].Key, out var value) ? value : string.Empty;
                var cell = CreateCell(cellValue, bold: false, baseFontSize, TextAlignment.Left, wrap: col == 8, trim: trim, maxTextWidth: maxTextWidth);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var totalCell = CreateCell($"合计：{FormatMoney2(order.TotalAmount)}", bold: true, baseFontSize, TextAlignment.Right, wrap: false, trim: false, maxTextWidth: null);
        Grid.SetRow(totalCell, rowIndex);
        Grid.SetColumn(totalCell, 0);
        Grid.SetColumnSpan(totalCell, headers.Length);
        table.Children.Add(totalCell);

        var tableContainer = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.8),
            SnapsToDevicePixels = true,
            Child = table
        };

        Grid.SetRow(tableContainer, 2);
        root.Children.Add(tableContainer);

        outerBorder.Child = root;
        return outerBorder;
    }

    private static Border CreateCell(string text, bool bold, double baseFontSize, TextAlignment alignment, bool wrap, bool trim, double? maxTextWidth)
    {
        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(DotMatrixCellBorderDip),
            Padding = new Thickness(DotMatrixHorizontalPaddingDip, DotMatrixVerticalPaddingDip, DotMatrixHorizontalPaddingDip, DotMatrixVerticalPaddingDip),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = PrintFontFamily,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontSize = Math.Max(8d, baseFontSize - 1d),
                TextAlignment = alignment,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                MaxWidth = maxTextWidth ?? double.PositiveInfinity
            }
        };
    }

    private static double MeasureFooterRowHeight(double baseFontSize)
    {
        var typeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var fontSize = Math.Max(8d, baseFontSize - 1d);
        var lineHeight = MeasureSingleLineHeight(typeface, fontSize);
        var baseRowHeight = MmToDip(DotMatrixBaseRowMm);
        return Math.Max(baseRowHeight, lineHeight + (DotMatrixVerticalPaddingDip * 2d));
    }

    private static double MeasureSingleLineHeight(Typeface typeface, double fontSize)
    {
        var formattedText = new FormattedText(
            "X",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            MeasurementPixelsPerDip);
        return formattedText.Height;
    }

    private static double MeasureWrappedTextHeight(string text, double maxTextWidth, Typeface typeface, double fontSize)
    {
        var formattedText = new FormattedText(
            string.IsNullOrEmpty(text) ? " " : text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            MeasurementPixelsPerDip)
        {
            MaxTextWidth = Math.Max(8d, maxTextWidth),
            Trimming = TextTrimming.None
        };
        return formattedText.Height;
    }

    private static List<string> WrapTextToLines(string? text, double maxTextWidth, Typeface typeface, double fontSize)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = string.Empty;
            foreach (var ch in paragraph)
            {
                var candidate = string.Concat(current, ch);
                if (MeasureTextWidth(candidate, typeface, fontSize, MeasurementPixelsPerDip) <= maxTextWidth || candidate.Length == 1)
                {
                    current = candidate;
                    continue;
                }

                lines.Add(current);
                current = ch.ToString();
            }

            lines.Add(current);
        }

        return lines;
    }

    private static Dictionary<string, double> ComputeColumnWidthsForPrint(
        IReadOnlyList<PrintColumnDefinition> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        Typeface typeface,
        double fontSize,
        double pixelsPerDip,
        double printableWidthDip,
        double horizontalMarginsDip,
        double gapDip,
        double paddingDip)
    {
        var contentWidth = Math.Max(80d, printableWidthDip - horizontalMarginsDip - (gapDip * Math.Max(0, columns.Count - 1)));
        var widths = new Dictionary<string, double>(StringComparer.Ordinal);
        var titleWidths = new Dictionary<string, double>(StringComparer.Ordinal);
        var maxDataWidths = new Dictionary<string, double>(StringComparer.Ordinal);

        const string modelKey = "Model";
        var noteColumn = columns.First(x => x.IsNote);
        var nonNoteColumns = columns.Where(x => !x.IsNote).ToList();
        var titleOnlyColumns = nonNoteColumns.Where(x => !string.Equals(x.Key, modelKey, StringComparison.Ordinal)).ToList();

        var modelMaxDataWidth = 0d;
        foreach (var row in rows)
        {
            row.TryGetValue(modelKey, out var modelValue);
            var valueWidth = MeasureTextWidth(modelValue, typeface, fontSize, pixelsPerDip);
            if (valueWidth > modelMaxDataWidth)
            {
                modelMaxDataWidth = valueWidth;
            }
        }

        foreach (var column in nonNoteColumns)
        {
            var titleWidth = MeasureTextWidth(column.Title, typeface, fontSize, pixelsPerDip);
            var maxDataWidth = string.Equals(column.Key, modelKey, StringComparison.Ordinal) ? modelMaxDataWidth : 0d;

            titleWidths[column.Key] = titleWidth;
            maxDataWidths[column.Key] = maxDataWidth;
            var measured = Math.Max(titleWidth, maxDataWidth);
            var withPadding = measured + paddingDip;
            widths[column.Key] = Math.Clamp(withPadding, column.MinWidth, column.MaxWidth);
        }

        var nonNoteWidth = nonNoteColumns.Sum(x => widths[x.Key]);
        var noteWidth = contentWidth - nonNoteWidth;

        if (noteWidth < noteColumn.MinWidth)
        {
            var effectivePadding = paddingDip;

            // First, reduce shared padding (but never below 0).
            var paddingDeficit = noteColumn.MinWidth - noteWidth;
            if (paddingDeficit > 0.1d && effectivePadding > 0d)
            {
                var paddingReduction = Math.Min(effectivePadding, paddingDeficit);
                effectivePadding -= paddingReduction;

                foreach (var column in nonNoteColumns)
                {
                    var measuredBase = Math.Max(titleWidths[column.Key], maxDataWidths[column.Key]);
                    widths[column.Key] = Math.Clamp(measuredBase + effectivePadding, column.MinWidth, column.MaxWidth);
                }

                nonNoteWidth = nonNoteColumns.Sum(x => widths[x.Key]);
                noteWidth = contentWidth - nonNoteWidth;
            }

            // If still insufficient, shrink title-only columns proportionally down to MinWidth.
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

        TraceColumnWidthDetails(columns, titleWidths, maxDataWidths, widths, contentWidth);

        return widths;
    }

    private static IReadOnlyList<PrintColumnDefinition> CreatePrintColumns()
    {
        return new[]
        {
            new PrintColumnDefinition { Key = "Model", Title = "型号", MinWidth = 48d, MaxWidth = 140d, IsNote = false },
            new PrintColumnDefinition { Key = "Length", Title = "长(mm)", MinWidth = 28d, MaxWidth = 86d, IsNote = false },
            new PrintColumnDefinition { Key = "Width", Title = "宽(mm)", MinWidth = 28d, MaxWidth = 86d, IsNote = false },
            new PrintColumnDefinition { Key = "Quantity", Title = "数量", MinWidth = 26d, MaxWidth = 72d, IsNote = false },
            new PrintColumnDefinition { Key = "UnitPrice", Title = "单价(元/㎡)", MinWidth = 40d, MaxWidth = 106d, IsNote = false },
            new PrintColumnDefinition { Key = "HoleFee", Title = "打孔费", MinWidth = 40d, MaxWidth = 106d, IsNote = false },
            new PrintColumnDefinition { Key = "OtherFee", Title = "其他费用", MinWidth = 40d, MaxWidth = 106d, IsNote = false },
            new PrintColumnDefinition { Key = "Amount", Title = "金额(元)", MinWidth = 44d, MaxWidth = 112d, IsNote = false },
            new PrintColumnDefinition { Key = "Note", Title = "备注", MinWidth = DotMatrixMinNoteWidthDip, MaxWidth = double.MaxValue, IsNote = true }
        };
    }

    private static double MeasureHeaderBlockHeight(OrderExportDto order, PrintBillOptions options, double maxWidth)
    {
        var baseFontSize = Math.Max(8d, options.FontSize);
        var companyTypeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var metaTypeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        var companyHeight = MeasureWrappedTextHeight(
            string.IsNullOrWhiteSpace(options.HeaderText) ? "亿达夹丝玻璃" : options.HeaderText,
            maxWidth,
            companyTypeface,
            baseFontSize + 2d);

        var contactPhone = options.UseCustomerPhone ? (order.CustomerPhone ?? string.Empty) : (options.CustomPhone ?? string.Empty);
        var metaText = $"订单号: {order.OrderNo ?? string.Empty}    日期: {order.DateTime:yyyy-MM-dd HH:mm:ss}    客户: {order.CustomerName ?? string.Empty}    电话: {contactPhone}";
        var metaHeight = MeasureWrappedTextHeight(metaText, maxWidth, metaTypeface, baseFontSize - 1d);

        return companyHeight + 2d + metaHeight;
    }

    private static double MeasureTextWidth(string? text, Typeface typeface, double fontSize, double pixelsPerDip)
    {
        var formattedText = new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            pixelsPerDip);
        return formattedText.WidthIncludingTrailingWhitespace;
    }

    private static double GetPixelsPerDip()
    {
        var visual = new DrawingVisual();
        return VisualTreeHelper.GetDpi(visual).PixelsPerDip;
    }

    private static string FormatInt(decimal value)
    {
        var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return rounded.ToString("F0", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney2(decimal value)
    {
        return OrderAmountCalculator.Round(value).ToString("F2", CultureInfo.InvariantCulture);
    }

    private static double MmToDip(double mm)
    {
        return mm * 96d / 25.4d;
    }

    [Conditional("DEBUG")]
    private static void TraceTableCoordinates(double tableLeft, IReadOnlyList<double> columnWidths, double pageWidth, double rightMargin)
    {
        var boundaries = new List<double>();
        var runningX = tableLeft;
        boundaries.Add(runningX);

        foreach (var width in columnWidths)
        {
            runningX += width;
            boundaries.Add(runningX);
        }

        var tableRightX = runningX;
        Debug.WriteLine($"Print table layout: pageWidth={pageWidth:F2}, tableLeft={tableLeft:F2}, tableRightX={tableRightX:F2}, rightMargin={rightMargin:F2}, availableRight={pageWidth - rightMargin:F2}");
        Debug.WriteLine($"Print table vertical boundaries X: {string.Join(", ", boundaries.Select(x => x.ToString("F2", CultureInfo.InvariantCulture)))}");
    }

    [Conditional("DEBUG")]
    private static void TracePaginationDecision(
        string? orderNo,
        int pageNo,
        int remainingRows,
        int rowsOnPage,
        bool isFinalPage,
        double usedHeight,
        double pageHeightLimit,
        double footerHeight)
    {
        Debug.WriteLine(
            $"Print pagination: order={orderNo ?? string.Empty}, page={pageNo}, remaining={remainingRows}, rowsOnPage={rowsOnPage}, final={isFinalPage}, usedHeight={usedHeight:F2}, pageLimit={pageHeightLimit:F2}, footerHeight={footerHeight:F2}");
    }

    [Conditional("DEBUG")]
    private static void TraceColumnWidthDetails(
        IReadOnlyList<PrintColumnDefinition> columns,
        IReadOnlyDictionary<string, double> titleWidths,
        IReadOnlyDictionary<string, double> dataWidths,
        IReadOnlyDictionary<string, double> finalWidths,
        double contentWidth)
    {
        var noteWidth = finalWidths.TryGetValue("Note", out var computedNoteWidth) ? computedNoteWidth : 0d;
        foreach (var column in columns)
        {
            var modelDataWidthText = string.Equals(column.Key, "Model", StringComparison.Ordinal)
                ? (dataWidths.TryGetValue(column.Key, out var modelDataWidth) ? modelDataWidth.ToString("F2", CultureInfo.InvariantCulture) : "0.00")
                : "N/A";

            Debug.WriteLine(
                $"Print column width: key={column.Key}, header={column.Title}, titleWidth={(titleWidths.TryGetValue(column.Key, out var titleWidth) ? titleWidth : 0d):F2}, modelMaxDataWidth={modelDataWidthText}, finalWidth={finalWidths[column.Key]:F2}, noteWidth={noteWidth:F2}");
        }

        Debug.WriteLine($"Print column width total: used={finalWidths.Values.Sum():F2}, contentWidth={contentWidth:F2}, noteWidth={noteWidth:F2}");
    }

}
