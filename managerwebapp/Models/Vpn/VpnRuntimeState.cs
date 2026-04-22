namespace managerwebapp.Models.Vpn;

public sealed record VpnRuntimeState(
    bool IsInstalled,
    bool IsActive,
    bool IsInstalling,
    string? LastMessage,
    bool LastRunFailed);
