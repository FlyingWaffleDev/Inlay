using Inlay.Models;

namespace Inlay;

internal interface ITemplateDocumentService
{
    Task<TemplateDocument> LoadAsync(Stream stream, CancellationToken cancellationToken = default);
    Task SaveAsync(Stream stream, TemplateDocument document, CancellationToken cancellationToken = default);
}
