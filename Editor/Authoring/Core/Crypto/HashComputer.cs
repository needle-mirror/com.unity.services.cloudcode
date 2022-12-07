using System;
using System.IO;
using System.Security.Cryptography;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Crypto
{
    class HashComputer : IHashComputer
    {
        public string ComputeFileHash(IScript script)
        {
            // No security risk here
            // we hash the name of the file for user privacy
            // ignoring here allows us to follow user journey instead of changing hash
#pragma warning disable CA5351
            using var md5 = MD5.Create();
            using var fileStream = File.OpenRead(script.Path);
            var hashBytes = md5.ComputeHash(fileStream);
            return BitConverter
                .ToString(hashBytes)
                .Replace("-", "")
                .ToLowerInvariant();
#pragma warning restore CA5351
        }
    }
}
