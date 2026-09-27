namespace FleetManagement.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    string Username,
    string FullName,
    string Role,
    Guid UserId,
    Guid? DriverId
);
