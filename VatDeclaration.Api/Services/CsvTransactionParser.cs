using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Services;

/// <summary>
/// Parses the expected source CSV format:
/// InvoiceNumber,IssueDate,PartnerName,NetAmount,VatRate,GrossAmount(optional)
///
/// - IssueDate: yyyy-MM-dd (also accepts dd.MM.yyyy)
/// - NetAmount / GrossAmount: decimal, accepts '.' or ',' as decimal separator
/// - VatRate: accepts fraction (0.27) or percentage (27) notation
///
/// Design notes (security / robustness):
/// - Uses CsvHelper in a mode that never executes formulas or macros (plain data parsing).
/// - Row-level failures are collected as warnings and the row skipped, so a single bad
///   line cannot take down the whole upload.
/// - A hard cap on row count guards against memory-exhaustion / zip-bomb style abuse.
/// - Text fields are length-capped and stripped of characters that are dangerous if the
///   values are later opened in spreadsheet software (CSV/formula injection hardening).
/// </summary>
public class CsvTransactionParser : ICsvTransactionParser
{
    private const int MaxFieldLength = 256;
    private static readonly string[] SupportedDateFormats =
    {
        "yyyy-MM-dd", "yyyy.MM.dd", "dd.MM.yyyy", "dd/MM/yyyy"
    };

    private static readonly HashSet<decimal> StandardHungarianRates = new() { 0.27m, 0.18m, 0.05m, 0m };

    private readonly FileUploadOptions _options;

    public CsvTransactionParser(IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
    }

    public ParseResult Parse(Stream csvStream)
    {
        var result = new ParseResult();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = true,
        };

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            throw new CsvParsingException("The file is empty or does not contain a valid CSV header row.");
        }

        var headers = csv.HeaderRecord.Select(h => h.Trim().ToLowerInvariant()).ToArray();
        string[] required = { "invoicenumber", "issuedate", "partnername", "netamount", "vatrate" };
        var missing = required.Where(r => !headers.Contains(r)).ToList();
        if (missing.Count > 0)
        {
            throw new CsvParsingException(
                $"Missing required column(s): {string.Join(", ", missing)}. " +
                "Expected header: InvoiceNumber,IssueDate,PartnerName,NetAmount,VatRate,GrossAmount(optional)");
        }

        int rowNumber = 1; // header is row 1
        while (csv.Read())
        {
            rowNumber++;

            if (rowNumber - 1 > _options.MaxRowCount)
            {
                throw new CsvParsingException(
                    $"File contains more than the allowed maximum of {_options.MaxRowCount} data rows.");
            }

            try
            {
                var record = ParseRow(csv, rowNumber);
                if (record is not null)
                {
                    result.Records.Add(record);
                }
            }
            catch (Exception ex) when (ex is not CsvParsingException)
            {
                result.Warnings.Add($"Row {rowNumber}: skipped ({ex.Message})");
            }
        }

        if (result.Records.Count == 0)
        {
            throw new CsvParsingException("No valid data rows were found in the uploaded file.");
        }

        return result;
    }

    private TransactionRecord? ParseRow(CsvReader csv, int rowNumber)
    {
        string invoiceNumber = SanitizeText(csv.GetField("InvoiceNumber") ?? string.Empty);
        string partnerName = SanitizeText(csv.GetField("PartnerName") ?? string.Empty);
        string dateRaw = (csv.GetField("IssueDate") ?? string.Empty).Trim();
        string netRaw = (csv.GetField("NetAmount") ?? string.Empty).Trim();
        string vatRateRaw = (csv.GetField("VatRate") ?? string.Empty).Trim();
        string? grossRaw = csv.GetField("GrossAmount")?.Trim();

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new FormatException("InvoiceNumber is required");
        }

        if (!TryParseDate(dateRaw, out var issueDate))
        {
            throw new FormatException($"invalid IssueDate '{dateRaw}'");
        }

        if (!TryParseDecimal(netRaw, out var netAmount))
        {
            throw new FormatException($"invalid NetAmount '{netRaw}'");
        }

        if (!TryParseDecimal(vatRateRaw, out var vatRate))
        {
            throw new FormatException($"invalid VatRate '{vatRateRaw}'");
        }

        // Normalise percentage notation (e.g. "27") into a fraction (0.27).
        if (vatRate > 1m)
        {
            vatRate /= 100m;
        }

        if (vatRate < 0m || vatRate > 1m)
        {
            throw new FormatException($"VatRate out of range: '{vatRateRaw}'");
        }

        if (netAmount < 0m)
        {
            throw new FormatException("NetAmount cannot be negative");
        }

        var vatAmount = Math.Round(netAmount * vatRate, 2, MidpointRounding.AwayFromZero);
        var computedGross = netAmount + vatAmount;

        decimal grossAmount = computedGross;
        if (!string.IsNullOrWhiteSpace(grossRaw) && TryParseDecimal(grossRaw, out var parsedGross))
        {
            // Tolerate small rounding differences from the source system; otherwise trust our own computation.
            grossAmount = Math.Abs(parsedGross - computedGross) <= 1.00m ? parsedGross : computedGross;
        }

        return new TransactionRecord
        {
            RowNumber = rowNumber,
            InvoiceNumber = Truncate(invoiceNumber, MaxFieldLength),
            IssueDate = issueDate,
            PartnerName = Truncate(partnerName, MaxFieldLength),
            NetAmount = netAmount,
            VatRate = vatRate,
            VatAmount = vatAmount,
            GrossAmount = grossAmount,
        };
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        foreach (var format in SupportedDateFormats)
        {
            if (DateOnly.TryParseExact(raw, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }
        }
        return DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        // Accept both '.' and ',' decimal separators, strip thousands separators/whitespace.
        var normalized = raw.Replace(" ", "").Replace("\u00A0", "");
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        normalized = normalized.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Defends against CSV/formula injection if the report is ever re-exported and opened
    /// in spreadsheet software, and strips control characters.
    /// </summary>
    private static string SanitizeText(string input)
    {
        var trimmed = input.Trim();
        trimmed = new string(trimmed.Where(c => !char.IsControl(c)).ToArray());
        if (trimmed.Length > 0 && "=+-@\t\r".Contains(trimmed[0]))
        {
            trimmed = "'" + trimmed;
        }
        return trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
