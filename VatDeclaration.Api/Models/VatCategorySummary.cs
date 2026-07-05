namespace VatDeclaration.Api.Models;

/// <summary>
/// Aggregated totals for a single VAT rate category (e.g. 27%, 18%, 5%, 0%/AAM).
/// </summary>
public class VatCategorySummary
{
    public decimal VatRate { get; set; }
    public string CategoryLabel { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }
    public bool IsStandardHungarianRate { get; set; }
}
