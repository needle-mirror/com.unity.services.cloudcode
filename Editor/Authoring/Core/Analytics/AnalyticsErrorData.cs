using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Analytics
{
    /// <summary>
    /// The <c>error_data</c> field: detail the error code cannot name, as a small key/value map in a
    /// string. Keys are fixed here and values are identifiers, never a message or a path.
    /// </summary>
    static class AnalyticsErrorData
    {
        const string k_Exception = "exception";
        const string k_Level = "level";

        /// <summary>Null when there is no exception, so the field stays absent from a success.</summary>
        public static string FromException(Exception exception)
        {
            var unwrapped = AnalyticsErrorCode.Unwrap(exception);
            return unwrapped == null ? null : Map(k_Exception, unwrapped.GetType().Name);
        }

        /// <summary>For the send sites that already hold the type name rather than the exception.</summary>
        public static string FromExceptionName(string exceptionTypeName)
        {
            return string.IsNullOrEmpty(exceptionTypeName) ? null : Map(k_Exception, Simplify(exceptionTypeName));
        }

        public static string ForApiCompatibilityLevel(string level)
        {
            return Map(k_Level, level);
        }

        // Values are type and enum names, so they need no escaping.
        static string Map(string key, string value)
        {
            return "{\"" + key + "\":\"" + value + "\"}";
        }

        // Some sites pass GetType().ToString(); record the short name so the column groups.
        static string Simplify(string exceptionTypeName)
        {
            var lastDot = exceptionTypeName.LastIndexOf('.');
            return lastDot < 0 ? exceptionTypeName : exceptionTypeName.Substring(lastDot + 1);
        }
    }
}
