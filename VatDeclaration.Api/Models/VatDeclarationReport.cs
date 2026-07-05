namespace VatDeclaration.Api.Models;

/// <summary>
/// The complete summarised AFA (VAT) declaration report produced from an uploaded source file.
/// This is a summary report to support preparation of the official NAV VAT return;
/// it is not itself an officially submittable NAV form.
/// </summary>
public class VatDeclarationReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    public List<VatCategorySummary> Categories { get; set; } = new();

    public int TotalTransactionCount { get; set; }
    public decimal GrandTotalNet { get; set; }
    public decimal GrandTotalVat { get; set; }
    public decimal GrandTotalGross { get; set; }

    /// <summary>Non-fatal issues encountered while parsing (skipped/adjusted rows).</summary>
    public List<string> Warnings { get; set; } = new();

    public string SourceFileName { get; set; } = string.Empty;
}
