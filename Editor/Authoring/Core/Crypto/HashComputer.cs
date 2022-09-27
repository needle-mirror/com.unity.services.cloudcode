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
            using var md5 = MD5.Create();
            using var fileStream = File.OpenRead(script.Path);
            var hashBytes = md5.ComputeHash(fileStream);
            return BitConverter
                .ToString(hashBytes)
                .Replace("-", "")
                .ToLowerInvariant();
        }
    }
}
