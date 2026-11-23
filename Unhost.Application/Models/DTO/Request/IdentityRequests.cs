using System.ComponentModel.DataAnnotations;

namespace Unhost.Application.Models.DTO.Request;

public sealed record SetupRequest([Required] string Email, [Required] string Password, string? BootstrapSecret);
public sealed record LoginRequest([Required] string Email, [Required] string Password, bool IsPersistent);
