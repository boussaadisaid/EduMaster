using EduMaster.Application.Printing;
using EduMaster.UI.Common;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EduMaster.UI.Printing;

/// <summary>
/// مرسّم المطبوعات (6.3 — ط-د/D-130): نماذج الطباعة النقية المختبَرة ← FlowDocument جاهز للطباعة.
/// عربي RTL بخط Segoe UI · المبالغ تُنسَّق بالدينار هنا فقط (D-51) عبر MoneyInput.FormatDinars — نفس نص الشاشة حرفياً (WYSIWYP) ·
/// اللوغو يصل مساراً كاملاً محلولاً من IImageStore (D-38) — البنّاء لا يلمس القرص إلا قراءة الصورة ·
/// عند مبادرة التحسين البصري يُستبدل هذا المرسّم وحده — النماذج النقية واختباراتها تبقى (D-130).
/// ⚠ قاعدة وُلدت من التجريب اليدوي (عائلة D-95): أعمدة جداول FlowDocument بالبكسل المطلق فقط —
/// Auto/Star تنهار إلى حرف في السطر عند قياس المجزّئ للطباعة (تجربة 2026-08-26: كشف من سطور ← 10 صفحات).
/// 6.4 — ق-6: BuildA4Report مسار جدولي عام بنموذج واحد — الأوزان النسبية تُحوَّل هنا إلى بكسلات.
/// </summary>
public static class PrintDocumentBuilder
{
    // مقاسات الصفحة بوحدات WPF ‏(96dpi): A4 عمودي للتقارير · A5 عمودي للإيصال (نصف ورقة — ط-8)
    public static readonly Size A4Portrait = new(793.7, 1122.5);
    public static readonly Size A5Portrait = new(559.4, 793.7);

