using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;

namespace JetReportDesigner.Rendering.Layout;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>
/// Builds a paginated <see cref="RenderDocument"/> from a banded report: a repeating
/// detail band, one grouping level with header/footer, aggregates at group/page/report
/// scope, and page header/footer with page numbers. Bands are fixed height (no
/// auto-grow); a band that does not fit forces a page break.
/// </summary>
public sealed class BandedLayoutBuilder
{
    private sealed record BandInstance(Band Band, double Y, Row? Row, IReadOnlyList<Row>? AggregateRows)
    {
        /// <summary>0-based index of this detail row within all rows; -1 for non-detail bands.</summary>
        public int RowIndex { get; init; } = -1;
    }

    public RenderDocument Build(
        ReportDefinition report,
        ReportData data,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (report.LayoutMode != LayoutMode.Banded)
        {
            throw new InvalidOperationException("BandedLayoutBuilder requires a banded report.");
        }

        var (pageWidth, pageHeight) = PageGeometry.Resolve(report.Page);
        var margins = report.Page.Margins;
        var now = DateTime.Now;

        var bands = report.Bands.Where(b => b.Visible).ToList();
        Band? Band(BandType type) => bands.FirstOrDefault(b => b.Type == type);
        var reportHeader = Band(BandType.ReportHeader);
        var pageHeader = Band(BandType.PageHeader);
        var groupHeader = Band(BandType.GroupHeader);
        var detail = Band(BandType.Detail);
        var groupFooter = Band(BandType.GroupFooter);
        var pageFooter = Band(BandType.PageFooter);
        var reportFooter = Band(BandType.ReportFooter);

        static double H(Band? b) => b?.Height ?? 0;

        var detailSource = detail?.DataSource ?? report.DataSources.FirstOrDefault()?.Name ?? string.Empty;
        var allRows = data.Get(detailSource).Rows.ToList();

        var groupSpec = groupHeader?.Group ?? groupFooter?.Group;
        var grouping = groupSpec is not null
            && !string.IsNullOrWhiteSpace(groupSpec.Expression)
            && (groupHeader is not null || groupFooter is not null);

        var culture = CultureResolver.Resolve(report.Culture);
        var baseContext = new BindingContext(null, parameters) { Now = now, Culture = culture };

        if (grouping)
        {
            var descending = groupSpec!.Sort.Equals("desc", StringComparison.OrdinalIgnoreCase);
            var comparer = Comparer<object?>.Create(CompareKeys);
            allRows = (descending
                    ? allRows.OrderByDescending(r => GroupKey(groupSpec.Expression, r), comparer)
                    : allRows.OrderBy(r => GroupKey(groupSpec.Expression, r), comparer))
                .ToList();
        }

        object? GroupKey(string expression, Row row) =>
            BindingResolver.ResolveGroupKey(expression, baseContext.WithRow(row));

        // ---- layout pass ----
        var pages = new List<List<BandInstance>>();
        List<BandInstance> page = [];
        var pageRows = new List<Row>();
        var y = margins.Top;
        double UsableBottom() => pageHeight - margins.Bottom - H(pageFooter);

        void ClosePage()
        {
            if (pageFooter is not null)
            {
                page.Add(new BandInstance(pageFooter, pageHeight - margins.Bottom - H(pageFooter), null, pageRows.ToList()));
            }
        }

        void BeginPage(bool first, Row? repeatGroupRow)
        {
            if (pages.Count > 0)
            {
                ClosePage();
            }

            page = [];
            pages.Add(page);
            pageRows = [];
            y = margins.Top;

            if (first && reportHeader is not null)
            {
                page.Add(new BandInstance(reportHeader, y, null, allRows));
                y += H(reportHeader);
            }

            if (pageHeader is not null)
            {
                page.Add(new BandInstance(pageHeader, y, null, null));
                y += H(pageHeader);
            }

            if (!first && grouping && groupHeader is { RepeatOnEveryPage: true } && repeatGroupRow is not null)
            {
                page.Add(new BandInstance(groupHeader, y, repeatGroupRow, null));
                y += H(groupHeader);
            }
        }

        void Ensure(double height, Row? currentGroupRow)
        {
            if (y + height > UsableBottom())
            {
                BeginPage(false, currentGroupRow);
            }
        }

        BeginPage(first: true, repeatGroupRow: null);

        object? previousKey = null;
        Row? currentGroupRow = null;
        var groupRows = new List<Row>();

        for (var i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];

            if (grouping)
            {
                var key = GroupKey(groupSpec!.Expression, row);
                if (i == 0 || !KeyEquals(key, previousKey))
                {
                    if (i != 0 && groupFooter is not null)
                    {
                        Ensure(H(groupFooter), currentGroupRow);
                        page.Add(new BandInstance(groupFooter, y, currentGroupRow, groupRows.ToList()));
                        y += H(groupFooter);
                    }

                    Ensure(H(groupHeader) + H(detail), row);
                    currentGroupRow = row;
                    if (groupHeader is not null)
                    {
                        page.Add(new BandInstance(groupHeader, y, row, null));
                        y += H(groupHeader);
                    }

                    groupRows = [];
                    previousKey = key;
                }
            }

            Ensure(H(detail), currentGroupRow);
            if (detail is not null)
            {
                page.Add(new BandInstance(detail, y, row, null) { RowIndex = i });
                y += H(detail);
            }

            groupRows.Add(row);
            pageRows.Add(row);
        }

        if (grouping && groupFooter is not null && allRows.Count > 0)
        {
            Ensure(H(groupFooter), currentGroupRow);
            page.Add(new BandInstance(groupFooter, y, currentGroupRow, groupRows.ToList()));
            y += H(groupFooter);
        }

