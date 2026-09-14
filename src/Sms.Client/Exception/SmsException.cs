namespace Sms.Client;

public class SmsException : Exception
{
    public SmsException(string message, Exception? innerException = null): base(message, innerException) { }
    
}