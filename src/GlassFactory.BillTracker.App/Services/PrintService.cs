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
    private const double DotMatrixPageWidthMm = 216d;
    private const double DotMatrixPageHeightMm = 93d;
    private const double DotMatrixMarginMm = 5d;
    private const double DotMatrixHeaderBlockMm = 16d;
    private const double DotMatrixTableHeaderMm = 7d;
    private const double DotMatrixRowMm = 6.5d;
    private const double DotMatrixFooterMm = 7d;

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
        var headerBlockHeight = MmToDip(DotMatrixHeaderBlockMm);
        var tableHeaderHeight = MmToDip(DotMatrixTableHeaderMm);
        var rowHeight = MmToDip(DotMatrixRowMm);
        var footerHeight = MmToDip(DotMatrixFooterMm);
        var baseFontSize = Math.Max(8d, options.FontSize);

        foreach (var order in orders)
        {
            var headers = new[] { "型号", "长（mm）", "宽（mm）", "数量", "单价（元/㎡）", "打孔费", "其他费用", "金额（元）", "备注" };
            var rows = (order.Items ?? Array.Empty<OrderExportItemDto>())
                .Where(item => item is not null)
                .Select(item => new[]
                {
                    item.Model ?? string.Empty,
                    FormatInt(item.GlassLengthMm),
                    FormatInt(item.GlassWidthMm),
                    item.Quantity.ToString(CultureInfo.InvariantCulture),
                    FormatInt(item.GlassUnitPricePerM2),
                    FormatInt(item.HoleFee),
                    FormatInt(item.OtherFee),
                    FormatMoney2(item.Amount),
                    item.Note ?? string.Empty
                })
                .ToList();

            var contentWidth = Math.Max(120d, pageWidth - (margin * 2d) - 0.5d);
            var columnWidths = BuildColumnWidths(contentWidth, headers, rows, baseFontSize, cellPadding: 6d);

            var rowsPerPageWithoutFooter = ComputeRowsPerPage(pageHeight, margin, headerBlockHeight, tableHeaderHeight, rowHeight, includeFooter: false, footerHeight);
            var rowsPerPageWithFooter = ComputeRowsPerPage(pageHeight, margin, headerBlockHeight, tableHeaderHeight, rowHeight, includeFooter: true, footerHeight);

            var nextRowIndex = 0;
            var pageNo = 1;
            while (true)
            {
                var remaining = rows.Count - nextRowIndex;
                var isFinalPage = remaining <= rowsPerPageWithFooter;

                int rowsThisPage;
                if (remaining <= 0)
                {
                    rowsThisPage = 0;
                    isFinalPage = true;
                }
                else
                {
                    rowsThisPage = isFinalPage
                        ? remaining
                        : Math.Min(remaining, rowsPerPageWithoutFooter);
                }

                TracePaginationDecision(order.OrderNo, pageNo, remaining, rowsThisPage, isFinalPage, rowsPerPageWithoutFooter, rowsPerPageWithFooter);

                var pageRows = rows.Skip(nextRowIndex).Take(rowsThisPage).ToList();
                var pageContent = new PageContent();
                var fixedPage = CreateDotMatrixPage(order, options, pageWidth, pageHeight, margin, headerBlockHeight, tableHeaderHeight, rowHeight, footerHeight, headers, columnWidths, pageRows, isFinalPage);

                ((IAddChild)pageContent).AddChild(fixedPage);
                document.Pages.Add(pageContent);

                if (remaining <= 0 || isFinalPage)
                {
                    break;
                }

                nextRowIndex += rowsThisPage;
                pageNo++;
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

    private static FixedPage CreateDotMatrixPage(
        OrderExportDto order,
        PrintBillOptions options,
        double pageWidth,
        double pageHeight,
        double margin,
        double headerBlockHeight,
        double tableHeaderHeight,
        double rowHeight,
        double footerHeight,
        IReadOnlyList<string> headers,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<string[]> pageRows,
        bool includeFooter)
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
            BorderThickness = new Thickness(0.8),
            Padding = new Thickness(6d),
            SnapsToDevicePixels = true
        };
        FixedPage.SetLeft(contentBorder, margin);
        FixedPage.SetTop(contentBorder, margin);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(headerBlockHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (includeFooter)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(footerHeight) });
        }

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Top
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

        var table = BuildPageTable(headers, columnWidths, pageRows, includeFooter ? $"合计：{FormatMoney2(order.TotalAmount)}" : null, baseFontSize, tableHeaderHeight, rowHeight, footerHeight);
        Grid.SetRow(table, 1);
        root.Children.Add(table);

        if (includeFooter)
        {
            var footer = new TextBlock
            {
                Text = "",
                FontSize = baseFontSize - 1
            };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
        }

        contentBorder.Child = root;
        fixedPage.Children.Add(contentBorder);
        return fixedPage;
    }

    private static Border BuildPageTable(
        IReadOnlyList<string> headers,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<string[]> pageRows,
        string? totalText,
        double baseFontSize,
        double tableHeaderHeight,
        double rowHeight,
        double footerHeight)
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
            var cell = CreateCell(headers[col], bold: true, isDotMatrix: true, baseFontSize, TextAlignment.Left, wrap: false, trim: true);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, col);
            table.Children.Add(cell);
        }

        var rowIndex = 1;
        foreach (var values in pageRows)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });
            for (var col = 0; col < values.Length; col++)
            {
                var trim = col == headers.Count - 1;
                var cell = CreateCell(values[col], bold: false, isDotMatrix: true, baseFontSize, TextAlignment.Left, wrap: trim, trim: trim);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        if (!string.IsNullOrWhiteSpace(totalText))
        {
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(footerHeight) });
            var totalCell = CreateCell(totalText, bold: true, isDotMatrix: true, baseFontSize, TextAlignment.Right, wrap: false, trim: false);
            Grid.SetRow(totalCell, rowIndex);
            Grid.SetColumn(totalCell, 0);
            Grid.SetColumnSpan(totalCell, headers.Count);
            table.Children.Add(totalCell);
        }

        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.8),
            SnapsToDevicePixels = true,
            Child = table
        };
    }

    private static int ComputeRowsPerPage(
        double pageHeight,
        double margin,
        double headerBlockHeight,
        double tableHeaderHeight,
        double rowHeight,
        bool includeFooter,
        double footerHeight)
    {
        var available = pageHeight - (margin * 2d) - 12d - headerBlockHeight - tableHeaderHeight - (includeFooter ? footerHeight : 0d);
        var rows = (int)Math.Floor(available / rowHeight);
        return Math.Max(1, rows);
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
        var headers = new[] { "型号", "长（mm）", "宽（mm）", "数量", "单价（元/㎡）", "打孔费", "其他费用", "金额（元）", "备注" };
        var valuesByRow = rows.Select(item => new[]
        {
            item.Model ?? string.Empty,
            FormatInt(item.GlassLengthMm),
            FormatInt(item.GlassWidthMm),
            item.Quantity.ToString(CultureInfo.InvariantCulture),
            FormatInt(item.GlassUnitPricePerM2),
            FormatInt(item.HoleFee),
            FormatInt(item.OtherFee),
            FormatMoney2(item.Amount),
            item.Note ?? string.Empty
        }).ToList();

        var table = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        var contentWidth = Math.Max(120d, pageWidth - (horizontalPadding * 2d) - tableWidthEpsilon);
        var columnWidths = BuildColumnWidths(contentWidth, headers, valuesByRow, baseFontSize, cellPadding);
        TraceTableCoordinates(horizontalPadding, columnWidths, pageWidth, horizontalPadding);
        foreach (var width in columnWidths)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        }
        table.Width = Math.Min(contentWidth, columnWidths.Sum());

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = CreateCell(headers[col], bold: true, isDotMatrix, baseFontSize, TextAlignment.Left, wrap: false, trim: true);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, col);
            table.Children.Add(cell);
        }

        var rowIndex = 1;
        foreach (var values in valuesByRow)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var col = 0; col < values.Length; col++)
            {
                var trim = col == 8;
                var cell = CreateCell(values[col], bold: false, isDotMatrix, baseFontSize, TextAlignment.Left, wrap: col == 8, trim: trim);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var totalCell = CreateCell($"合计：{FormatMoney2(order.TotalAmount)}", bold: true, isDotMatrix, baseFontSize, TextAlignment.Right, wrap: false, trim: false);
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

    private static Border CreateCell(string text, bool bold, bool isDotMatrix, double baseFontSize, TextAlignment alignment, bool wrap, bool trim)
    {
        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(3, 2, 3, 2),
            Child = new TextBlock
            {
                Text = text,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontSize = isDotMatrix ? Math.Max(8d, baseFontSize - 1) : baseFontSize,
                TextAlignment = alignment,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None
            }
        };
    }

    private static IReadOnlyList<double> BuildColumnWidths(
        double contentWidth,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows,
        double baseFontSize,
        double cellPadding)
    {
        const int noteColumnIndex = 8;
        const double minNoteWidth = 90d;

        var typeface = new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var widths = new double[headers.Count];
        var minWidths = new[] { 70d, 40d, 40d, 35d, 45d, 50d, 60d, 50d, minNoteWidth };
        var maxWidths = new[] { 140d, 80d, 80d, 65d, 90d, 90d, 90d, 90d, contentWidth };

        for (var col = 0; col < headers.Count; col++)
        {
            if (col == noteColumnIndex)
            {
                continue;
            }

            var maxTextWidth = MeasureTextWidth(headers[col], typeface, baseFontSize);
            foreach (var row in rows)
            {
                if (col >= row.Length)
                {
                    continue;
                }

                var valueWidth = MeasureTextWidth(row[col], typeface, baseFontSize);
                if (valueWidth > maxTextWidth)
                {
                    maxTextWidth = valueWidth;
                }
            }

            var measured = maxTextWidth + cellPadding;
            widths[col] = Math.Clamp(measured, minWidths[col], maxWidths[col]);
        }

        var nonNoteWidth = widths.Where((_, idx) => idx != noteColumnIndex).Sum();
        var noteWidth = contentWidth - nonNoteWidth;
        if (noteWidth < minNoteWidth)
        {
            var deficit = minNoteWidth - noteWidth;
            var shrinkableColumns = Enumerable.Range(0, headers.Count).Where(i => i != noteColumnIndex).ToList();
            while (deficit > 0.1d)
            {
                var totalSpare = shrinkableColumns.Sum(i => Math.Max(0d, widths[i] - minWidths[i]));
                if (totalSpare <= 0.1d)
                {
                    break;
                }

                foreach (var index in shrinkableColumns)
                {
                    var spare = Math.Max(0d, widths[index] - minWidths[index]);
                    if (spare <= 0d)
                    {
                        continue;
                    }

                    var delta = Math.Min(spare, deficit * (spare / totalSpare));
                    widths[index] -= delta;
                    deficit -= delta;
                    if (deficit <= 0.1d)
                    {
                        break;
                    }
                }
            }

            nonNoteWidth = widths.Where((_, idx) => idx != noteColumnIndex).Sum();
            noteWidth = contentWidth - nonNoteWidth;
        }

        widths[noteColumnIndex] = Math.Max(40d, noteWidth);
        return widths;
    }

    private static double MeasureTextWidth(string? text, Typeface typeface, double fontSize)
    {
        var formattedText = new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            1.0);
        return formattedText.WidthIncludingTrailingWhitespace;
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
        int rowsThisPage,
        bool isFinalPage,
        int rowsPerPageWithoutFooter,
        int rowsPerPageWithFooter)
    {
        Debug.WriteLine(
            $"Print pagination: order={orderNo ?? string.Empty}, page={pageNo}, remaining={remainingRows}, rowsThisPage={rowsThisPage}, final={isFinalPage}, noFooterCap={rowsPerPageWithoutFooter}, withFooterCap={rowsPerPageWithFooter}");
    }

}
