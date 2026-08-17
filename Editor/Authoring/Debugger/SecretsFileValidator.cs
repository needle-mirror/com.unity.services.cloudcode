#if UNITY_6000_3_OR_NEWER
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class SecretsFileValidator
    {
        const string k_JsonExtension = ".json";

        internal enum Result
        {
            Valid,
            NotJsonExtension,
            UnparseableJson
        }

        internal static Result Validate(string assetPath, string contents)
        {
            if (string.IsNullOrEmpty(assetPath)
                || !assetPath.EndsWith(k_JsonExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Result.NotJsonExtension;
            }

            if (string.IsNullOrWhiteSpace(contents))
            {
                return Result.UnparseableJson;
            }

            JToken token;
            try
            {
                token = JToken.Parse(contents);
            }
            catch (JsonException)
            {
                return Result.UnparseableJson;
            }

            return token is JObject ? Result.Valid : Result.UnparseableJson;
        }
    }
}
#endif
