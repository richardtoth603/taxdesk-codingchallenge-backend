using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Services;

/// <summary>
/// Renders the VAT declaration summary report as a PDF using QuestPDF.
/// All content is placed as text via QuestPDF's document model (no HTML/script
/// evaluation involved), so there is no injection risk from source-file content.
/// </summary>
public class PdfReportService : IPdfReportService
{
    private static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

    public byte[] GeneratePdf(VatDeclarationReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("ÁFA bevallás összesítő riport").FontSize(18).Bold();
                    col.Item().Text("VAT Declaration Summary Report").FontSize(11).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Forrásfájl / Source file: {report.SourceFileName}");
                            c.Item().Text($"Riport azonosító / Report ID: {report.ReportId}");
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Készült / Generated: {report.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC");
                            if (report.PeriodStart is not null && report.PeriodEnd is not null)
                            {
                                c.Item().Text($"Időszak / Period: {report.PeriodStart} – {report.PeriodEnd}");
                            }
                            c.Item().Text($"Tételek száma / Transactions: {report.TotalTransactionCount}");
                        });
                    });

                    col.Item().PaddingTop(8).Text("Kategória szerinti bontás / Breakdown by VAT category")
                        .FontSize(13).Bold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("ÁFA kulcs\nVAT rate");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Db\nCount");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Nettó (HUF)\nNet");
                            header.Cell().Element(HeaderCell).AlignRight().Text("ÁFA (HUF)\nVAT");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Bruttó (HUF)\nGross");
                        });

                        foreach (var category in report.Categories)
                        {
                            var flag = category.IsStandardHungarianRate ? "" : "  ⚠";
                            table.Cell().Element(BodyCell).Text(category.CategoryLabel + flag);
                            table.Cell().Element(BodyCell).AlignRight().Text(category.TransactionCount.ToString());
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(category.TotalNet));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(category.TotalVat));
                            table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(category.TotalGross));
                        }

                        table.Cell().Element(TotalCell).Text("Mindösszesen / Total");
                        table.Cell().Element(TotalCell).AlignRight().Text(report.TotalTransactionCount.ToString());
                        table.Cell().Element(TotalCell).AlignRight().Text(FormatMoney(report.GrandTotalNet));
                        table.Cell().Element(TotalCell).AlignRight().Text(FormatMoney(report.GrandTotalVat));
                        table.Cell().Element(TotalCell).AlignRight().Text(FormatMoney(report.GrandTotalGross));
                    });

                    if (report.Warnings.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("Figyelmeztetések / Warnings").FontSize(12).Bold();
                        col.Item().Column(c =>
                        {
                            foreach (var warning in report.Warnings.Take(30))
                            {
                                c.Item().Text($"• {warning}").FontSize(9).FontColor(Colors.Orange.Darken2);
                            }
                        });
                    }

                    col.Item().PaddingTop(16).Text(
                        "Megjegyzés: Ez a riport a forrásfájl alapján készült összesítő, tájékoztató jellegű " +
                        "dokumentum, amely a NAV-hoz benyújtandó áfabevallás előkészítését segíti, de önmagában " +
                        "nem minősül hivatalosan benyújtható bevallásnak."
                    ).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by VAT Declaration Processor – ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string FormatMoney(decimal value) => value.ToString("N2", Hu);

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White))
            .Background(Colors.Blue.Darken2).Padding(5);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static IContainer TotalCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).DefaultTextStyle(x => x.Bold()).Padding(5);
}
