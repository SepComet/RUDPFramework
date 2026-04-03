namespace Network.NetworkHost
{
    public interface IAuthoritativeMovementWorldValidator
    {
        AuthoritativeMovementWorldValidationResult Validate(AuthoritativeMovementWorldValidationRequest request);
    }
}
