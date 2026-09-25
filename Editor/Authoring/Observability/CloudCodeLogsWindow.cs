using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Authoring.Client.Http;
#if UNITY_6000_3_OR_NEWER
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
#endif
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if DEPLOYMENT_API_AVAILABLE_V1_1
using IProjectID = Unity.Services.DeploymentApi.Editor.IProjectIdentifierProvider;
#else
using IProjectID = Unity.Services.CloudCode.Authoring.Editor.Deployment.IProjectIdentifierProvider;
#endif

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    class CloudCodeLogsWindow : EditorWindow
    {
        const string k_QuerySyntaxDocsUrl =
            "https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/logging/concepts/filter-logs";

        static readonly string k_AssetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Observability", "Assets");
        static readonly string k_WindowUxmlPath =
            PathUtils.Join(k_AssetPath, "CloudCodeLogsWindow.uxml");
        static readonly string k_RecordRowUxmlPath =
            PathUtils.Join(k_AssetPath, "CloudCodeLogRecordRow.uxml");
        static readonly string k_StyleSheetPath =
            PathUtils.Join(k_AssetPath, "CloudCodeLogsWindow.uss");

        const string k_HiddenClass = "logs-hidden";
        const string k_SeverityWarnClass = "log-row__severity--warn";
        const string k_SeverityErrorClass = "log-row__severity--error";
        const string k_StatusErrorClass = "logs-status--error";

        internal const int BodyPreviewLength = 2000;

        internal const string TimeRangePrefKey = "Unity.Services.CloudCode.Observability.Logs.TimeRange";
        internal const string SeverityPrefKey = "Unity.Services.CloudCode.Observability.Logs.Severity";

        static readonly (string Label, string From)[] k_TimeRanges =
        {
            ("Last 1 minute", "now-1m"),
            ("Last 5 minutes", "now-5m"),
            ("Last 10 minutes", "now-10m"),
            ("Last 30 minutes", "now-30m"),
            ("Last 1 hour", "now-1h"),
            ("Last 3 hours", "now-3h"),
            ("Last 12 hours", "now-12h"),
            ("Last 1 day", "now-1d"),
            ("Last 7 days", "now-7d")
        };

        internal const int DefaultTimeRangeIndex = 4;

        ILogsClient m_Client;

        readonly LogsQuery m_Query = new LogsQuery();
        readonly List<LogRecord> m_Records = new List<LogRecord>();
        LogsResponse m_LastResponse;

        CancellationTokenSource m_RequestCancellation;
        bool m_InitialQueryDone;
        bool m_RequestInFlight;

        VisualTreeAsset m_RecordRowTemplate;

        DropdownField m_TimeRangeField;
        EnumField m_SeverityField;
        TextField m_QueryField;
        ListView m_RecordList;
        VisualElement m_DetailContent;
        Button m_PrevButton;
        Button m_NextButton;
        Button m_RefreshButton;
        Button m_SearchButton;
        Label m_StatusLabel;
        HelpBox m_LocalServerWarning;
#if UNITY_6000_3_OR_NEWER
        ICloudCodeLocalServer m_LocalServer;
#endif

        [MenuItem("Services/CloudCode/Observability Logs")]
        public static void ShowWindow()
        {
            var window = GetWindow<CloudCodeLogsWindow>();
            window.titleContent = new GUIContent("Cloud Code Observability Logs");
            window.minSize = new Vector2(640, 360);
            window.Show();
        }

        void CreateGUI()
        {
            m_InitialQueryDone = false;
            m_Client = CreateClient();
            m_Query.From = k_TimeRanges[LoadTimeRangeIndex()].From;
            m_Query.Severity = LoadSeverity();

            if (!LoadVisualTree())
            {
                return;
            }

            ResolveElements();
            BindElements();

            SubscribeToLocalServer();

            RefreshLocalServerWarning();
            RefreshResultViews();
            BeginInitialQuery();
        }

        bool LoadVisualTree()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_WindowUxmlPath);
            m_RecordRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_RecordRowUxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetPath);

            if (tree == null || m_RecordRowTemplate == null)
            {
                rootVisualElement.Add(new HelpBox(
                    $"Could not load the Cloud Code logs window layout from {k_AssetPath}.",
                    HelpBoxMessageType.Error));
                return false;
            }

            tree.CloneTree(rootVisualElement);

            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            return true;
        }

        void ResolveElements()
        {
            m_LocalServerWarning = rootVisualElement.Q<HelpBox>("local-server-warning");
            m_TimeRangeField = rootVisualElement.Q<DropdownField>("time-range");
            m_SeverityField = rootVisualElement.Q<EnumField>("severity-field");
            m_QueryField = rootVisualElement.Q<TextField>("query-field");
            m_RecordList = rootVisualElement.Q<ListView>("record-list");
            m_DetailContent = rootVisualElement.Q<VisualElement>("detail-content");
            m_StatusLabel = rootVisualElement.Q<Label>("status-label");
            m_PrevButton = rootVisualElement.Q<Button>("prev-button");
            m_NextButton = rootVisualElement.Q<Button>("next-button");
            m_RefreshButton = rootVisualElement.Q<Button>("refresh-button");
            m_SearchButton = rootVisualElement.Q<Button>("search-button");
        }

        void BindElements()
        {
            m_TimeRangeField.choices = k_TimeRanges.Select(r => r.Label).ToList();
            m_TimeRangeField.index = LoadTimeRangeIndex();
            m_TimeRangeField.RegisterValueChangedCallback(_ => OnTimeRangeChanged());

            m_SeverityField.Init(LoadSeverity());
            m_SeverityField.RegisterValueChangedCallback(evt =>
            {
                m_Query.Severity = (LogSeverity)evt.newValue;
                EditorPrefs.SetInt(SeverityPrefKey, (int)m_Query.Severity);
                ResetPagingAndQuery();
            });

            m_QueryField.tooltip =
                "Optional query expression (Example: body ~= \"timeout\".)";
            m_QueryField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    ResetPagingAndQuery();
                }
            });

            m_RefreshButton.clicked += ResetPagingAndQuery;
            m_SearchButton.clicked += ResetPagingAndQuery;
            rootVisualElement.Q<Button>("syntax-button").clicked +=
                () => Application.OpenURL(k_QuerySyntaxDocsUrl);
            rootVisualElement.Q<Button>("copy-selected-button").clicked += CopySelected;
            rootVisualElement.Q<Button>("copy-all-button").clicked += CopyAll;
            m_PrevButton.clicked += LoadPreviousPage;
            m_NextButton.clicked += LoadNextPage;

            m_RecordList.horizontalScrollingEnabled = true;
            m_RecordList.itemsSource = m_Records;
            m_RecordList.makeItem = MakeRecordRow;
            m_RecordList.bindItem = BindRecordRow;
            m_RecordList.selectionChanged += _ => RefreshDetailPane();
        }

        void OnDisable()
        {
            EditorApplication.update -= WaitForProjectBinding;
            UnsubscribeFromLocalServer();
            CancelPendingRequest();
        }

        void SubscribeToLocalServer()
        {
#if UNITY_6000_3_OR_NEWER
            m_LocalServer = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();

            if (m_LocalServer != null)
            {
                m_LocalServer.OnServerStatusChanged += OnLocalServerStatusChanged;
            }
#endif
        }

        void UnsubscribeFromLocalServer()
        {
#if UNITY_6000_3_OR_NEWER
            if (m_LocalServer != null)
            {
                m_LocalServer.OnServerStatusChanged -= OnLocalServerStatusChanged;
                m_LocalServer = null;
            }
#endif
        }

