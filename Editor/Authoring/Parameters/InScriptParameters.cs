using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Parameters
{
    class InScriptParameters
    {
        struct EvaluatedParam
        {
            public ParameterType type { get; set; }
            public bool required { get; set; }
        }

        static readonly string k_FailedToParseMessage = L10n.Tr("Failed to parse in-script parameters. ");
        static readonly string k_FailedToParseParameterMessageFormat = L10n.Tr("Failed to parse parameter '{0}'. ");
        static readonly string k_FailedToParseParameterTypeMessageFormat = L10n.Tr("Could not parse '{0}'.");

        readonly INodeJsRunner m_ScriptRunner;
        readonly ILogger m_Logger;

        public InScriptParameters(INodeJsRunner runner, ILogger logger)
        {
            m_ScriptRunner = runner;
            m_Logger = logger;
        }

        public async Task<List<CloudCodeParameter>> GetParametersFromPath(string path)
        {
            var fullScriptPath = Path.GetFullPath(ScriptPaths.ScriptParameters);
            var output = await m_ScriptRunner.ExecNodeJs(new[] { fullScriptPath, path });
            return ParseParameters(path, output);
        }

        List<CloudCodeParameter> ParseParameters(string path, string output)
        {
            if (output == string.Empty)
            {
                return null;
            }

            try
            {
                JObject parameters = JObject.Parse(output);

                if (parameters == null)
                {
                    LogFailedToParse(path, k_FailedToParseMessage);
                    return null;
                }

                var parsedParams = new List<CloudCodeParameter>();
                foreach (var symbol in parameters)
                {
                    var paramName = symbol.Key;
                    var cloudCodeParam = new CloudCodeParameter
                    {
                        Name = paramName
                    };

                    string failureReason;
                    if (!TryParseParameter(symbol.Value, cloudCodeParam, out failureReason))
                    {
                        LogFailedToParse(
                            path,
                            string.Format(k_FailedToParseParameterMessageFormat, paramName)
                            + failureReason);
                        return null;
                    }

                    parsedParams.Add(cloudCodeParam);
                }
                return parsedParams;
            }
            catch (JsonReaderException)
            {
                LogFailedToParse(path, k_FailedToParseMessage);
                return null;
            }
            catch (JsonSerializationException)
            {
                LogFailedToParse(path, k_FailedToParseMessage);
                return null;
            }
        }

        bool TryParseParameter(JToken param, CloudCodeParameter result, out string failureReason)
        {
            failureReason = string.Empty;

            if (param is JValue)
            {
                return TryParseValue(param, result, out failureReason);
            }

            if (param is JObject jParamData)
            {
                return TryParseObject(result, jParamData);
            }
            return false;
        }

        static bool TryParseValue(JToken param, CloudCodeParameter result, out string failureReason)
        {
            failureReason = string.Format(
                k_FailedToParseParameterTypeMessageFormat,
                param);
            try
            {
                ParameterType type = param.ToObject<ParameterType>();
                result.ParameterType = type;
            }
            catch (JsonSerializationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        static bool TryParseObject(CloudCodeParameter result, JObject jParamData)
        {
            try
            {
                var paramData = jParamData.ToObject<EvaluatedParam>();
                result.ParameterType = paramData.type;
                result.Required = paramData.required;
            }
            catch (JsonSerializationException)
            {
                return false;
            }
            return true;
        }

        void LogFailedToParse(string path, string message)
        {
            m_Logger.LogError(path, message);
        }
    }
}
