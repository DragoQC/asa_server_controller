namespace asa_server_controller.Data.Entities;

public sealed class LoginMethodTypeEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<UserLoginMethodEntity> UserLoginMethods { get; set; } = [];
}
