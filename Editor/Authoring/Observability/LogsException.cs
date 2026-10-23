using System;
using System.Text;
using Unity.Services.CloudCode.Authoring.Client.Http;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    class LogsUnavailableException : Exception
    {
        public LogsUnavailableException(string message) : base(message)
        {
        }
    }

    class LogsRequestException : HttpException
    {
        public ProblemJson ProblemJson { get; }

        public LogsRequestException(HttpClientResponse response, ProblemJson problemJson)
            : base(Format(response, problemJson))
        {
            ProblemJson = problemJson;
            Response = response;
        }

        static string Format(HttpClientResponse response, ProblemJson problemJson)
        {
            var builder = new StringBuilder();
            builder.Append($"({response.StatusCode}) ");

            var title = problemJson?.Title;
            var detail = problemJson?.Detail;

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(detail))
            {
                builder.Append(string.IsNullOrEmpty(response.ErrorMessage)
                    ? "Failed to query Cloud Code logs."
                    : response.ErrorMessage);
            }
            else
            {
                builder.Append(title);

                if (!string.IsNullOrEmpty(detail))
                {
                    builder.Append(string.IsNullOrEmpty(title) ? detail : $". {detail}");
                }
            }

            if (response.StatusCode == 403)
            {
                builder.Append("\nYour Unity account needs the 'Observability Viewer' role on this project.");
            }

            return builder.ToString();
        }
    }
}
