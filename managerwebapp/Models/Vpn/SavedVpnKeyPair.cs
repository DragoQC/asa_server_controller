namespace managerwebapp.Models.Vpn;

public sealed record SavedVpnKeyPair(
    string Name,
    string PrivateKeyPath,
    string PublicKeyPath,
    string? PrivateKey,
    string? PublicKey,
    bool Exists);
