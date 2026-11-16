using System;
using NUnit.Framework;
using Unity.Services.CloudCode.Authoring.Editor.Observability;

namespace Unity.Services.CloudCode.Cli.Tests
{
    class CloudCodeLogsEditorCliCommandsTests
    {
        [TestCase("abc")]
        [TestCase("0")]
        [TestCase("101")]
        [TestCase("-1")]
        public void GetRemoteLogs_InvalidLimit_Throws(string limit)
        {
            var exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await CloudCodeEditorCliCommands.GetRemoteLogs(limit: limit));

            Assert.That(exception.ParamName, Is.EqualTo("limit"));
            Assert.That(exception.Message, Does.Contain($"'{limit}' is not a valid limit (expected 1-100)"));
        }

        [TestCase("-1")]
        [TestCase("abc")]
        public void GetRemoteLogs_InvalidOffset_Throws(string offset)
        {
            var exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await CloudCodeEditorCliCommands.GetRemoteLogs(offset: offset));

            Assert.That(exception.ParamName, Is.EqualTo("offset"));
            Assert.That(exception.Message, Does.Contain($"'{offset}' is not a valid offset"));
        }

        [TestCase("5")]
        [TestCase("loud")]
        public void GetRemoteLogs_InvalidSeverity_Throws(string severity)
        {
            var exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await CloudCodeEditorCliCommands.GetRemoteLogs(severity: severity));

            Assert.That(exception.ParamName, Is.EqualTo("severity"));
            Assert.That(exception.Message, Does.Contain($"'{severity}' is not a valid severity"));
        }

        [Test]
        public void GetRemoteLogs_InvalidTo_Throws()
        {
            var exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await CloudCodeEditorCliCommands.GetRemoteLogs(to: "now-5x"));

            Assert.That(exception.ParamName, Is.EqualTo("to"));
            Assert.That(exception.Message, Does.Contain("'now-5x' is not a valid relative time"));
        }

        [Test]
        public void GetRemoteLogs_InvalidFrom_Throws()
        {
            var exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await CloudCodeEditorCliCommands.GetRemoteLogs(from: "nowhere"));

            Assert.That(exception.ParamName, Is.EqualTo("from"));
            Assert.That(exception.Message, Does.Contain("'nowhere' is not a valid relative time"));
        }

        [Test]
        public void BuildNextPage_AllSeverityWithoutFilter_OmitsSeverityAndFilter()
        {
            Assert.That(CloudCodeEditorCliCommands.BuildNextPage("2026-01-01T00:00:00.000Z", "2026-01-01T01:00:00.000Z", "All", null, 50, 50),
                Is.EqualTo("--from 2026-01-01T00:00:00.000Z --to 2026-01-01T01:00:00.000Z --limit 50 --offset 50"));
        }

        [Test]
        public void BuildNextPage_WithSeverityAndFilter_IncludesBoth()
        {
            Assert.That(CloudCodeEditorCliCommands.BuildNextPage("a", "b", "Warn", "body ~= \"timeout\"", 10, 20),
                Is.EqualTo("--from a --to b --limit 10 --offset 20 --severity warn --filter 'body ~= \"timeout\"'"));
        }

        [Test]
        public void BuildNextPage_FilterWithDollarAndSingleQuote_QuotesForPosixShell()
        {
            Assert.That(CloudCodeEditorCliCommands.BuildNextPage("a", "b", "All", "body ~= \"$HOME\" AND body ~= 'x'", 10, 20),
                Is.EqualTo("--from a --to b --limit 10 --offset 20 --filter 'body ~= \"$HOME\" AND body ~= '\\''x'\\'''"));
        }
    }
}
