namespace Network.NetworkHost
{
    public readonly struct AuthoritativeMovementWorldValidationResult
    {
        private AuthoritativeMovementWorldValidationResult(bool isAllowed)
        {
            IsAllowed = isAllowed;
        }

        public bool IsAllowed { get; }

        public static AuthoritativeMovementWorldValidationResult Allow()
        {
            return new AuthoritativeMovementWorldValidationResult(true);
        }

        public static AuthoritativeMovementWorldValidationResult Reject()
        {
            return new AuthoritativeMovementWorldValidationResult(false);
        }
    }
}
