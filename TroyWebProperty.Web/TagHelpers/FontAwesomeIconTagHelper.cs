using Microsoft.AspNetCore.Razor.TagHelpers;

namespace TroyWebProperty.Web.TagHelpers;

/// <summary>Decorative Font Awesome Free solid icons; retain visible labels.</summary>
[HtmlTargetElement("fa-icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class FontAwesomeIconTagHelper : TagHelper
{

    public string Name { get; set; } = "";
    public bool Trailing { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "i";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.RemoveAll("name");
        output.Attributes.RemoveAll("trailing");
        output.Attributes.SetAttribute("class",
            "fa-solid fa-" + Name + " fa-icon" + (Trailing ? " icon-trailing" : ""));
        output.Attributes.SetAttribute("aria-hidden", "true");
    }

}