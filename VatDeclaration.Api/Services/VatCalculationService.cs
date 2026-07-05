using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Services;

/// <summary>
/// Groups parsed transactions by VAT rate and produces the summary report,
/// mirroring the category breakdown used when preparing a Hungarian ÁFA (VAT) return
/// (standard rates 27%, 18%, 5%, and 0%/exempt).
/// </summary>
public class VatCalculationService : IVatCalculationService
{
    private static readonly HashSet<decimal> StandardHungarianRates = new() { 0.27m, 0.18m, 0.05m, 0m };

    public VatDeclarationReport BuildReport(ParseResult parseResult, string sourceFileName)
    {
        var records = parseResult.Records;

        var categories = records
            .GroupBy(r => r.VatRate)
            .OrderByDescending(g => g.Key)
            .Select(g => new VatCategorySummary
            {
                VatRate = g.Key,
                CategoryLabel = FormatCategoryLabel(g.Key),
                TransactionCount = g.Count(),
                TotalNet = Math.Round(g.Sum(r => r.NetAmount), 2, MidpointRounding.AwayFromZero),
                TotalVat = Math.Round(g.Sum(r => r.VatAmount), 2, MidpointRounding.AwayFromZero),
                TotalGross = Math.Round(g.Sum(r => r.GrossAmount), 2, MidpointRounding.AwayFromZero),
                IsStandardHungarianRate = StandardHungarianRates.Contains(g.Key),
            })
            .ToList();

        var warnings = new List<string>(parseResult.Warnings);
        foreach (var category in categories.Where(c => !c.IsStandardHungarianRate))
        {
            warnings.Add(
                $"VAT rate {category.CategoryLabel} is not a standard Hungarian rate (27%/18%/5%/0%). " +
                "Please double-check these transactions before filing.");
        }

        var report = new VatDeclarationReport
        {
            SourceFileName = sourceFileName,
            Categories = categories,
            TotalTransactionCount = records.Count,
            GrandTotalNet = Math.Round(records.Sum(r => r.NetAmount), 2, MidpointRounding.AwayFromZero),
            GrandTotalVat = Math.Round(records.Sum(r => r.VatAmount), 2, MidpointRounding.AwayFromZero),
            GrandTotalGross = Math.Round(records.Sum(r => r.GrossAmount), 2, MidpointRounding.AwayFromZero),
            Warnings = warnings,
        };

        if (records.Count > 0)
        {
            report.PeriodStart = records.Min(r => r.IssueDate);
            report.PeriodEnd = records.Max(r => r.IssueDate);
        }

        return report;
    }

    private static string FormatCategoryLabel(decimal rate)
    {
        if (rate == 0m)
        {
            return "0% (AAM / exempt)";
        }
        return $"{rate * 100m:0.##}%";
    }
}
