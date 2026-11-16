#if UNITY_PIPELINE_AVAILABLE && UNITY_EDITOR && UNITY_6000_3_OR_NEWER
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;

namespace Unity.Services.CloudCode.Cli.Tests
{
    class CloudCodeEditorCliCommandsTests
    {
        const string k_LogName = "server-20260101-000000-000.log";

        string m_Dir;
        string m_LogFile;
        string m_OldLogDir;

        [SetUp]
        public void Setup()
        {
            m_Dir = Path.Combine(Path.GetTempPath(), "cc-cli-logs-" + Path.GetRandomFileName());
            Directory.CreateDirectory(m_Dir);
            m_LogFile = Path.Combine(m_Dir, k_LogName);
            File.WriteAllText(m_LogFile,
                "{\"@t\":\"2026-01-01T00:00:00Z\",\"@m\":\"m1\"}\n{\"@t\":\"2026-01-01T00:00:01Z\",\"@m\":\"m2\"}\n");
            m_OldLogDir = CloudCodeLocalServer.s_ServerLogDirectoryOverride;
            CloudCodeLocalServer.s_ServerLogDirectoryOverride = m_Dir;
            Assume.That(CloudCodeEditorCliCommands.GetLocalServerLogs().LogFile, Is.EqualTo(m_LogFile),
                "A running local server tails its own log file instead of the one under test.");
        }

        [TearDown]
        public void TearDown()
        {
            CloudCodeLocalServer.s_ServerLogDirectoryOverride = m_OldLogDir;
            if (Directory.Exists(m_Dir))
                Directory.Delete(m_Dir, true);
        }

        [Test]
        public void GetLocalServerLogs_Defaults_ReturnsTheWholeLog()
        {
            var page = CloudCodeEditorCliCommands.GetLocalServerLogs();

            Assert.That(page.Entries.Select(e => e.Message), Is.EqualTo(new[] { "m1", "m2" }));
            Assert.That(page.Cursor, Is.EqualTo($"{k_LogName}@{new FileInfo(m_LogFile).Length}"));
        }

        [Test]
        public void GetLocalServerLogs_LimitPassedThrough_ReturnsTheNewestEntries()
        {
            var page = CloudCodeEditorCliCommands.GetLocalServerLogs(limit: 1);

            Assert.That(page.Entries.Select(e => e.Message), Is.EqualTo(new[] { "m2" }));
        }

        [TestCase("verbose")]
        [TestCase("INFO")]
        [TestCase("Warning")]
        [TestCase("eRRor")]
        public void GetLocalServerLogs_ValidLevel_IsCaseInsensitive(string level)
        {
            Assert.DoesNotThrow(() => CloudCodeEditorCliCommands.GetLocalServerLogs(level: level));
        }

        [TestCase("bogus")]
        [TestCase("")]
        [TestCase("99")]
        public void GetLocalServerLogs_InvalidLevel_Throws(string level)
        {
            var e = Assert.Throws<ArgumentException>(() => CloudCodeEditorCliCommands.GetLocalServerLogs(level: level));
            Assert.That(e.ParamName, Is.EqualTo("level"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void GetLocalServerLogs_LimitBelowOne_Throws(int limit)
        {
            var e = Assert.Throws<ArgumentException>(() => CloudCodeEditorCliCommands.GetLocalServerLogs(limit: limit));
            Assert.That(e.ParamName, Is.EqualTo("limit"));
        }

        [TestCase("garbage")]
        [TestCase("@5")]
        [TestCase("server.log@")]
        [TestCase("server.log@abc")]
        [TestCase("server.log@-1")]
        public void GetLocalServerLogs_MalformedCursor_Throws(string cursor)
        {
            var e = Assert.Throws<ArgumentException>(() => CloudCodeEditorCliCommands.GetLocalServerLogs(cursor: cursor));
            Assert.That(e.ParamName, Is.EqualTo("cursor"));
        }

        [Test]
        public void GetLocalServerLogs_WellFormedCursor_ReturnsPage()
        {
            var page = CloudCodeEditorCliCommands.GetLocalServerLogs(cursor: $"{k_LogName}@0", limit: 1);

            Assert.That(page.Entries.Select(e => e.Message), Is.EqualTo(new[] { "m1" }));
            Assert.That(page.HasMore, Is.True);
            Assert.That(page.CursorReset, Is.False);
        }
    }
}
#endif
