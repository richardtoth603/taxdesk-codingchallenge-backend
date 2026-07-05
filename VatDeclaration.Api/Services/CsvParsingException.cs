namespace VatDeclaration.Api.Services;

/// <summary>
/// Thrown for structural problems with the uploaded file (missing headers, unreadable
/// content, row-count limits exceeded) as opposed to individual bad data rows, which
/// are collected as warnings instead of aborting the whole upload.
/// </summary>
public class CsvParsingException : Exception
{
    public CsvParsingException(string message) : base(message) { }
}
