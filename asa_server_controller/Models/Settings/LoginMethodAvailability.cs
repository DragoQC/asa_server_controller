namespace asa_server_controller.Models.Settings;

public sealed record LoginMethodAvailability(
    bool PasswordEnabled,
    bool EmailEnabled,
    bool TwoFactorEnabled);
