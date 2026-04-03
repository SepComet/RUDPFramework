using System;

namespace Network.NetworkHost
{
    public sealed class PermissiveAuthoritativeMovementWorldValidator : IAuthoritativeMovementWorldValidator
    {
        private PermissiveAuthoritativeMovementWorldValidator()
        {
        }

        public static PermissiveAuthoritativeMovementWorldValidator Instance { get; } = new PermissiveAuthoritativeMovementWorldValidator();

        public AuthoritativeMovementWorldValidationResult Validate(AuthoritativeMovementWorldValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return AuthoritativeMovementWorldValidationResult.Allow();
        }
    }
}
