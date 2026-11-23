using Odontia.Application.Common;
using Odontia.Application.Features.Dashboard;
using Odontia.Application.Features.Documents;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Notifications;
using Odontia.Domain.Enums;

namespace Odontia.Api.Controllers;

/// <summary>Formulario de carga de documento (multipart/form-data).</summary>
public sealed class UploadDocumentForm
{
    public IFormFile File { get; set; } = default!;
    public DocumentCategory Category { get; set; }
    public string? Description { get; set; }
    public Guid? ClinicalEntryId { get; set; }
    public Guid? TreatmentPlanId { get; set; }
}
