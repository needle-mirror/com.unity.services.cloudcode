using System.Collections.Generic;
using System.Text;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    enum LogSeverity
    {
        All = 0,
        Debug = 5,
        Info = 9,
        Warn = 13,
        Error = 17
    }

    class LogsQuery
    {
        internal const string CloudCodeScopeClause = "resourceAttributes.service.name = \"cloud-code\"";

        internal const int MaxLimit = 100;

        const int k_SeverityBandSize = 4;

        public int Offset { get; set; }

        public int Limit { get; set; } = 50;

        public string From { get; set; }

        public string To { get; set; }

        public LogSeverity Severity { get; set; } = LogSeverity.All;

        public string UserFilter { get; set; }

        public int NormalizedOffset
        {
            get { return Offset < 0 ? 0 : Offset; }
        }

        public int NormalizedLimit
        {
            get
            {
                if (Limit < 1)
                {
                    return 1;
                }

                return Limit > MaxLimit ? MaxLimit : Limit;
            }
        }

        public string BuildFilterExpression()
        {
            var clauses = new List<string> { CloudCodeScopeClause };

            if (Severity != LogSeverity.All)
            {
                clauses.Add(BuildSeverityClause(Severity));
            }

            var userFilter = UserFilter == null ? null : UserFilter.Trim();
            if (!string.IsNullOrEmpty(userFilter))
            {
                clauses.Add($"({userFilter})");
            }

            var builder = new StringBuilder();
            for (var i = 0; i < clauses.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" AND ");
                }

                builder.Append(clauses[i]);
            }

            return builder.ToString();
        }

        static string BuildSeverityClause(LogSeverity severity)
        {
            var lowest = (int)severity;

            if (severity == LogSeverity.Error)
            {
                return $"severityNumber >= {lowest}";
            }

            return $"(severityNumber >= {lowest} AND severityNumber < {lowest + k_SeverityBandSize})";
        }
    }
}
