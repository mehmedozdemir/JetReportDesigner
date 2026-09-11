using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;

namespace JetReportDesigner.Rendering.Layout;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>
/// Builds a paginated <see cref="RenderDocument"/> from a banded report: a repeating
/// detail band, any number of nested grouping levels (each with its own header/footer
/// pair), aggregates at group/page/report scope, and page header/footer with page
/// numbers. Bands are fixed height (no auto-grow); a band that does not fit forces a
/// page break.
/// </summary>
public sealed class BandedLayoutBuilder
{
    private sealed record BandInstance(Band Band, double Y, Row? Row, IReadOnlyList<Row>? AggregateRows)
    {
        /// <summary>0-based index of this detail row within all rows; -1 for non-detail bands.</summary>
        public int RowIndex { get; init; } = -1;

        /// <summary>Nesting level for a group header/footer instance; -1 for every other band.</summary>
        public int GroupLevel { get; init; } = -1;

        /// <summary>
        /// The height actually used for this instance — the band's own configured
        /// height, unless a "can grow" element (currently: a detail row) needed more.
        /// </summary>
        public double EffectiveHeight { get; init; } = Band.Height;
    }

    /// <summary>One nesting level: its header/footer bands (either may be absent) and the shared grouping key.</summary>
    private sealed record GroupLevel(int Index, Band? Header, Band? Footer, GroupSpec Spec);

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
        var detail = Band(BandType.Detail);
        var pageFooter = Band(BandType.PageFooter);
        var reportFooter = Band(BandType.ReportFooter);

        static double H(Band? b) => b?.Height ?? 0;

        var detailSource = detail?.DataSource ?? report.DataSources.FirstOrDefault()?.Name ?? string.Empty;
        var allRows = data.Get(detailSource).Rows.ToList();

        var culture = CultureResolver.Resolve(report.Culture);
        var baseContext = new BindingContext(null, parameters) { Now = now, Culture = culture };

        // Every group header/footer band, paired up by GroupLevel (0 = outermost); a
        // level only counts as "grouping" when it has an expression to group by.
        var headerBands = bands.Where(b => b.Type == BandType.GroupHeader).OrderBy(b => b.GroupLevel).ToList();
        var footerBands = bands.Where(b => b.Type == BandType.GroupFooter).OrderBy(b => b.GroupLevel).ToList();
        var levels = headerBands.Select(b => b.GroupLevel)
            .Concat(footerBands.Select(b => b.GroupLevel))
            .Distinct()
            .OrderBy(l => l)
            .Select(l =>
            {
                var header = headerBands.FirstOrDefault(b => b.GroupLevel == l);
                var footer = footerBands.FirstOrDefault(b => b.GroupLevel == l);
                return new GroupLevel(l, header, footer, header?.Group ?? footer?.Group!);
            })
            .Where(l => l.Spec is { Expression.Length: > 0 })
            .ToList();

        var grouping = levels.Count > 0;

        object? GroupKey(string expression, Row row) =>
            BindingResolver.ResolveGroupKey(expression, baseContext.WithRow(row));

        if (grouping)
        {
            var comparer = Comparer<object?>.Create(CompareKeys);
            IOrderedEnumerable<Row>? ordered = null;
            foreach (var level in levels)
            {
                var expression = level.Spec.Expression;
                var descending = level.Spec.Sort.Equals("desc", StringComparison.OrdinalIgnoreCase);
                object? Key(Row r) => GroupKey(expression, r);
                ordered = ordered is null
                    ? (descending ? allRows.OrderByDescending(Key, comparer) : allRows.OrderBy(Key, comparer))
                    : (descending ? ordered.ThenByDescending(Key, comparer) : ordered.ThenBy(Key, comparer));
            }

            allRows = ordered!.ToList();
        }

        // ---- layout pass ----
        var pages = new List<List<BandInstance>>();
        List<BandInstance> page = [];
        var pageRows = new List<Row>();
        var y = margins.Top;
        double UsableBottom() => pageHeight - margins.Bottom - H(pageFooter);

