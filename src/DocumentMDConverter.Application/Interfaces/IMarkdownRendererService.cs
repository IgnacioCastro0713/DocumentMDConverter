namespace DocumentMDConverter.Application.Interfaces;

public interface IMarkdownRendererService
{
    string RenderToHtml(string markdown, bool disableHtml = false);
}
