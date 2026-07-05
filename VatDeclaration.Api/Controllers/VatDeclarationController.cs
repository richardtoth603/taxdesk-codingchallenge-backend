using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VatDeclaration.Api.Models;
using VatDeclaration.Api.Services;
using VatDeclaration.Api.Validation;

namespace VatDeclaration.Api.Controllers;

[ApiController]
[Route("api/vat")]
[EnableRateLimiting("uploads")]
public class VatDeclarationController : ControllerBase
{
    private readonly ICsvTransactionParser _parser;
    private readonly IVatCalculationService _calculationService;
    private readonly IPdfReportService _pdfReportService;
    private readonly FileUploadValidator _validator;
    private readonly ILogger<VatDeclarationController> _logger;

    public VatDeclarationController(
        ICsvTransactionParser parser,
        IVatCalculationService calculationService,
        IPdfReportService pdfReportService,
        FileUploadValidator validator,
        ILogger<VatDeclarationController> logger)
    {
        _parser = parser;
        _calculationService = calculationService;
        _pdfReportService = pdfReportService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a source file (invoices/transactions CSV) and returns the calculated
    /// VAT declaration summary as JSON.
    /// </summary>
    [HttpPost("process")]
    [RequestSizeLimit(5_242_880)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    public ActionResult<VatDeclarationReport> Process([FromForm] FileUploadRequest request)
    {
        var report = ProcessInternal(request.File, out var error);
        if (report is null)
        {
            return BadRequest(new { error });
        }

        _logger.LogInformation(
            "Processed VAT report {ReportId} from file {FileName} with {Count} transactions",
            report.ReportId, request.File.FileName, report.TotalTransactionCount);

        return Ok(report);
    }

    /// <summary>
    /// Uploads a source file and returns the VAT declaration summary rendered as a PDF.
    /// </summary>
    [HttpPost("process/pdf")]
    [RequestSizeLimit(5_242_880)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    public ActionResult ProcessPdf([FromForm] FileUploadRequest request)
    {
        var report = ProcessInternal(request.File, out var error);
        if (report is null)
        {
            return BadRequest(new { error });
        }

        var pdfBytes = _pdfReportService.GeneratePdf(report);
        var safeFileName = $"afa-bevallas-osszesito-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";

        _logger.LogInformation("Generated PDF for VAT report {ReportId}", report.ReportId);

        return File(pdfBytes, "application/pdf", safeFileName);
    }

    private VatDeclarationReport? ProcessInternal(IFormFile? file, out string? error)
    {
        var (isValid, validationError) = _validator.Validate(file);
        if (!isValid)
        {
            error = validationError;
            return null;
        }

        using var stream = file!.OpenReadStream();
        var parseResult = _parser.Parse(stream);
        var report = _calculationService.BuildReport(parseResult, file.FileName);
        error = null;
        return report;
    }
}

public class FileUploadRequest
{
    public IFormFile File { get; set; } = null!;
}
