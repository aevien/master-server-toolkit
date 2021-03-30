using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public class MstMessageHandlerException : SystemException
    {
        public ResponseStatus Status { get; set; }

        public MstMessageHandlerException()
        {
            Status = ResponseStatus.Error;
        }

        public MstMessageHandlerException(string message) : base(message)
        {
            Status = ResponseStatus.Error;
        }

        public MstMessageHandlerException(string message, ResponseStatus status) : base(message)
        {
            Status = status;
        }

        public MstMessageHandlerException(string message, ResponseStatus status, Exception innerException) : base(message, innerException)
        {
            Status = status;
        }

        protected MstMessageHandlerException(ResponseStatus status, SerializationInfo info, StreamingContext context) : base(info, context)
        {
            Status = status;
        }
    }
}