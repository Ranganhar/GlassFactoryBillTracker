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
    private const double CellPaddingLeftDip = 3d;
    private const double CellPaddingRightDip = 3d;
    private const double CellPaddingTopDip = 2d;
    private const double CellPaddingBottomDip = 2d;
    private const double DotMatrixInnerPaddingDip = 6d;
    private const double BorderStrokeDip = 0.5d;
    private const double DotMatrixOuterBorderDip = 0.8d;
    private const double DotMatrixHeaderGapDip = 1.5d;
    private const double DotMatrixMinNoteWidthDip = 40d;
    private const double CellHorizontalInsetsDip = CellPaddingLeftDip + CellPaddingRightDip + (BorderStrokeDip * 2d);

    private sealed class DotMatrixRowLayout
    {
        public required string[] Values { get; init; }
        public required double Height { get; init; }
        public required bool NoteWrapped { get; init; }
    }

    private sealed class DotMatrixColumnLayout
    {
        public required IReadOnlyDictionary<string, double> TitleWidths { get; init; }
        public required IReadOnlyDictionary<string, double> MaxDataWidths { get; init; }
        public required IReadOnlyDictionary<string, double> FinalWidths { get; init; }
        public required IReadOnlyList<double> WidthsByIndex { get; init; }
        public required double NoteTextWidth { get; init; }
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
        var scaleResult = PrintScaleCalculator.Compute(options);
        TraceScaleSelection(options, scaleResult, pageWidth, pageHeight);
        var margin = MmToDip(DotMatrixMarginMm);
        var baseFontSize = Math.Max(8d, options.FontSize);
        var tableFontSize = Math.Max(8d, baseFontSize - 1d);
        var rowVerticalPadding = CellPaddingTopDip + CellPaddingBottomDip;
        var baseTextRowHeight = Math.Max(1d, MmToDip(DotMatrixBaseRowMm) - rowVerticalPadding);
        var tableHeaderHeight = Math.Max(
            MmToDip(DotMatrixTableHeaderMm),
            MeasureSingleLineHeight(new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), tableFontSize) + rowVerticalPadding);

        var contentWidth = pageWidth - (margin * 2d);
        var contentHeight = pageHeight - (margin * 2d);
        var tableWidth = Math.Max(80d, contentWidth - (DotMatrixInnerPaddingDip * 2d));
        var typeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        foreach (var order in orders)
        {
            var columns = CreatePrintColumns();
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

            var columnLayout = ComputeColumnLayoutForPrint(
                columns,
                sourceRows,
                typeface,
                tableFontSize,
                MeasurementPixelsPerDip,
                tableWidth,
                0d,
                CellHorizontalInsetsDip);
            var columnWidths = columnLayout.WidthsByIndex;
            var noteTextWidth = columnLayout.NoteTextWidth;

            var headerHeight = MeasureHeaderBlockHeight(order, options, tableWidth);
            var pageRowsHeightLimit = Math.Max(
                Math.Max(baseTextRowHeight, tableFontSize * 1.4d) + rowVerticalPadding,
                contentHeight - (DotMatrixInnerPaddingDip * 2d) - headerHeight - DotMatrixHeaderGapDip - tableHeaderHeight);

            var rowLayouts = BuildRowLayouts(
                columns,
                sourceRows,
                typeface,
                tableFontSize,
                noteTextWidth,
                baseTextRowHeight,
                rowVerticalPadding,
                order.OrderNo);

            var footerHeight = MeasureFooterRowHeight(baseFontSize);
            var pageLayouts = PaginateRows(rowLayouts, pageRowsHeightLimit, footerHeight, order.OrderNo);

            foreach (var page in pageLayouts)
            {
                var pageContent = new PageContent();
                var fixedPage = CreateDotMatrixPage(
                    order,
                    options,
                    columns,
                    pageWidth,
                    pageHeight,
                    scaleResult,
                    margin,
                    headerHeight,
                    tableHeaderHeight,
                    columnWidths,
                    page.Rows,
                    page.IncludeFooter,
                    footerHeight,
                    tableFontSize);
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
        double baseTextRowHeight,
        double rowVerticalPadding,
        string? orderNo)
    {
        var rows = new List<DotMatrixRowLayout>();

        foreach (var source in sourceRows)
        {
            var values = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                values[i] = source.TryGetValue(columns[i].Key, out var value) ? value : string.Empty;
            }

            var noteText = values[NoteColumnIndex];
            var noteRequiredWidth = MeasureLongestLineWidth(noteText, typeface, tableFontSize, MeasurementPixelsPerDip);
            var noteHeight = MeasureWrappedTextHeight(noteText, typeface, tableFontSize, MeasurementPixelsPerDip, noteTextWidth);
            var rowTextHeight = Math.Max(baseTextRowHeight, noteHeight);
            var rowHeight = rowTextHeight + rowVerticalPadding;

            rows.Add(new DotMatrixRowLayout
            {
                Values = values,
                Height = rowHeight,
                NoteWrapped = noteRequiredWidth > noteTextWidth + 0.1d
            });
        }

        TraceRowHeights(orderNo, rows, noteTextWidth);

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
                var rowHeight = row.Height;
                var isLastRow = nextIndex == rows.Count - 1;
                var requiredHeight = rowHeight + (isLastRow ? footerHeight : 0d);
                var canFit = (usedHeight + requiredHeight) <= (pageRowsHeightLimit + 0.1d);
                if (canFit || pageRows.Count == 0)
                {
                    pageRows.Add(new DotMatrixRowLayout
                    {
                        Values = row.Values,
                        Height = rowHeight,
                        NoteWrapped = row.NoteWrapped
                    });
                    usedHeight += rowHeight;
                    nextIndex++;
                    continue;
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
        IReadOnlyList<PrintColumnDefinition> columns,
        double pageWidth,
        double pageHeight,
        PrintScaleResult scaleResult,
        double margin,
        double measuredHeaderHeight,
        double tableHeaderHeight,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<DotMatrixRowLayout> pageRows,
        bool includeFooter,
        double footerHeight,
        double tableFontSize)
    {
        options ??= new PrintBillOptions();
        order ??= new OrderExportDto();
        var baseFontSize = Math.Max(8d, options.FontSize);
        var fixedPage = new FixedPage
        {
            Width = scaleResult.ViewportWidthDip,
            Height = scaleResult.ViewportHeightDip,
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
        Canvas.SetLeft(contentBorder, margin);
        Canvas.SetTop(contentBorder, margin);

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
            columns,
            columnWidths,
            pageRows,
            includeFooter ? $"合计：{FormatMoney2(order.TotalAmount)}" : null,
            tableFontSize,
            tableHeaderHeight,
            footerHeight);
        Grid.SetRow(table, 1);
        root.Children.Add(table);

        contentBorder.Child = root;

        var transformedRoot = new Canvas
        {
            Width = pageWidth,
            Height = pageHeight,
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(scaleResult.Scale, scaleResult.Scale),
                    new TranslateTransform(scaleResult.TranslateXDip, scaleResult.TranslateYDip)
                }
            }
        };
        transformedRoot.Children.Add(contentBorder);

        fixedPage.Children.Add(transformedRoot);
        return fixedPage;
    }

    private static Border BuildPageTable(
        IReadOnlyList<PrintColumnDefinition> columns,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<DotMatrixRowLayout> pageRows,
        string? totalText,
        double tableFontSize,
        double tableHeaderHeight,
        double footerHeight)
    {
        var table = new Grid
        {
            Width = columnWidths.Sum(),
            HorizontalAlignment = HorizontalAlignment.Left,
            SnapsToDevicePixels = true
        };

        var columnLefts = BuildColumnLefts(columnWidths);
        TraceColumnRenderRanges("DotMatrix", columns, columnWidths, columnLefts);

        foreach (var width in columnWidths)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        }

        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tableHeaderHeight) });
        for (var col = 0; col < columns.Count; col++)
        {
            var cell = CreateCell(
                columns[col].Title,
                bold: true,
                fontSize: tableFontSize,
                alignment: TextAlignment.Left,
                wrap: false,
                trim: false,
                cellWidth: columnWidths[col],
                debugContext: "DotMatrix",
                rowIndex: 0,
                columnIndex: col,
                columnKey: columns[col].Key,
                cellLeft: columnLefts[col]);
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
                    fontSize: tableFontSize,
                    alignment: TextAlignment.Left,
                    wrap: isNote,
                    trim: false,
                    cellWidth: columnWidths[col],
                    debugContext: "DotMatrix",
                    rowIndex: rowIndex,
                    columnIndex: col,
                    columnKey: columns[col].Key,
                    cellLeft: columnLefts[col]);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        if (!string.IsNullOrWhiteSpace(totalText))
        {
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(footerHeight) });
            var totalCell = CreateTotalFooterCell(
                totalText,
                fontSize: tableFontSize,
                cellWidth: columnWidths.Sum(),
                debugContext: "DotMatrix",
                rowIndex: rowIndex,
                columnIndex: 0,
                columnKey: "Total",
                cellLeft: 0d);
            Grid.SetRow(totalCell, rowIndex);
            Grid.SetColumn(totalCell, 0);
            Grid.SetColumnSpan(totalCell, columns.Count);
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
        var columnLayout = ComputeColumnLayoutForPrint(
            columns,
            valuesByRow,
            new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            tableFontSize,
            MeasurementPixelsPerDip,
            contentWidth,
            0d,
            CellHorizontalInsetsDip);
        var columnWidths = columnLayout.WidthsByIndex;
        TraceTableCoordinates(horizontalPadding, columnWidths, pageWidth, horizontalPadding);
        var columnLefts = BuildColumnLefts(columnWidths);
        TraceColumnRenderRanges(isDotMatrix ? "A4-DotMatrix" : "A4", columns, columnWidths, columnLefts);
        foreach (var width in columnWidths)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        }
        table.Width = Math.Min(contentWidth, columnWidths.Sum());

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = CreateCell(
                headers[col],
                bold: true,
                fontSize: tableFontSize,
                alignment: TextAlignment.Left,
                wrap: false,
                trim: false,
                cellWidth: columnWidths[col],
                debugContext: isDotMatrix ? "A4-DotMatrix" : "A4",
                rowIndex: 0,
                columnIndex: col,
                columnKey: columns[col].Key,
                cellLeft: columnLefts[col]);
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
                var cellValue = values.TryGetValue(columns[col].Key, out var value) ? value : string.Empty;
                var cell = CreateCell(
                    cellValue,
                    bold: false,
                    fontSize: tableFontSize,
                    alignment: TextAlignment.Left,
                    wrap: col == NoteColumnIndex,
                    trim: false,
                    cellWidth: columnWidths[col],
                    debugContext: isDotMatrix ? "A4-DotMatrix" : "A4",
                    rowIndex: rowIndex,
                    columnIndex: col,
                    columnKey: columns[col].Key,
                    cellLeft: columnLefts[col]);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var totalCell = CreateTotalFooterCell(
            $"合计：{FormatMoney2(order.TotalAmount)}",
            fontSize: tableFontSize,
            cellWidth: columnWidths.Sum(),
            debugContext: isDotMatrix ? "A4-DotMatrix" : "A4",
            rowIndex: rowIndex,
            columnIndex: 0,
            columnKey: "Total",
            cellLeft: 0d);
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

    private static Border CreateCell(
        string text,
        bool bold,
        double fontSize,
        TextAlignment alignment,
        bool wrap,
        bool trim,
        double cellWidth,
        string debugContext,
        int rowIndex,
        int columnIndex,
        string columnKey,
        double cellLeft)
    {
        var textMaxWidth = Math.Max(8d, cellWidth - CellHorizontalInsetsDip);
        var margin = new Thickness(0d);
        var clippingEnabled = false;
        var textTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None;

        TraceRenderCell(
            debugContext,
            rowIndex,
            columnIndex,
            columnKey,
            cellLeft,
            cellWidth,
            textMaxWidth,
            textTrimming,
            margin,
            clippingEnabled);

        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(BorderStrokeDip),
            Padding = new Thickness(CellPaddingLeftDip, CellPaddingTopDip, CellPaddingRightDip, CellPaddingBottomDip),
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = text,
                FontFamily = PrintFontFamily,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontSize = Math.Max(8d, fontSize),
                TextAlignment = alignment,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = textTrimming,
                Margin = margin,
                MaxWidth = textMaxWidth
            }
        };
    }

    private static Border CreateTotalFooterCell(
        string totalText,
        double fontSize,
        double cellWidth,
        string debugContext,
        int rowIndex,
        int columnIndex,
        string columnKey,
        double cellLeft)
    {
        var boldTypeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var textMaxWidth = Math.Max(8d, cellWidth - CellHorizontalInsetsDip);
        var measuredWidth = Math.Min(textMaxWidth, MeasureTextWidth(totalText, boldTypeface, Math.Max(8d, fontSize), MeasurementPixelsPerDip));

        TraceRenderCell(
            debugContext,
            rowIndex,
            columnIndex,
            columnKey,
            cellLeft,
            cellWidth,
            textMaxWidth,
            TextTrimming.None,
            new Thickness(0d),
            clippingEnabled: false);

        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(BorderStrokeDip),
            Padding = new Thickness(CellPaddingLeftDip, CellPaddingTopDip, CellPaddingRightDip, CellPaddingBottomDip),
            SnapsToDevicePixels = true,
            Child = new Grid
            {
                Width = textMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    new TextBlock
                    {
                        Text = totalText,
                        FontFamily = PrintFontFamily,
                        FontWeight = FontWeights.Bold,
                        FontSize = Math.Max(8d, fontSize),
                        Width = measuredWidth,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Right,
                        TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.None
                    }
                }
            }
        };
    }

    private static double MeasureFooterRowHeight(double baseFontSize)
    {
        var typeface = new Typeface(PrintFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var fontSize = Math.Max(8d, baseFontSize - 1d);
        var lineHeight = MeasureSingleLineHeight(typeface, fontSize);
        var baseRowHeight = MmToDip(DotMatrixBaseRowMm);
        return Math.Max(baseRowHeight, lineHeight + (CellPaddingTopDip + CellPaddingBottomDip));
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

    private static double MeasureWrappedTextHeight(string text, Typeface typeface, double fontSize, double pixelsPerDip, double maxWidth)
    {
        var formattedText = new FormattedText(
            string.IsNullOrEmpty(text) ? " " : text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            pixelsPerDip)
        {
            MaxTextWidth = Math.Max(8d, maxWidth),
            Trimming = TextTrimming.None
        };
        return formattedText.Height;
    }

    private static double MeasureLongestLineWidth(string? text, Typeface typeface, double fontSize, double pixelsPerDip)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0d;
        }

        var maxWidth = 0d;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines)
        {
            var width = MeasureTextWidth(line, typeface, fontSize, pixelsPerDip);
            if (width > maxWidth)
            {
                maxWidth = width;
            }
        }

        return maxWidth;
    }

    private static DotMatrixColumnLayout ComputeColumnLayoutForPrint(
        IReadOnlyList<PrintColumnDefinition> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        Typeface typeface,
        double fontSize,
        double pixelsPerDip,
        double availableTableWidth,
        double gapDip,
        double horizontalInsetsDip)
    {
        var titleWidths = new Dictionary<string, double>(StringComparer.Ordinal);
        var maxDataWidths = new Dictionary<string, double>(StringComparer.Ordinal);
        var finalWidths = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var column in columns)
        {
            var titleWidth = MeasureTextWidth(column.Title, typeface, fontSize, pixelsPerDip);
            titleWidths[column.Key] = titleWidth;

            var maxDataWidth = 0d;
            foreach (var row in rows)
            {
                var value = row.TryGetValue(column.Key, out var text) ? text : string.Empty;
                var measuredWidth = MeasureTextWidth(value, typeface, fontSize, pixelsPerDip);
                if (measuredWidth > maxDataWidth)
                {
                    maxDataWidth = measuredWidth;
                }
            }

            maxDataWidths[column.Key] = maxDataWidth;
        }

        var nonNoteColumns = columns.Where(column => !column.IsNote).ToList();
        var noteColumn = columns.First(column => column.IsNote);

        foreach (var column in nonNoteColumns)
        {
            var measuredWidth = Math.Max(titleWidths[column.Key], maxDataWidths[column.Key]) + horizontalInsetsDip;
            finalWidths[column.Key] = Math.Clamp(Math.Max(column.MinWidth, measuredWidth), column.MinWidth, column.MaxWidth);
        }

        var totalGaps = gapDip * Math.Max(0, columns.Count - 1);
        var nonNoteTotal = nonNoteColumns.Sum(column => finalWidths[column.Key]);
        var noteWidth = availableTableWidth - nonNoteTotal - totalGaps;

        if (noteWidth < noteColumn.MinWidth)
        {
            var deficit = noteColumn.MinWidth - noteWidth;
            while (deficit > 0.1d)
            {
                var totalSpare = nonNoteColumns.Sum(column => Math.Max(0d, finalWidths[column.Key] - column.MinWidth));
                if (totalSpare <= 0.1d)
                {
                    break;
                }

                foreach (var column in nonNoteColumns)
                {
                    var spare = Math.Max(0d, finalWidths[column.Key] - column.MinWidth);
                    if (spare <= 0d)
                    {
                        continue;
                    }

                    var reduction = Math.Min(spare, deficit * (spare / totalSpare));
                    finalWidths[column.Key] -= reduction;
                    deficit -= reduction;
                    if (deficit <= 0.1d)
                    {
                        break;
                    }
                }
            }

            nonNoteTotal = nonNoteColumns.Sum(column => finalWidths[column.Key]);
            noteWidth = availableTableWidth - nonNoteTotal - totalGaps;
        }

        finalWidths[noteColumn.Key] = Math.Clamp(noteWidth, 8d, noteColumn.MaxWidth);

        var totalWidth = finalWidths.Values.Sum() + totalGaps;
        if (totalWidth > availableTableWidth + 0.1d)
        {
            var overflow = totalWidth - availableTableWidth;
            finalWidths[noteColumn.Key] = Math.Max(8d, finalWidths[noteColumn.Key] - overflow);
        }

        var widthsByIndex = columns.Select(column => finalWidths[column.Key]).ToList();
        var noteTextWidth = Math.Max(8d, finalWidths[noteColumn.Key] - horizontalInsetsDip);

        TraceColumnWidthDetails(columns, titleWidths, maxDataWidths, finalWidths, availableTableWidth, horizontalInsetsDip);

        return new DotMatrixColumnLayout
        {
            TitleWidths = titleWidths,
            MaxDataWidths = maxDataWidths,
            FinalWidths = finalWidths,
            WidthsByIndex = widthsByIndex,
            NoteTextWidth = noteTextWidth
        };
    }

    private static IReadOnlyList<PrintColumnDefinition> CreatePrintColumns()
    {
        return new[]
        {
            new PrintColumnDefinition { Key = "Model", Title = "型号", MinWidth = 48d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "Length", Title = "长(mm)", MinWidth = 28d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "Width", Title = "宽(mm)", MinWidth = 28d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "Quantity", Title = "数量", MinWidth = 26d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "UnitPrice", Title = "单价(元/㎡)", MinWidth = 40d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "HoleFee", Title = "打孔费", MinWidth = 40d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "OtherFee", Title = "其他费用", MinWidth = 40d, MaxWidth = double.MaxValue, IsNote = false },
            new PrintColumnDefinition { Key = "Amount", Title = "金额(元)", MinWidth = 44d, MaxWidth = double.MaxValue, IsNote = false },
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
            companyTypeface,
            baseFontSize + 2d,
            MeasurementPixelsPerDip,
            maxWidth);

        var contactPhone = options.UseCustomerPhone ? (order.CustomerPhone ?? string.Empty) : (options.CustomPhone ?? string.Empty);
        var metaText = $"订单号: {order.OrderNo ?? string.Empty}    日期: {order.DateTime:yyyy-MM-dd HH:mm:ss}    客户: {order.CustomerName ?? string.Empty}    电话: {contactPhone}";
        var metaHeight = MeasureWrappedTextHeight(metaText, metaTypeface, baseFontSize - 1d, MeasurementPixelsPerDip, maxWidth);

        return companyHeight + 2d + metaHeight;
    }

    private static double MeasureTextWidth(string text, Typeface typeface, double fontSize, double pixelsPerDip)
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
        return OrderAmountCalculator.RoundAmount(value).ToString("F0", CultureInfo.InvariantCulture);
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
        double availableTableWidth,
        double horizontalInsetsDip)
    {
        var noteWidth = finalWidths.TryGetValue("Note", out var computedNoteWidth) ? computedNoteWidth : 0d;
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var titleWidth = titleWidths.TryGetValue(column.Key, out var measuredTitleWidth) ? measuredTitleWidth : 0d;
            var dataWidth = dataWidths.TryGetValue(column.Key, out var measuredDataWidth) ? measuredDataWidth : 0d;
            var finalWidth = finalWidths.TryGetValue(column.Key, out var measuredFinalWidth) ? measuredFinalWidth : 0d;
            var textWidth = Math.Max(8d, finalWidth - horizontalInsetsDip);
            var requiresEllipsis = !column.IsNote && dataWidth > textWidth + 0.1d;

            Debug.WriteLine(
                $"Print column width: index={index}, key={column.Key}, header={column.Title}, titleMax={titleWidth:F2}, dataMax={dataWidth:F2}, final={finalWidth:F2}, textWidth={textWidth:F2}, requiresEllipsis={requiresEllipsis}, noteWidth={noteWidth:F2}");
        }

        Debug.WriteLine($"Print column width total: used={finalWidths.Values.Sum():F2}, availableTableWidth={availableTableWidth:F2}, noteWidth={noteWidth:F2}");
    }

    private static IReadOnlyList<double> BuildColumnLefts(IReadOnlyList<double> columnWidths)
    {
        var lefts = new List<double>(columnWidths.Count);
        var current = 0d;
        for (var i = 0; i < columnWidths.Count; i++)
        {
            lefts.Add(current);
            current += columnWidths[i];
        }

        return lefts;
    }

    [Conditional("DEBUG")]
    private static void TraceColumnRenderRanges(
        string context,
        IReadOnlyList<PrintColumnDefinition> columns,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<double> columnLefts)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var left = columnLefts[i];
            var right = left + columnWidths[i];
            Debug.WriteLine(
                $"Print column render range: ctx={context}, index={i}, key={columns[i].Key}, left={left:F2}, right={right:F2}, width={columnWidths[i]:F2}, innerTextWidth={Math.Max(8d, columnWidths[i] - CellHorizontalInsetsDip):F2}");
        }
    }

    [Conditional("DEBUG")]
    private static void TraceRenderCell(
        string context,
        int rowIndex,
        int columnIndex,
        string columnKey,
        double cellLeft,
        double cellWidth,
        double textMaxWidth,
        TextTrimming trimming,
        Thickness margin,
        bool clippingEnabled)
    {
        var textLeft = cellLeft + BorderStrokeDip + CellPaddingLeftDip + margin.Left;
        var textRight = cellLeft + cellWidth - BorderStrokeDip - CellPaddingRightDip - margin.Right;
        Debug.WriteLine(
            $"Print cell render: ctx={context}, row={rowIndex}, col={columnIndex}, key={columnKey}, cell=[{cellLeft:F2},{cellLeft + cellWidth:F2}], text=[{textLeft:F2},{textRight:F2}], maxTextWidth={textMaxWidth:F2}, trimming={trimming}, paddingL={CellPaddingLeftDip:F2}, paddingR={CellPaddingRightDip:F2}, paddingT={CellPaddingTopDip:F2}, paddingB={CellPaddingBottomDip:F2}, border={BorderStrokeDip:F2}, marginL={margin.Left:F2}, marginR={margin.Right:F2}, clip={clippingEnabled}");
    }

    [Conditional("DEBUG")]
    private static void TraceRowHeights(string? orderNo, IReadOnlyList<DotMatrixRowLayout> rowLayouts, double noteTextWidth)
    {
        var sampleCount = Math.Min(10, rowLayouts.Count);
        Debug.WriteLine($"Print row heights: order={orderNo ?? string.Empty}, rows={rowLayouts.Count}, noteTextWidth={noteTextWidth:F2}, sampled={sampleCount}");
        for (var i = 0; i < sampleCount; i++)
        {
            Debug.WriteLine($"Print row height: index={i}, height={rowLayouts[i].Height:F2}, wrapped={rowLayouts[i].NoteWrapped}");
        }
    }

    [Conditional("DEBUG")]
    private static void TraceScaleSelection(PrintBillOptions options, PrintScaleResult result, double contentWidthDip, double contentHeightDip)
    {
        Debug.WriteLine(
            $"Print scale: printer={options.PrinterName ?? string.Empty}, fit={options.FitToPageScale}, manual={options.ManualScalePercent}%, imageableKnown={options.PrinterImageableAreaKnown}, imageableFromCaps={options.PrinterImageableAreaFromCapabilities}, origin=({options.PrinterImageableOriginXDip:F2},{options.PrinterImageableOriginYDip:F2}), imageable=({options.PrinterImageableWidthDip:F2}x{options.PrinterImageableHeightDip:F2}), logical=({contentWidthDip:F2}x{contentHeightDip:F2}), scale={result.Scale:F4}, translate=({result.TranslateXDip:F2},{result.TranslateYDip:F2}), viewport=({result.ViewportWidthDip:F2}x{result.ViewportHeightDip:F2}), source={result.Source}");
    }

}
