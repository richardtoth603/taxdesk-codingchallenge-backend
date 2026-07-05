using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Services;

public interface ICsvTransactionParser
{
    /// <summary>
    /// Parses a CSV stream into transaction records. Row-level errors are collected
    /// as warnings and the offending row is skipped rather than aborting the whole file,
    /// unless the file is structurally invalid (e.g. missing required headers).
    /// </summary>
    ParseResult Parse(Stream csvStream);
}
