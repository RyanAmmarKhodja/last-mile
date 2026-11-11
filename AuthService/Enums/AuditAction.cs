namespace LastMile.Auth.Enums;

public enum AuditAction
{
    UserRegistered,
    LoginSucceeded,
    LoginFailed,
    TokenRefreshed,
    TokenRefreshFailed,
    RefreshTokenReuseDetected,
    Logout,
    M2MTokenIssued,
    M2MTokenDenied,
    UserCreated,
    UserUpdated,
    UserDeleted,
    UserLookedUp,
    ServiceClientCreated,
    ServiceClientDeleted,
    ServiceClientSecretRotated,
    SigningKeyGenerated
}
