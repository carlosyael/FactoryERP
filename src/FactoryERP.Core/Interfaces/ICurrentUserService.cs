namespace FactoryERP.Core.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
}
