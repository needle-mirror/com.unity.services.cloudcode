#if UNITY_6000_7_OR_NEWER
using System;
using NUnit.Framework;

namespace Unity.Services.CloudCode.Cli.Tests
{
    class CloudCodeModuleEditorCliCommandsTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(".-")]
        [TestCase("é")]
        [TestCase("My.Module")]
        [TestCase("My Module")]
        [TestCase("3DGame")]
        [TestCase("_Game")]
        [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void CreateModule_InvalidModuleName_Throws(string moduleName)
        {
            var exception = Assert.Throws<ArgumentException>(() => CloudCodeModuleEditorCliCommands.CreateModule(moduleName));

            Assert.That(exception.Message, Does.Contain("module-name"));
        }

        [TestCase(".-")]
        [TestCase("é")]
        [TestCase("My-Module")]
        [TestCase("3DGame")]
        public void CreateModule_InvalidCloudAssembly_Throws(string cloudAssembly)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => CloudCodeModuleEditorCliCommands.CreateModule("MyModule", cloudAssembly: cloudAssembly));

            Assert.That(exception.Message, Does.Contain("cloud-assembly"));
        }

        [TestCase("/?<>")]
        [TestCase(":*|")]
        [TestCase("My:Client")]
        public void CreateModule_InvalidClientAssembly_Throws(string clientAssembly)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => CloudCodeModuleEditorCliCommands.CreateModule("MyModule", clientAssembly: clientAssembly));

            Assert.That(exception.Message, Does.Contain("client-assembly"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(".-")]
        [TestCase("?<>")]
        public void CreateModuleScript_ScriptNameWithoutAllowedChars_Throws(string scriptName)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => CloudCodeModuleEditorCliCommands.CreateModuleScript(scriptName, "Assets/MyModule/MyModule.ccmu"));

            Assert.That(exception.Message, Does.Contain("script-name"));
        }
    }
}
#endif
