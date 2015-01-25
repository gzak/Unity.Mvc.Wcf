using System;

namespace Unity.Mvc.Wcf
{
    public class WcfConnectionException : Exception
    {
        public WcfConnectionException(string message, Exception innerException): base(message, innerException) { }
    }
}
