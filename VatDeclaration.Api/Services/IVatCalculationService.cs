using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Services;

public interface IVatCalculationService
{
    VatDeclarationReport BuildReport(ParseResult parseResult, string sourceFileName);
}
