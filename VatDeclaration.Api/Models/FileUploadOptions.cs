namespace VatDeclaration.Api.Models;

public class FileUploadOptions
{
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public int MaxRowCount { get; set; } = 50_000;
    public string[] AllowedExtensions { get; set; } = new[] { ".csv" };
}
