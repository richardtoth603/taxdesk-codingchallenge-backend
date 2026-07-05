namespace VatDeclaration.Api.Models;

/// <summary>
/// Result of parsing an uploaded file: the successfully parsed rows plus any
/// row-level problems that were skipped rather than aborting the whole upload.
/// </summary>
public class ParseResult
{
    public List<TransactionRecord> Records { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