    // عرض المحتوى = عرض الصفحة − حواف PagePadding ‏(36×2) — أعمدة الجداول بالبكسل فوقه مباشرةً ومجموعها يساويه تماماً
    private const double A4ContentWidth = 721.7;   // 793.7 − 72
    private const double A5ContentWidth = 487.4;   // 559.4 − 72

    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
    private static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
    private static readonly Brush LineColor = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB));
    private static readonly Brush HeaderBg = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
    private static readonly Brush DebtRed = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xB2, 0x6B, 0x00));

    // ═══ الإيصال (قبض/صرف — المرآة من Kind داخل النموذج نفسه، ط-3) ═══
    public static FlowDocument BuildReceipt(ReceiptPrintModel model, string? logoFullPath)
    {
        var doc = NewDocument(A5Portrait);
        AddSchoolHeader(doc, model.Header, logoFullPath);
        AddSeparator(doc);

        doc.Blocks.Add(new Paragraph(new Run($"{model.DocumentTitle}  ·  {model.ReceiptNoText}"))
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 8),
        });

        doc.Blocks.Add(InfoLine("التاريخ:", model.PaidOn.ToString("yyyy-MM-dd")));
        if (model.IsRefund)
        {
            doc.Blocks.Add(InfoLine("صُرف إلى الطالب:", model.StudentName));
        }
        else
        {
            doc.Blocks.Add(InfoLine("استُلم من:", model.PayerName ?? model.StudentName));
            doc.Blocks.Add(InfoLine("عن الطالب:", model.StudentName));
        }

        // المبلغ مؤطَّر — أوضح عنصر في الوثيقة
        doc.Blocks.Add(new BlockUIContainer(new Border
        {
            BorderBrush = LineColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(60, 10, 60, 10),
            Child = new TextBlock
            {
                Text = $"{MoneyInput.FormatDinars(model.AmountCentimes)} دج",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = Ink,
            },
        }));

        if (model.HasAllocations)
        {
            doc.Blocks.Add(SectionTitle("تخصيص المبلغ:"));
            var table = DataTable(new[] { "المستحق", "المبلغ (دج)" }, new[] { 340.0, 147.4 });   // = A5ContentWidth
            foreach (var line in model.Allocations)
                AddRow(table, line.SourceDescription, MoneyInput.FormatDinars(line.AmountCentimes));
            doc.Blocks.Add(table);
        }

        if (model.HasUnallocated)
            doc.Blocks.Add(new Paragraph(new Run($"غير مخصص من هذه الدفعة (يبقى زائدة دائنة للطالب — D-107): {MoneyInput.FormatDinars(model.UnallocatedCentimes)} دج"))
            {
                Foreground = Amber,
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
            });

        if (!string.IsNullOrWhiteSpace(model.Note))
            doc.Blocks.Add(InfoLine(model.IsRefund ? "السبب:" : "ملاحظة:", model.Note!));

        doc.Blocks.Add(new Paragraph(new Run("توقيع المستلم:  ______________________"))
        {
            Foreground = Gray,
            Margin = new Thickness(0, 30, 0, 0),
        });

        return doc;
    }

    // ═══ تقرير حركة القبض لفترة (6.1 — الإجماليات من مصدرها المختبَر بلا إعادة حساب، WYSIWYP) ═══
    public static FlowDocument BuildPaymentMovement(PaymentMovementPrintModel model, string? logoFullPath)
    {
        var report = model.Report;
        var doc = NewDocument(A4Portrait);
        AddSchoolHeader(doc, model.Header, logoFullPath);
        AddSeparator(doc);

        doc.Blocks.Add(new Paragraph(new Run("تقرير حركة القبض والصرف"))
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2),
        });
        doc.Blocks.Add(new Paragraph(new Run($"الفترة: من {report.From:yyyy-MM-dd} إلى {report.To:yyyy-MM-dd}"))
        {
            FontSize = 11,
            Foreground = Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        });

        doc.Blocks.Add(new Paragraph(new Run(
            $"قبض: {MoneyInput.FormatDinars(report.ReceiptsTotalCentimes)} دج ({report.ReceiptsCount}) · " +
            $"صرف: {MoneyInput.FormatDinars(report.RefundsTotalCentimes)} دج ({report.RefundsCount}) · " +
            $"الصافي: {MoneyInput.FormatDinars(report.NetCentimes)} دج · " +
            $"غير مخصص: {MoneyInput.FormatDinars(report.UnallocatedTotalCentimes)} دج"))
        {
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });

        if (report.Rows.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("لا حركة قبض في هذه الفترة")) { Foreground = Gray, TextAlignment = TextAlignment.Center });
            return doc;
        }

        // إيصال · التاريخ · الطالب · الدافع · النوع · المبلغ · غير مخصص · ملاحظة — المجموع = A4ContentWidth
        var table = DataTable(
            new[] { "إيصال", "التاريخ", "الطالب", "الدافع", "النوع", "المبلغ (دج)", "غير مخصص", "ملاحظة" },
            new[] { 58.0, 72.0, 160.0, 105.0, 48.0, 82.0, 72.0, 124.7 });
        foreach (var row in report.Rows)
            AddRow(table,
                row.ReceiptNoText,
                row.PaidOn.ToString("yyyy-MM-dd"),
                row.StudentName,
                row.PayerName ?? "—",
                row.KindText,
                MoneyInput.FormatDinars(row.AmountCentimes),
                row.HasUnallocated ? MoneyInput.FormatDinars(row.UnallocatedCentimes) : "—",
                row.Note ?? "—");
        doc.Blocks.Add(table);

        return doc;
    }

    // ═══ كشف حساب طالب (6.1) ═══
    public static FlowDocument BuildStudentStatement(StudentStatementPrintModel model, string? logoFullPath)
    {
        var statement = model.Statement;
        var student = model.Student;
        var doc = NewDocument(A4Portrait);
        AddSchoolHeader(doc, model.Header, logoFullPath);
        AddSeparator(doc);

        doc.Blocks.Add(new Paragraph(new Run(statement.IsAcademicYearScoped ? $"كشف حساب طالب — {statement.AcademicYearName}" : "كشف حساب طالب"))
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2),
        });
        doc.Blocks.Add(new Paragraph(new Run($"{student.FullName} · {student.CategoryText} · هاتف: {student.Phone ?? "—"} · الولي: {student.GuardianFullName ?? "—"}"))
        {
            FontSize = 11,
            Foreground = Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        });

        doc.Blocks.Add(new Paragraph(new Run(
            statement.IsAcademicYearScoped
                ? $"الرصيد القائم للسنة: {MoneyInput.FormatDinars(statement.BalanceCentimes)} دج · " +
                  $"المخصص للسنة من الإيصالات: {MoneyInput.FormatDinars(statement.ReceiptsTotalCentimes)} دج · " +
                  $"الزائدة الدائنة (كل السنوات): {MoneyInput.FormatDinars(statement.CreditCentimes)} دج"
                : $"الرصيد القائم: {MoneyInput.FormatDinars(statement.BalanceCentimes)} دج · " +
                  $"الزائدة الدائنة: {MoneyInput.FormatDinars(statement.CreditCentimes)} دج · " +
                  $"إجمالي المقبوض: {MoneyInput.FormatDinars(statement.ReceiptsTotalCentimes)} دج · " +
                  $"إجمالي المصروف: {MoneyInput.FormatDinars(statement.RefundsTotalCentimes)} دج"))
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = DebtRed,
            Margin = new Thickness(0, 0, 0, 8),
        });

        doc.Blocks.Add(SectionTitle("المستحقات:"));
        if (statement.Charges.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("لا مستحقات لهذا الطالب")) { Foreground = Gray, Margin = new Thickness(0, 2, 0, 8) });
        }
        else
        {
            // النوع · الوصف · الأصلي · الحالي · المخصوص · المتبقي · الحالة · سبب التسوية · أُنشئ — المجموع = A4ContentWidth
            var charges = DataTable(
                new[] { "النوع", "الوصف", "الأصلي", "الحالي", "المخصوص", "المتبقي", "الحالة", "سبب التسوية", "أُنشئ" },
                new[] { 62.0, 160.0, 70.0, 70.0, 70.0, 52.0, 92.7, 75.0 });
            foreach (var charge in statement.Charges)
                AddRow(charges,
                    charge.KindText,
                    charge.SourceDescription,
                    MoneyInput.FormatDinars(charge.OriginalAmountCentimes),
                    MoneyInput.FormatDinars(charge.AmountCentimes),
                    MoneyInput.FormatDinars(charge.AllocatedCentimes),
                    MoneyInput.FormatDinars(charge.RemainingCentimes),
                    charge.StatusText,
                    charge.AdjustmentNote ?? "—",
                    charge.CreatedAtUtc.ToString("yyyy-MM-dd"));
            doc.Blocks.Add(charges);
        }

        doc.Blocks.Add(SectionTitle("الإيصالات وتخصيصاتها:"));
        if (statement.Payments.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("لا إيصالات لهذا الطالب")) { Foreground = Gray });
        }
        else
        {
            // إيصال · التاريخ · النوع · الدافع · المبلغ · التخصيص · ملاحظة — المجموع = A4ContentWidth
            var payments = statement.IsAcademicYearScoped
                ? DataTable(new[] { "إيصال", "التاريخ", "النوع", "الدافع", "قيمة الإيصال", "المخصص للسنة", "التخصيص", "ملاحظة" },
                    new[] { 55.0, 70.0, 48.0, 105.0, 75.0, 82.0, 180.0, 130.0 })
                : DataTable(new[] { "إيصال", "التاريخ", "النوع", "الدافع", "المبلغ (دج)", "التخصيص", "ملاحظة" },
                    new[] { 58.0, 72.0, 48.0, 120.0, 82.0, 210.0, 131.7 });
            foreach (var payment in statement.Payments)
            {
                var row = new TableRow();
                row.Cells.Add(MakeCell(payment.ReceiptNoText));
                row.Cells.Add(MakeCell(payment.PaidOn.ToString("yyyy-MM-dd")));
                row.Cells.Add(MakeCell(payment.KindText));
                row.Cells.Add(MakeCell(payment.PayerName ?? "—"));
                row.Cells.Add(MakeCell(MoneyInput.FormatDinars(payment.AmountCentimes)));
                if (statement.IsAcademicYearScoped)
                    row.Cells.Add(MakeCell(MoneyInput.FormatDinars(payment.AllocatedToSelectedAcademicYearCentimes)));
                row.Cells.Add(payment.Allocations.Count == 0
                    ? MakeCell("—")
                    : MakeCell(payment.Allocations.Select(a => $"• {a.SourceDescription}: {MoneyInput.FormatDinars(a.AmountCentimes)}").ToList()));
                row.Cells.Add(MakeCell(payment.Note ?? "—"));
                payments.RowGroups[0].Rows.Add(row);
            }
            doc.Blocks.Add(payments);
        }

        return doc;
    }

    // ═══ تقرير جدولي A4 عام (6.4 — ق-6): أوزان الأعمدة النسبية تُحوَّل هنا إلى بكسلات مجموعها = عرض المحتوى تماماً ═══
    public static FlowDocument BuildA4Report(TabularReportPrintModel model, string? logoFullPath)
    {
        var doc = NewDocument(A4Portrait);
        AddSchoolHeader(doc, model.Header, logoFullPath);
        AddSeparator(doc);

        doc.Blocks.Add(new Paragraph(new Run(model.Title))
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2),
        });
        if (!string.IsNullOrWhiteSpace(model.SubtitleLine))
            doc.Blocks.Add(new Paragraph(new Run(model.SubtitleLine))
            {
                FontSize = 11,
                Foreground = Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
            });
        if (!string.IsNullOrWhiteSpace(model.TotalsLine))
            doc.Blocks.Add(new Paragraph(new Run(model.TotalsLine))
            {
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
            });

        if (model.Columns.Count == 0 || model.Rows.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("لا بيانات")) { Foreground = Gray, TextAlignment = TextAlignment.Center });
            return doc;
        }

        // أوزان نسبية ← بكسلات مطلقة مجموعها = A4ContentWidth تماماً — الأخير يمتص كسر التقريب (لا Auto/Star إطلاقاً)
        var weights = model.Columns.Select(c => c.Weight > 0 ? c.Weight : 1.0).ToArray();
        var totalWeight = weights.Sum();
        var widths = new double[weights.Length];
        var accumulated = 0.0;
        for (var i = 0; i < weights.Length - 1; i++)
        {
            widths[i] = Math.Round(A4ContentWidth * weights[i] / totalWeight, 1);
            accumulated += widths[i];
        }
        widths[^1] = A4ContentWidth - accumulated;

        var table = DataTable(model.Columns.Select(c => c.Header).ToList(), widths);
        foreach (var row in model.Rows)
            AddRow(table, row.ToArray());
        doc.Blocks.Add(table);

        return doc;
    }

    // ═══ اللبنات المشتركة ═══

    private static FlowDocument NewDocument(Size pageSize) => new()
    {
        FlowDirection = FlowDirection.RightToLeft,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12,
        Foreground = Ink,
        PageWidth = pageSize.Width,
        PageHeight = pageSize.Height,
        PagePadding = new Thickness(36),
        ColumnGap = 0,
        ColumnWidth = pageSize.Width,   // عمود واحد بعرض الصفحة والحواشي من PagePadding — وصفة طباعة FlowDocument القياسية
        TextAlignment = TextAlignment.Right,
    };

    /// <summary>ترويسة هوية المدرسة (ط-7): الاسم (ساقط على «EduMaster» عبر DisplayName — D-131) + هاتف/عنوان + اللوغو إن وُجد</summary>
    private static void AddSchoolHeader(FlowDocument doc, PrintHeader header, string? logoFullPath)
    {
        var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel();
        texts.Children.Add(new TextBlock
        {
            Text = header.SchoolName,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Ink,
        });
        var contact = string.Join(" · ", new[] { header.SchoolPhone, header.SchoolAddress }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (contact.Length > 0)
            texts.Children.Add(new TextBlock { Text = contact, FontSize = 10, Foreground = Gray, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(texts);

        var logo = TryLoadLogo(logoFullPath);
        if (logo is not null)
        {
            Grid.SetColumn(logo, 1);
            grid.Children.Add(logo);
        }

        doc.Blocks.Add(new BlockUIContainer(grid));
    }

    /// <summary>اللوغو زينة لا شرط — ملف غائب أو تالف لا يُسقط مطبوعاً أبداً</summary>
    private static UIElement? TryLoadLogo(string? logoFullPath)
    {
        if (string.IsNullOrWhiteSpace(logoFullPath) || !File.Exists(logoFullPath))
            return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;   // يحرر قفل الملف فوراً
            bitmap.UriSource = new Uri(logoFullPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return new Image { Source = bitmap, Width = 56, Height = 56, Margin = new Thickness(10, 0, 0, 0) };
        }
        catch
        {
            return null;
        }
    }

    private static void AddSeparator(FlowDocument doc)
        => doc.Blocks.Add(new BlockUIContainer(new Border { Height = 1, Background = LineColor, Margin = new Thickness(0, 8, 0, 0) }));

    private static Paragraph SectionTitle(string text)
        => new(new Run(text)) { FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 4) };

    private static Paragraph InfoLine(string label, string value)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        paragraph.Inlines.Add(new Run($"{label} ") { Foreground = Gray, FontSize = 11 });
        paragraph.Inlines.Add(new Run(value) { FontWeight = FontWeights.SemiBold });
        return paragraph;
    }

    /// <summary>جدول بأعمدة بكسلات مطلقة مجموعها = عرض المحتوى تماماً — لا Auto/Star إطلاقاً (انهيار القياس عند الطباعة) · المطلق في WPF اسمه GridUnitType.Pixel — لا وجود لـ«Absolute» (عائلة D-95)</summary>
    private static Table DataTable(IReadOnlyList<string> headers, IReadOnlyList<double> widths)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 2, 0, 6) };
        foreach (var width in widths)
            table.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Pixel) });

        var headerRow = new TableRow { Background = HeaderBg };
        foreach (var header in headers)
            headerRow.Cells.Add(MakeCell(header, bold: true));

        var group = new TableRowGroup();
        group.Rows.Add(headerRow);
        table.RowGroups.Add(group);
        return table;
    }

    private static void AddRow(Table table, params string[] cells)
    {
        var row = new TableRow();
        foreach (var cell in cells)
            row.Cells.Add(MakeCell(cell));
        table.RowGroups[0].Rows.Add(row);
    }

    private static TableCell MakeCell(string text, bool bold = false)
        => MakeCell(new[] { text }, bold);

    private static TableCell MakeCell(IReadOnlyList<string> lines, bool bold = false)
    {
        var cell = new TableCell
        {
            Padding = new Thickness(5, 3, 5, 3),
            BorderBrush = LineColor,
            BorderThickness = new Thickness(0, 0, 0, 0.75),
        };
        foreach (var line in lines)
            cell.Blocks.Add(new Paragraph(new Run(line))
            {
                FontSize = 11,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Margin = new Thickness(0),
            });
        return cell;
    }
}
