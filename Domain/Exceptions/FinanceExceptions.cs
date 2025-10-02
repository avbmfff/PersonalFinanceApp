namespace Application.Exceptions
{
    public class WalletNotFoundException : Exception
    {
        public WalletNotFoundException(Guid walletId)
            : base($"Wallet not found: {walletId}")
        {
        }
    }

    public class InvalidAmountException : Exception
    {
        public InvalidAmountException(decimal amount)
            : base($"Invalid amount: {amount}. Amount must be positive.")
        {
        }
    }

    public class InsufficientFundsException : Exception
    {
        public InsufficientFundsException(Guid walletId, decimal attempted, decimal available)
            : base($"Insufficient funds for wallet {walletId}. Attempted: {attempted}, Available: {available}")
        {
        }
    }

    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }
}