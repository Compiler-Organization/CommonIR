using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Errors
{
    public class ErrorHandler
    {
        public static Exception Create(string message)
        {
            Exception exception = new Exception(message);

            return exception;
        }

        public static NotImplementedException CreateNotImplimented(string message)
        {
            NotImplementedException exception = new NotImplementedException(message);

            return exception;
        }
    }
}
