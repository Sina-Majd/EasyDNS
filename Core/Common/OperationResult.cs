namespace EasyDNS.Core.Common
{
    public class OperationResult
    {
        public bool Success { get; }
        public string Message { get; }

        private OperationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static OperationResult Ok(string message = "Operation completed successfully.") => new(true, message);
        public static OperationResult Fail(string message) => new(false, message);
    }
}
