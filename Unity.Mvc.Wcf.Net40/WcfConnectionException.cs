using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Mvc.Wcf
{
    public class WcfConnectionException : Exception
    {
        public WcfConnectionException(string message, Exception innerException): base(message, innerException) { }
    }
}