        var previousKeys = new object?[levels.Count];
        var currentGroupRowByLevel = new Row?[levels.Count];
        var groupRowsByLevel = new List<Row>[levels.Count];
        for (var i = 0; i < levels.Count; i++)
        {
            groupRowsByLevel[i] = [];
        }

        var hasOpenGroup = false;

        void ClosePage()
        {
            if (pageFooter is not null)
            {
                page.Add(new BandInstance(pageFooter, pageHeight - margins.Bottom - H(pageFooter), null, pageRows.ToList()));
            }
        }

        void BeginPage(bool first)
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

            if (!first && grouping && hasOpenGroup)
            {
                for (var lvl = 0; lvl < levels.Count; lvl++)
                {
                    if (levels[lvl].Header is { RepeatOnEveryPage: true } header && currentGroupRowByLevel[lvl] is { } repeatRow)
                    {
                        page.Add(new BandInstance(header, y, repeatRow, null) { GroupLevel = lvl });
                        y += H(header);
                    }
                }
            }
        }

        void Ensure(double height)
        {
            if (y + height > UsableBottom())
            {
                BeginPage(false);
            }
        }

        // The tallest "can grow" element in the detail band, for this row's own data —
        // a fixed-height report never measures anything (MeasuredHeight is a no-op then).
        double DetailRowHeight(Band? band, Row row)
        {
            if (band is null || band.Elements.Count == 0)
            {
                return H(band);
            }

            var rowContext = baseContext.WithRow(row);
            var height = band.Height;
            foreach (var element in band.Elements)
            {
                height = Math.Max(height, ElementEmitter.MeasuredHeight(element, report.Styles, rowContext));
            }

            return height;
        }

        BeginPage(first: true);

        for (var i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];

            if (grouping)
            {
                var currentKeys = new object?[levels.Count];
                for (var lvl = 0; lvl < levels.Count; lvl++)
                {
                    currentKeys[lvl] = GroupKey(levels[lvl].Spec.Expression, row);
                }

                var changedAt = -1;
                if (!hasOpenGroup)
                {
                    changedAt = 0;
                }
                else
                {
                    for (var lvl = 0; lvl < levels.Count; lvl++)
                    {
                        if (!KeyEquals(currentKeys[lvl], previousKeys[lvl]))
                        {
                            changedAt = lvl;
                            break;
                        }
                    }
                }

                if (changedAt >= 0)
                {
                    // Close the innermost open groups first, down to the level that changed.
                    for (var lvl = levels.Count - 1; lvl >= changedAt; lvl--)
                    {
                        if (hasOpenGroup && levels[lvl].Footer is { } footer)
                        {
                            Ensure(H(footer));
                            page.Add(new BandInstance(footer, y, currentGroupRowByLevel[lvl], groupRowsByLevel[lvl].ToList()) { GroupLevel = lvl });
                            y += H(footer);
                        }

                        groupRowsByLevel[lvl] = [];
                    }

                    // Open the newly-started levels, outermost first, keeping their headers
                    // (down to the detail band) together against a page break.
                    double headersHeight = 0;
                    for (var lvl = changedAt; lvl < levels.Count; lvl++)
                    {
                        headersHeight += H(levels[lvl].Header);
                    }

                    Ensure(headersHeight + H(detail));

                    for (var lvl = changedAt; lvl < levels.Count; lvl++)
                    {
                        currentGroupRowByLevel[lvl] = row;
                        if (levels[lvl].Header is { } header)
                        {
                            page.Add(new BandInstance(header, y, row, null) { GroupLevel = lvl });
                            y += H(header);
                        }
                    }

                    previousKeys = currentKeys;
                    hasOpenGroup = true;
                }
            }

            var detailHeight = DetailRowHeight(detail, row);
            Ensure(detailHeight);
            if (detail is not null)
            {
                page.Add(new BandInstance(detail, y, row, null) { RowIndex = i, EffectiveHeight = detailHeight });
                y += detailHeight;
            }

            for (var lvl = 0; lvl < levels.Count; lvl++)
            {
                groupRowsByLevel[lvl].Add(row);
            }

            pageRows.Add(row);
        }

        if (grouping && allRows.Count > 0)
        {
            for (var lvl = levels.Count - 1; lvl >= 0; lvl--)
            {
                if (levels[lvl].Footer is { } footer)
                {
                    Ensure(H(footer));
                    page.Add(new BandInstance(footer, y, currentGroupRowByLevel[lvl], groupRowsByLevel[lvl].ToList()) { GroupLevel = lvl });
                    y += H(footer);
                }
            }
        }

        if (reportFooter is not null)
        {
            Ensure(H(reportFooter));
            page.Add(new BandInstance(reportFooter, y, null, allRows));
            y += H(reportFooter);
        }

        ClosePage();

        // Per-row, per-level group membership, so a header/footer can aggregate its own
        // (possibly not-yet-fully-seen) group — same row list, aliased across every row
        // in the run, growing as the run is walked.
        var rowToGroupByLevel = new Dictionary<Row, IReadOnlyList<Row>>[levels.Count];
        for (var lvl = 0; lvl < levels.Count; lvl++)
        {
            rowToGroupByLevel[lvl] = new Dictionary<Row, IReadOnlyList<Row>>(ReferenceEqualityComparer.Instance);
        }

        if (grouping)
        {
            var prevKeys = new object?[levels.Count];
            var runs = new List<Row>[levels.Count];
            for (var lvl = 0; lvl < levels.Count; lvl++)
            {
                runs[lvl] = [];
            }

            for (var i = 0; i < allRows.Count; i++)
            {
                var row = allRows[i];
                var currentKeys = levels.Select(l => GroupKey(l.Spec.Expression, row)).ToArray();

                var changedAt = i == 0 ? 0 : -1;
                if (i != 0)
                {
                    for (var lvl = 0; lvl < levels.Count; lvl++)
                    {
                        if (!KeyEquals(currentKeys[lvl], prevKeys[lvl]))
                        {
                            changedAt = lvl;
                            break;
                        }
                    }
                }

                if (changedAt >= 0)
                {
                    for (var lvl = changedAt; lvl < levels.Count; lvl++)
                    {
                        runs[lvl] = [];
                    }
                }

                for (var lvl = 0; lvl < levels.Count; lvl++)
                {
                    runs[lvl].Add(row);
                    rowToGroupByLevel[lvl][row] = runs[lvl];
                }

                prevKeys = currentKeys;
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
                        ?? (instance.Row is { } gr && instance.GroupLevel is >= 0 && instance.GroupLevel < rowToGroupByLevel.Length
                            && rowToGroupByLevel[instance.GroupLevel].TryGetValue(gr, out var g) ? g : allRows),
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
                        Height = instance.EffectiveHeight,
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
                            Height = instance.EffectiveHeight,
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
                    else if (element.Type == ElementType.Chart)
                    {
                        var chartSource = string.IsNullOrWhiteSpace(element.Chart?.DataSource)
                            ? detailSource
                            : element.Chart!.DataSource;
                        primitives.AddRange(
                            ChartEmitter.Emit(element, report.Styles, data.Get(chartSource).Rows, context, margins.Left, instance.Y));
                    }
                    else if (element.Type == ElementType.Subreport)
                    {
                        if (SubreportEmitter.Emit(element, context, margins.Left, instance.Y) is { } placeholder)
                        {
                            primitives.Add(placeholder);
                        }
                    }
                    else if (element.Type == ElementType.Matrix)
                    {
                        var matrixSource = string.IsNullOrWhiteSpace(element.Matrix?.DataSource)
                            ? detailSource
                            : element.Matrix!.DataSource;
                        primitives.AddRange(
                            MatrixEmitter.Emit(element, report.Styles, data.Get(matrixSource).Rows, context, margins.Left, instance.Y));
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
