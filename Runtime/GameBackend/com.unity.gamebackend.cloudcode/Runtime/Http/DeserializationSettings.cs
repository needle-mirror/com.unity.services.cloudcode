using System;

namespace Unity.GameBackend.CloudCode.Http
{   
    public enum MissingMemberHandling
    {
        Error,
        Ignore
    }
    public class DeserializationSettings
    {
        public MissingMemberHandling MissingMemberHandling = MissingMemberHandling.Error;
    }
    
}
