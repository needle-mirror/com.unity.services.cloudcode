namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    class LogsTarget
    {
        public string ProjectId { get; }
        public string EnvironmentId { get; }

        public string UnavailableReason { get; }

        public bool IsReady
        {
            get { return UnavailableReason == null; }
        }

        public LogsTarget(string projectId, string environmentId, string unavailableReason)
        {
            ProjectId = projectId;
            EnvironmentId = environmentId;
            UnavailableReason = unavailableReason;
        }
    }
}
