using asa_server_controller.Models.Invitations;
using asa_server_controller.Services;
using Microsoft.AspNetCore.Mvc;

namespace asa_server_controller.Controllers;

[ApiController]
[Route("api/vpn")]
public sealed class VpnController(InvitationService invitationService) : ControllerBase
{
    [HttpGet("invite/{inviteKey}")]
    public async Task<IActionResult> GetInviteConfig(string inviteKey, CancellationToken cancellationToken)
    {
        try
        {
            InviteRemoteServerRequest request = await invitationService.GetInviteRequestAsync(inviteKey, cancellationToken);
            return Ok(request);
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
