using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode
{
    public interface ICloudCodeService
    {
        Task<string> CallEndpointAsync(string function, Dictionary<string, object> args);
        Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args);
    }

    public class CloudCodeService
    {
        public static ICloudCodeService Instance { get; internal set; }
    }
}
