namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    static class ErrorPayloads
    {
        /// <summary>Success carries no fields, so the error key is absent rather than empty.</summary>
        public static EmptyPayload For(string error, string errorData = null)
        {
            return error == null
                ? new EmptyPayload()
                : new CloudCodeErrorPayload { error = error, error_data = errorData };
        }
    }
}
