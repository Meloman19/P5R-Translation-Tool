using System;

namespace P5R_Packager.Common
{
    internal sealed class KnownException : Exception
    {
        public KnownException(string message) : base(message) { }
    }
}