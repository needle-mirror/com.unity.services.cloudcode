// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.Clients
{
    class InvalidJwtException : Exception
    {
        public string Token { get; }

        public InvalidJwtException(string message, string token) : base(message)
        {
            Token = token;
        }
    }
}
