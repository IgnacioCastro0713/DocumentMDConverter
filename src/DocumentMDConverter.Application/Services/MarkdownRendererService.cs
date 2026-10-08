using Markdig;
using DocumentMDConverter.Application.Interfaces;

namespace DocumentMDConverter.Application.Services;

public sealed class MarkdownRendererService : IMarkdownRendererService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseBootstrap()
        .Build();

    private readonly MarkdownPipeline _htmlDisabledPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseBootstrap()
        .DisableHtml()
        .Build();

    public string RenderToHtml(string markdown, bool disableHtml = false)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown, disableHtml ? _htmlDisabledPipeline : _pipeline);
    }
}