        if (reportFooter is not null)
        {
            Ensure(H(reportFooter), currentGroupRow);
            page.Add(new BandInstance(reportFooter, y, null, allRows));
            y += H(reportFooter);
        }

        ClosePage();

        // Per-row group membership, so a group header/footer can aggregate its own rows.
        var rowToGroup = new Dictionary<Row, IReadOnlyList<Row>>(ReferenceEqualityComparer.Instance);
        if (grouping)
        {
            List<Row> current = [];
            object? prev = null;
            for (var i = 0; i < allRows.Count; i++)
            {
                var key = GroupKey(groupSpec!.Expression, allRows[i]);
                if (i == 0 || !KeyEquals(key, prev))
                {
                    current = [];
                    prev = key;
                }

                current.Add(allRows[i]);
                rowToGroup[allRows[i]] = current;
            }
        }

        // ---- emit pass (totalPages now known) ----
        var totalPages = pages.Count;
        var renderPages = new List<RenderPage>(totalPages);

        for (var p = 0; p < totalPages; p++)
        {
            var primitives = new List<RenderPrimitive>();
            if (report.Page.BackgroundImage is { Source: { Length: > 0 } pageBg } pageBgSpec)
            {
                primitives.Add(new ImagePrimitive
                {
                    X = 0, Y = 0, Width = pageWidth, Height = pageHeight,
                    Source = pageBg, Fit = ElementEmitter.ParseFit(pageBgSpec.Fit),
                });
            }

            var pageDetailRows = pages[p]
                .Where(x => x.Band.Type == BandType.Detail && x.Row is not null)
                .Select(x => x.Row!)
                .ToList();

            foreach (var instance in pages[p])
            {
                IReadOnlyList<Row> scopeRows = instance.Band.Type switch
                {
                    BandType.ReportHeader or BandType.ReportFooter => allRows,
                    BandType.PageHeader or BandType.PageFooter => instance.AggregateRows ?? pageDetailRows,
                    BandType.GroupHeader or BandType.GroupFooter => instance.AggregateRows
                        ?? (instance.Row is { } gr && rowToGroup.TryGetValue(gr, out var g) ? g : allRows),
                    _ => allRows,
                };

                var context = new BindingContext(instance.Row, parameters)
                {
                    PageNumber = p + 1,
                    TotalPages = totalPages,
                    Now = now,
                    Culture = culture,
                    AggregateRows = scopeRows,
                    RowNumber = instance.RowIndex >= 0 ? instance.RowIndex + 1 : 0,
                    TotalRows = scopeRows.Count,
                };

                string? Aggregate(ReportElement element)
                {
                    if (element.Aggregate == AggregateFunction.None || instance.AggregateRows is null)
                    {
                        return null;
                    }

                    var scopeRows = element.AggregateScope switch
                    {
                        AggregateScope.Report => allRows,
                        AggregateScope.Page => instance.AggregateRows,
                        _ => instance.AggregateRows,
                    };
                    var value = AggregateComputer.Compute(element.Aggregate, element.Value, scopeRows);
                    return BindingResolver.FormatValue(value, element.Format, culture);
                }

                if (instance.Band.BackgroundImage is { Source: { Length: > 0 } bandBg } bandBgSpec)
                {
                    primitives.Add(new ImagePrimitive
                    {
                        X = margins.Left,
                        Y = instance.Y,
                        Width = pageWidth - margins.Left - margins.Right,
                        Height = instance.Band.Height,
                        Source = bandBg,
                        Fit = ElementEmitter.ParseFit(bandBgSpec.Fit),
                    });
                }

                // conditional formatting on the band row: paint its background, then
                // cascade the matched styles onto every element in the band.
                var bandRuleStyles = FormatRuleEvaluator.Apply(instance.Band.FormatRules, context).Styles;
                foreach (var ruleStyle in bandRuleStyles)
                {
                    if (ruleStyle.Background is { } bg)
                    {
                        primitives.Add(new RectanglePrimitive
                        {
                            X = margins.Left,
                            Y = instance.Y,
                            Width = pageWidth - margins.Left - margins.Right,
                            Height = instance.Band.Height,
                            FillColorHex = bg,
                            BorderThicknessPx = 0,
                        });
                    }
                }

                foreach (var element in instance.Band.Elements)
                {
                    if (element.Type == ElementType.Table)
                    {
                        var tableRows = data.Get(element.Table?.DataSource ?? detailSource).Rows;
                        primitives.AddRange(
                            TableEmitter.Emit(element, report.Styles, tableRows, context, margins.Left, instance.Y));
                    }
                    else
                    {
                        primitives.AddRange(
                            ElementEmitter.Emit(
                                element, report.Styles, context, margins.Left, instance.Y, Aggregate, bandRuleStyles));
                    }
                }
            }

            renderPages.Add(new RenderPage { Primitives = primitives });
        }

        return new RenderDocument
        {
            PageWidthPx = pageWidth,
            PageHeightPx = pageHeight,
            Pages = renderPages,
        };
    }

    private static bool KeyEquals(object? a, object? b) =>
        (a is null && b is null) || (a is not null && a.Equals(b));

    private static int CompareKeys(object? a, object? b)
    {
        if (a is null && b is null)
        {
            return 0;
        }

        if (a is null)
        {
            return -1;
        }

        if (b is null)
        {
            return 1;
        }

        if (a is IComparable ca && a.GetType() == b.GetType())
        {
            return ca.CompareTo(b);
        }

        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
