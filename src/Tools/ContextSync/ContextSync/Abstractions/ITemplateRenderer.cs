namespace ContextSync.Abstractions;

public interface ITemplateRenderer
{
    string Render(string templateName, object model);
    string RenderText(string templateText, object model);
}
