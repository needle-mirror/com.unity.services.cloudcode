// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.DependencyInversion
{
    class TypeAlreadyRegisteredException : Exception
    {
        public TypeAlreadyRegisteredException(Type type)
            : base($"A factory for type {type.Name} has already been registered")
        {
        }
    }
}
