using EduMaster.Application.Abstractions;
using EduMaster.Application.Printing;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace EduMaster.UI.Printing;

/// <summary>
/// تنفيذ خدمة الطباعة (6.3 — ط-د/D-130): يحلّ مسار اللوغو عبر IImageStore (D-38)، يستدعي المرسّم النقي،
/// ثم PrintDialog — المستخدم يختار الطابعة أو «Microsoft Print to PDF» — وPrintDocument بمقاس صفحة ثابت.
/// عديمة الحالة (Singleton) · فشل الطباعة يُسجَّل إنجليزياً ويعود Failed — والـVM يقرر الرسالة العربية (D-22/D-24).
/// </summary>
public sealed class PrintService : IPrintService
{
    private readonly IImageStore _imageStore;
    private readonly ILogger<PrintService> _logger;

    public PrintService(IImageStore imageStore, ILogger<PrintService> logger)
    {
        _imageStore = imageStore;
        _logger = logger;
    }

    public PrintOutcome PrintReceipt(ReceiptPrintModel model)
        => PrintCore(
            PrintDocumentBuilder.BuildReceipt(model, ResolveLogo(model.Header.LogoPath)),
            PrintDocumentBuilder.A5Portrait,
            $"EduMaster — {model.DocumentTitle} {model.ReceiptNoText}");

    public PrintOutcome PrintPaymentMovement(PaymentMovementPrintModel model)
        => PrintCore(
            PrintDocumentBuilder.BuildPaymentMovement(model, ResolveLogo(model.Header.LogoPath)),
            PrintDocumentBuilder.A4Portrait,
            $"EduMaster — تقرير حركة القبض {model.Report.From:yyyy-MM-dd}…{model.Report.To:yyyy-MM-dd}");

    public PrintOutcome PrintStudentStatement(StudentStatementPrintModel model)
        => PrintCore(
            PrintDocumentBuilder.BuildStudentStatement(model, ResolveLogo(model.Header.LogoPath)),
            PrintDocumentBuilder.A4Portrait,
            $"EduMaster — كشف حساب {model.Student.FullName}");

    public PrintOutcome PrintA4Report(TabularReportPrintModel model)
        => PrintCore(
            PrintDocumentBuilder.BuildA4Report(model, ResolveLogo(model.Header.LogoPath)),
            PrintDocumentBuilder.A4Portrait,
            $"EduMaster — {model.Title}");

    /// <summary>اسم اللوغو المخزَّن ← مسار كامل (D-38) · الفشل هنا لا يمنع الطباعة — اللوغو زينة لا شرط</summary>
    private string? ResolveLogo(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            return null;
        try
        {
            return _imageStore.GetFullPath(storedFileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve school logo path {StoredFileName} — printing without logo", storedFileName);
            return null;
        }
    }

    private PrintOutcome PrintCore(FlowDocument document, Size pageSize, string jobName)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
                return PrintOutcome.Cancelled;   // إلغاء المستخدم من نافذة الطباعة ليس خطأً (روح D-64)

            var paginator = new FixedPageSizePaginator(((IDocumentPaginatorSource)document).DocumentPaginator, pageSize);
            dialog.PrintDocument(paginator, jobName);
            return PrintOutcome.Printed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print failed for job {JobName}", jobName);
            return PrintOutcome.Failed;
        }
    }

    /// <summary>
    /// يفرض مقاس الصفحة المطلوب عند الطباعة — الوثيقة مُجزّأة داخلياً على المقاس نفسه فتتطابق الصفحات.
    /// أعضاء الغلاف الأربعة بتواقيع DocumentPaginator الحقيقية (عائلة D-95 — تُفحص لا تُفترض):
    /// GetPage ← DocumentPage · Source ← IDocumentPaginatorSource
    /// </summary>
    private sealed class FixedPageSizePaginator : DocumentPaginator
    {
        private readonly DocumentPaginator _inner;
        private readonly Size _pageSize;

        public FixedPageSizePaginator(DocumentPaginator inner, Size pageSize)
        {
            _inner = inner;
            _pageSize = pageSize;
        }

        public override bool IsPageCountValid => _inner.IsPageCountValid;
        public override int PageCount => _inner.PageCount;
        public override Size PageSize { get => _pageSize; set { /* ثابت — لا يُضبط من الطابعة */ } }
        public override DocumentPage GetPage(int pageNumber) => _inner.GetPage(pageNumber);
        public override IDocumentPaginatorSource Source => _inner.Source;
    }
}
