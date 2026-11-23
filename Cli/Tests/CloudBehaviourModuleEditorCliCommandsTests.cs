#if UNITY_6000_7_OR_NEWER
using System;
using NUnit.Framework;

namespace Unity.Services.CloudCode.Cli.Tests
{
    class CloudBehaviourModuleEditorCliCommandsTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(".-")]
        [TestCase("?<>")]
        public void CreateCloudBehaviour_BehaviourNameWithoutAllowedChars_Throws(string behaviourName)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => CloudBehaviourModuleEditorCliCommands.CreateCloudBehaviour(behaviourName, "Assets/MyModule/MyModule.ccmu"));

            Assert.That(exception.Message, Does.Contain("behaviour-name"));
        }
    }
}
#endif
