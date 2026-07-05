namespace VatDeclaration.Api.Models;

/// <summary>
/// A single parsed and validated invoice/transaction line from the source file.
/// </summary>
public class TransactionRecord
{
    public int RowNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public decimal NetAmount { get; set; }

    /// <summary>VAT rate expressed as a fraction, e.g. 0.27 for 27%.</summary>
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
}
