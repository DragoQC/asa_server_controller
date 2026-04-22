namespace asa_server_controller.Data.Entities;

public sealed class UserLoginMethodEntity : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int LoginMethodTypeId { get; set; }
    public LoginMethodTypeEntity LoginMethodType { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
}
