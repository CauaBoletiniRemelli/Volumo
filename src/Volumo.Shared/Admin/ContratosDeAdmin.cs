using System.ComponentModel.DataAnnotations;

namespace Volumo.Shared.Admin;

public sealed record Suspensao([Required, StringLength(500)] string Motivo);
