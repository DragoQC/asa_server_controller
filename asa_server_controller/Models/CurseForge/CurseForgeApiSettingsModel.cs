using System.ComponentModel.DataAnnotations;

namespace asa_server_controller.Models.CurseForge;

public sealed class CurseForgeApiSettingsModel
{
    [StringLength(512)]
    public string ApiKey { get; set; } = string.Empty;
}