#if UNITY_6000_3_OR_NEWER
        void OnLocalServerStatusChanged(
            object sender,
            ICloudCodeLocalServer.LocalCloudCodeServerStatus status)
        {
            RefreshLocalServerWarning();
        }

#endif

        void RefreshLocalServerWarning()
        {
            if (m_LocalServerWarning == null)
            {
                return;
            }

            m_LocalServerWarning.EnableInClassList(k_HiddenClass, !IsUsingLocalServer());
        }

        bool IsUsingLocalServer()
        {
#if UNITY_6000_3_OR_NEWER
            return m_LocalServer != null
                && m_LocalServer.GetCurrentServerStatus()
                != ICloudCodeLocalServer.LocalCloudCodeServerStatus.Idle;
#else
            return false;
#endif
        }

        static ILogsClient CreateClient()
        {
            var services = CloudCodeAuthoringServices.Instance;
            return new LogsClient(
                services.GetService<IAccessTokens>(),
                services.GetService<IProjectID>(),
                services.GetService<IEnvironmentsApi>(),
                services.GetService<IHttpClient>());
        }

        VisualElement MakeRecordRow()
        {
            var row = m_RecordRowTemplate.CloneTree().Q<VisualElement>(className: "log-row");
            row.RemoveFromHierarchy();
            return row;
        }

        internal static string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrEmpty(timestamp))
            {
                return string.Empty;
            }

            if (!DateTime.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return timestamp;
            }

            return parsed.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC";
        }

        void BindRecordRow(VisualElement element, int index)
        {
            var record = m_Records[index];

            element.Q<Label>("timestamp").text = FormatTimestamp(record.Timestamp);

            var severity = element.Q<Label>("severity");
            severity.text = record.SeverityText ?? string.Empty;
            ApplySeverityClass(severity, record.SeverityNumber);

            element.Q<Label>("body").text = SingleLine(record.Body);
        }

        void OnTimeRangeChanged()
        {
            var range = k_TimeRanges[m_TimeRangeField.index];

            m_Query.From = range.From;

            EditorPrefs.SetString(TimeRangePrefKey, range.From);

            ResetPagingAndQuery();
        }

        internal static string TimeRangeFrom(int index)
        {
            return k_TimeRanges[index].From;
        }

        internal static int LoadTimeRangeIndex()
        {
            var stored = EditorPrefs.GetString(TimeRangePrefKey, null);

            if (!string.IsNullOrEmpty(stored))
            {
                for (var i = 0; i < k_TimeRanges.Length; i++)
                {
                    if (k_TimeRanges[i].From == stored)
                    {
                        return i;
                    }
                }
            }

            return DefaultTimeRangeIndex;
        }

        internal static LogSeverity LoadSeverity()
        {
            var stored = EditorPrefs.GetInt(SeverityPrefKey, (int)LogSeverity.All);

            return Enum.IsDefined(typeof(LogSeverity), stored)
                ? (LogSeverity)stored
                : LogSeverity.All;
        }

        void ResetPagingAndQuery()
        {
            m_Query.To = FormatQueryTimestamp(DateTime.UtcNow);
            SetOffsetAndQuery(0);
        }

        internal static string FormatQueryTimestamp(DateTime utc)
        {
            return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        void LoadPreviousPage()
        {
            SetOffsetAndQuery(PreviousOffset(m_Query.NormalizedOffset, m_Query.NormalizedLimit));
        }

        internal static int PreviousOffset(int offset, int limit)
        {
            var previous = offset - limit;
            return previous < 0 ? 0 : previous;
        }

        void LoadNextPage()
        {
            SetOffsetAndQuery(m_Query.NormalizedOffset + m_Query.NormalizedLimit);
        }

        void SetOffsetAndQuery(int offset)
        {
            m_Query.Offset = offset;
            Query();
        }

        void Query()
        {
            m_InitialQueryDone = true;
            m_Query.UserFilter = m_QueryField.value;
            _ = QueryAsync();
        }

        async Task QueryAsync()
        {
            CancelPendingRequest();
            var cancellation = new CancellationTokenSource();
            m_RequestCancellation = cancellation;

            m_RequestInFlight = true;
            SetStatus("Querying Cloud Code logs...");
            RefreshQueryControls();

            try
            {
                var response = await m_Client.GetLogsAsync(m_Query, cancellation.Token);

                if (cancellation.IsCancellationRequested)
                {
                    return;
                }

                m_LastResponse = response;
                m_Records.Clear();

                if (response.Results != null)
                {
                    m_Records.AddRange(response.Results);
                }

                RefreshResultViews();
                SetStatus(m_Records.Count == 0
                    ? "No Cloud Code logs matched the query."
                    : BuildResultsStatus());
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                if (cancellation.IsCancellationRequested)
                {
                    return;
                }

                m_LastResponse = null;
                m_Records.Clear();
                RefreshResultViews();
                SetStatus(e.Message, isError: true);
            }
            finally
            {
                if (ReferenceEquals(m_RequestCancellation, cancellation))
                {
                    m_RequestInFlight = false;
                    m_RequestCancellation = null;
                    RefreshQueryControls();
                }

                cancellation.Dispose();
            }
        }

        void CancelPendingRequest()
        {
            if (m_RequestCancellation == null)
            {
                return;
            }

            m_RequestCancellation.Cancel();
            m_RequestCancellation = null;
            m_RequestInFlight = false;
        }

        void BeginInitialQuery()
        {
            if (TryStartInitialQuery())
            {
                return;
            }

            EditorApplication.update += WaitForProjectBinding;
        }

        void WaitForProjectBinding()
        {
            TryStartInitialQuery();
        }

        bool TryStartInitialQuery()
        {
            if (!CloudProjectSettings.projectBound)
            {
                return false;
            }

            if (!m_InitialQueryDone)
            {
                ResetPagingAndQuery();
            }

            EditorApplication.update -= WaitForProjectBinding;
            return true;
        }

        void RefreshResultViews()
        {
            m_RecordList.ClearSelection();
            m_RecordList.Rebuild();
            RefreshDetailPane();
            RefreshQueryControls();
        }

        string BuildResultsStatus()
        {
            return BuildResultsStatus(
                m_Query.NormalizedOffset,
                m_Records.Count,
                m_LastResponse == null ? 0 : m_LastResponse.Total);
        }

        internal static string BuildResultsStatus(int offset, int recordCount, int total)
        {
            var first = offset + 1;
            var last = offset + recordCount;

            if (total >= last)
            {
                return $"Showing {first}-{last} of {total} record(s).";
            }

            return $"Showing record(s) {first}-{last}.";
        }

        void RefreshQueryControls()
        {
            m_RefreshButton.SetEnabled(!m_RequestInFlight);
            m_SearchButton.SetEnabled(!m_RequestInFlight);

            m_PrevButton.SetEnabled(!m_RequestInFlight && m_Query.NormalizedOffset > 0);

            var hasMore = m_LastResponse != null
                && HasMorePages(
                m_Query.NormalizedOffset,
                m_Query.NormalizedLimit,
                m_Records.Count,
                m_LastResponse.Total);
            m_NextButton.SetEnabled(!m_RequestInFlight && hasMore);
        }

        internal static bool HasMorePages(int offset, int limit, int recordCount, int total)
        {
            if (recordCount < limit)
            {
                return false;
            }

            return total <= 0 || offset + limit < total;
        }

        void RefreshDetailPane()
        {
            m_DetailContent.Clear();

            var record = SelectedRecord();
            if (record == null)
            {
                m_DetailContent.Add(new Label("(select a log record)"));
                return;
            }

            m_DetailContent.Add(DetailRow(
                "Severity",
                FormatSeverity(record.SeverityText, record.SeverityNumber)));
            m_DetailContent.Add(DetailRow("Timestamp", FormatTimestamp(record.Timestamp)));
            m_DetailContent.Add(DetailRow("Body", record.Body));

            AddAttributeSection("Resource Attributes", record.ResourceAttributes);
            AddAttributeSection("Log Attributes", record.LogAttributes);
        }

        void AddAttributeSection(string title, Dictionary<string, string> attributes)
        {
            if (attributes == null || attributes.Count == 0)
            {
                return;
            }

            var header = new Label(title);
            header.AddToClassList("logs-detail__section-header");
            m_DetailContent.Add(header);

            foreach (var attribute in attributes.OrderBy(a => a.Key))
            {
                m_DetailContent.Add(DetailRow(attribute.Key, attribute.Value));
            }
        }

        static VisualElement DetailRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("logs-detail__row");

            var key = new Label(label);
            key.AddToClassList("logs-detail__key");
            row.Add(key);

            var text = new Label(value ?? string.Empty);
            text.AddToClassList("logs-detail__value");
            text.focusable = true;
            text.selection.isSelectable = true;
            row.Add(text);

            return row;
        }

        void SetStatus(string message, bool isError = false)
        {
            m_StatusLabel.text = message;
            m_StatusLabel.tooltip = message;
            m_StatusLabel.EnableInClassList(k_StatusErrorClass, isError);
        }

        void CopySelected()
        {
            var record = SelectedRecord();
            if (record == null)
            {
                SetStatus("Select a log record first.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = ToJson(record);
            SetStatus("Copied the selected record to the clipboard.");
        }

        void CopyAll()
        {
            if (m_Records.Count == 0)
            {
                SetStatus("There is nothing to copy.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = ToJson(m_Records);
            SetStatus($"Copied {m_Records.Count} record(s) to the clipboard.");
        }

        static string ToJson(object value)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            return IsolatedJsonConvert.SerializeObject(value, settings);
        }

        LogRecord SelectedRecord()
        {
            return m_RecordList == null ? null : m_RecordList.selectedItem as LogRecord;
        }

        internal static string FormatSeverity(string severityText, int? severityNumber)
        {
            if (severityNumber == null)
            {
                return severityText ?? string.Empty;
            }

            var number = severityNumber.Value.ToString(CultureInfo.InvariantCulture);

            return string.IsNullOrEmpty(severityText) ? number : $"{severityText} ({number})";
        }

        internal static string SingleLine(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(body.Length);
            foreach (var character in body)
            {
                builder.Append(character == '\n' || character == '\r' || character == '\t' ? ' ' : character);
            }

            var line = builder.ToString();
            return line.Length > BodyPreviewLength ? line.Substring(0, BodyPreviewLength) + "…" : line;
        }

        static void ApplySeverityClass(VisualElement severity, int? severityNumber)
        {
            severity.RemoveFromClassList(k_SeverityWarnClass);
            severity.RemoveFromClassList(k_SeverityErrorClass);

            if (severityNumber == null)
            {
                return;
            }

            if (severityNumber >= (int)LogSeverity.Error)
            {
                severity.AddToClassList(k_SeverityErrorClass);
            }
            else if (severityNumber >= (int)LogSeverity.Warn)
            {
                severity.AddToClassList(k_SeverityWarnClass);
            }
        }
    }
}
