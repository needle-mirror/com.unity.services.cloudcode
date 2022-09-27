// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.DependencyInversion
{
    class ConstructorNotFoundException : Exception
    {
        public ConstructorNotFoundException(Type type)
            : base($"Type {type.Name} must have a single public constructor")
        {
        }
    }
}
