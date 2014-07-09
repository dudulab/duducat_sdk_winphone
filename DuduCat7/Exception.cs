using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuduCat
{
    internal class ActiveConfigException : Exception
    {
        public ActiveConfigException() : base() { }
        public ActiveConfigException(string msg) : base(msg) { }
        public ActiveConfigException(string msg, Exception innerException) : base(msg, innerException) { }
    }

    internal class KeyNotFoundException : ActiveConfigException
    {
        public KeyNotFoundException() : base() { }
        public KeyNotFoundException(string msg) : base(msg) { }
        public KeyNotFoundException(string msg, Exception innerException) : base(msg, innerException) { }
    }
}
