using managerwebapp.Models.Cluster;
using managerwebapp.Services;
using Microsoft.AspNetCore.Mvc;

namespace managerwebapp.Controllers;

[ApiController]
[Route("api/nfs")]
public sealed class NfsController(NfsService nfsService) : ControllerBase
{
    [HttpGet("invite/{inviteKey}")]
    public async Task<IActionResult> GetShareConfig(string inviteKey, CancellationToken cancellationToken)
    {
        try
        {
            NfsShareInviteResponse response = await nfsService.GetShareRequestAsync(inviteKey, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                success = false,
                message = exception.Message
            });
        }
    }
}
