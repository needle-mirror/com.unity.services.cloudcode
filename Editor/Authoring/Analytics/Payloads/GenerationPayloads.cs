namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    static class GenerationPayloads
    {
        /// <summary>Success builds the base type, so the error key is absent rather than empty.</summary>
        public static GenerationEventPayload For(AnalyticsSource source, string error, string errorData = null)
        {
            var payload = error == null
                ? new GenerationEventPayload()
                : new GenerationEventErrorPayload { error = error, error_data = errorData };

            payload.source = source.ToAnalyticsValue();
            return payload;
        }
    }
}
