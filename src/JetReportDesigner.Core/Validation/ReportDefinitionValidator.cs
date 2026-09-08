using FluentValidation;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Validation;

/// <summary>
/// Structural validation of a <see cref="ReportDefinition"/> at the API boundary.
/// Covers what the JSON schema cannot express cross-field: layout-mode consistency,
/// unique names, and reference integrity between bands and data sources.
/// </summary>
public sealed class ReportDefinitionValidator : AbstractValidator<ReportDefinition>
{
    private static readonly string[] ValidPageSizes = ["A4", "A5", "Letter", "Legal", "Custom"];

    public ReportDefinitionValidator()
    {
        RuleFor(r => r.SchemaVersion).Equal(1);

        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);

        RuleFor(r => r.Page).NotNull().SetValidator(new PageSetupValidator());

        RuleForEach(r => r.Parameters).SetValidator(new ParameterValidator());
        RuleFor(r => r.Parameters)
            .Must(HaveUniqueNames)
            .WithMessage("Parameter names must be unique.");

        RuleForEach(r => r.DataSources).SetValidator(new DataSourceValidator());
        RuleFor(r => r.DataSources)
            .Must(ds => HaveUniqueNames(ds.Select(d => d.Name)))
            .WithMessage("Data source names must be unique.");

        RuleFor(r => r.Connections)
            .Must(c => HaveUniqueNames(c.Select(x => x.Name)))
            .WithMessage("Connection names must be unique.");

        When(r => r.LayoutMode == LayoutMode.Free, () =>
        {
            RuleFor(r => r.Body)
                .NotNull()
                .WithMessage("A free-layout report must have a body.");
            RuleFor(r => r.Bands)
                .Empty()
                .WithMessage("A free-layout report must not define bands.");
        });

        When(r => r.LayoutMode == LayoutMode.Banded, () =>
        {
            RuleFor(r => r.Bands)
                .NotEmpty()
                .WithMessage("A banded report must define at least one band.");
            RuleFor(r => r.Body)
                .Null()
                .WithMessage("A banded report must not define a free body.");
            RuleFor(r => r)
                .Must(BandDataSourcesExist)
                .WithMessage("Every band data source must match a declared data source.");
        });

        RuleForEach(r => r.Bands).SetValidator(new BandValidator());
        When(r => r.Body is not null, () =>
            RuleForEach(r => r.Body!.Elements).SetValidator(new ElementValidator()));
    }

    private static bool HaveUniqueNames(IEnumerable<ReportParameter> parameters) =>
        HaveUniqueNames(parameters.Select(p => p.Name));

    private static bool HaveUniqueNames(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count == list.Distinct(StringComparer.Ordinal).Count();
    }

    private static bool BandDataSourcesExist(ReportDefinition r)
    {
        var known = r.DataSources.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var band in r.Bands)
        {
            if (band.DataSource is { } ds && !known.Contains(ds))
            {
                return false;
            }

            if (band.Group is { } g && !known.Contains(g.DataSource))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class PageSetupValidator : AbstractValidator<PageSetup>
    {
        public PageSetupValidator()
        {
            RuleFor(p => p.Size).Must(s => ValidPageSizes.Contains(s));
            RuleFor(p => p.Orientation).Must(o => o is "portrait" or "landscape");
            RuleFor(p => p.Columns).Equal(1);
            When(p => p.Size == "Custom", () =>
            {
                RuleFor(p => p.CustomWidth).NotNull().GreaterThan(0);
                RuleFor(p => p.CustomHeight).NotNull().GreaterThan(0);
            });
        }
    }

    private sealed class ParameterValidator : AbstractValidator<ReportParameter>
    {
        public ParameterValidator() =>
            RuleFor(p => p.Name)
                .NotEmpty()
                .Matches("^[A-Za-z_][A-Za-z0-9_]*$")
                .WithMessage("Parameter name must be an identifier.");
    }

    private sealed class DataSourceValidator : AbstractValidator<DataSourceDefinition>
    {
        public DataSourceValidator()
        {
            RuleFor(d => d.Name)
                .NotEmpty()
                .Matches("^[A-Za-z_][A-Za-z0-9_]*$");

            When(d => d.Kind == DataSourceKind.Json, () =>
                RuleFor(d => d.Json).NotNull());
            When(d => d.Kind == DataSourceKind.Rest, () =>
            {
                RuleFor(d => d.Rest).NotNull();
                RuleFor(d => d.Rest!.Url).NotEmpty();
            });
            When(d => d.Kind == DataSourceKind.Sql, () =>
            {
                RuleFor(d => d.Sql).NotNull();
                RuleFor(d => d.Sql!.CommandText).NotEmpty();
                RuleFor(d => d.Sql!.Connection).NotEmpty();
            });
        }
    }

    private sealed class BandValidator : AbstractValidator<Band>
    {
        public BandValidator()
        {
            RuleFor(b => b.Height).GreaterThanOrEqualTo(0);
            When(b => b.Type is BandType.GroupHeader or BandType.GroupFooter, () =>
                RuleFor(b => b.Group).NotNull());
            RuleForEach(b => b.Elements).SetValidator(new ElementValidator());
        }
    }

    private sealed class ElementValidator : AbstractValidator<ReportElement>
    {
        public ElementValidator()
        {
            RuleFor(e => e.Id).NotEmpty();
            RuleFor(e => e.Bounds).NotNull();
            RuleFor(e => e.Bounds.Width).GreaterThanOrEqualTo(0);
            RuleFor(e => e.Bounds.Height).GreaterThanOrEqualTo(0);
            When(e => e.Type == ElementType.Table, () =>
                RuleFor(e => e.Table).NotNull());
        }
    }
}
