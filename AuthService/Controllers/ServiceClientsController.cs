using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using LastMile.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Auth.Controllers;

/// <summary>Admin management of M2M service accounts.</summary>
[ApiController]
[Route("api/v1/service-clients")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class ServiceClientsController(ServiceClientService clients) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ServiceClientDto>> List(CancellationToken ct) => clients.ListAsync(ct);

    /// <summary>Create a service client. The generated client_secret is returned only once.</summary>
    [HttpPost]
    [ProducesResponseType<ServiceClientCredentialsDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateServiceClientDto dto, CancellationToken ct)
    {
        var created = await clients.CreateAsync(dto, User, ct);
        return Created($"/api/v1/service-clients/{created.Client.Id}", created);
    }

    [HttpPost("{id:guid}/rotate-secret")]
    public Task<ServiceClientCredentialsDto> RotateSecret(Guid id, CancellationToken ct) =>
        clients.RotateSecretAsync(id, User, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await clients.DeleteAsync(id, User, ct);
        return NoContent();
    }
}
