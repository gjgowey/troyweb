using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace TroyWebProperty.Web.TagHelpers;

[HtmlTargetElement("input", Attributes = "asp-for,class")]
[HtmlTargetElement("select", Attributes = "asp-for,class")]
[HtmlTargetElement("textarea", Attributes = "asp-for,class")]
public sealed class FieldAccessibilityTagHelper : TagHelper
{

    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }
    [ViewContext] [HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (context.AllAttributes.ContainsName("data-no-field-validation"))
        {
            return;
        }

        var classes = context.AllAttributes["class"]?.Value?.ToString()?.Split(' ') ?? [];

        if (!classes.Contains("form-control") && !classes.Contains("form-select"))
        {
            return;
        }

        var type = output.Attributes["type"]?.Value?.ToString();

        if (type is "hidden" or "checkbox" or "radio")
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(For);
        ArgumentNullException.ThrowIfNull(ViewContext);
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        var id = TagBuilder.CreateSanitizedId(name, "_") + "-validation";
        var descriptions = output.Attributes["aria-describedby"]?.Value?.ToString()
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        output.Attributes.SetAttribute("aria-describedby", string.Join(" ", descriptions.Append(id).Distinct()));

        if (ViewContext.ViewData.ModelState.TryGetValue(name, out var state) && state.Errors.Count > 0)
        {
            output.Attributes.SetAttribute("aria-invalid", "true");
        }
    }

}

[HtmlTargetElement("span", Attributes = "asp-validation-for")]
public sealed class ValidationMessageAccessibilityTagHelper : TagHelper
{

    [HtmlAttributeName("asp-validation-for")]
    public ModelExpression? For { get; set; }

    [ViewContext] [HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(For);
        ArgumentNullException.ThrowIfNull(ViewContext);
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        output.Attributes.SetAttribute("id", TagBuilder.CreateSanitizedId(name, "_") + "-validation");
    }

}