using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Services;

public interface IPdfReportService
{
    byte[] GeneratePdf(VatDeclarationReport report);
}
