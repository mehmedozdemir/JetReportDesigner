using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Turns a <see cref="ElementType.Subreport"/> element into a <see cref="SubreportPrimitive"/>
/// placeholder, with its parameter bindings already resolved against the parent's
/// current row/context. <see cref="ReportRenderService"/> resolves the referenced
/// report and splices its content in afterwards.
/// </summary>
public static class SubreportEmitter
{
    public static SubreportPrimitive? Emit(
        ReportElement element,
        BindingContext context,
        double offsetX,
        double offsetY)
    {
        var subreport = element.Subreport;
        if (subreport is null || string.IsNullOrWhiteSpace(subreport.ReportId))
        {
            return null;
        }

        var b = element.Bounds;
        var parameters = new Dictionary<string, object?>();
        foreach (var (name, expression) in subreport.Parameters)
        {
            parameters[name] = BindingResolver.ResolveValue(expression, null, context);
        }

        return new SubreportPrimitive
        {
            X = b.X + offsetX,
            Y = b.Y + offsetY,
            Width = b.Width,
            Height = b.Height,
            ReportId = subreport.ReportId,
            Parameters = parameters,
        };
    }
}
