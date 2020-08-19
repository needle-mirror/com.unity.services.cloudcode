# Cloud Code SDK

## Getting Started

To get started with the Cloud Code SDK:

* Install the version of the package you wish using Package Manager
    * You'll need a [scoped registry](https://docs.unity3d.com/Manual/upm-scoped.html) set up to access the preview package: 
        - URL: `https://artifactory.prd.it.unity3d.com/artifactory/api/npm/upm-candidates`
        - Scope: `com.unity.services`
* Sign in to your cloud project using the Services window in Unity
* Implement the Authentication sign in flow at a convenient point in your application.

**Note**: The Cloud Code SDK requires that an authentication flow from the Authentication SDK has been completed prior to using any of the Cloud Code APIs, as a valid player ID and access token are required to access the Cloud Code services. This can be achieved with the following code snippet for anonymous authentication, or see the documentation for the Authentication SDK for more details and other sign in methods:

```cs
Authentication.SignInAnonymously();
```

## Using the SDK

Use of the Cloud Code SDK requires authoring one or more cloud functions in the [Cloud Code dashboard](https://dashboard.unity3d.com/cloud-code).

### CallEndpointAsync

```
/// <summary>
/// Calls a Cloud Code function.
/// </summary>
/// <param name="function">Cloud Code function to call</param>
/// <param name="args">Arguments for the cloud code function</param>
/// <typeparam name="TRequest">Class with fields for each argument to provide to the Cloud Code function</typeparam>
/// <typeparam name="TResult">Serialized from JSON returned by Cloud Code</typeparam>
/// <returns></returns>
public static async Task<TResult> CallEndpointAsync<TRequest, TResult>(string function, TRequest args)
```

#### Example Use

```
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;


class Example : MonoBehaviour {
    public Text text;


    class RequestType {
        public int a;
        public int b;
    }

    class ResultType {
        public int value;
    }


    private IEnumerator UpdateTextWhenFinished(Task<ResultType> task)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        text.text = task.Result.value.ToString();
    }

    // fired from a button or similar
    public void CallMethod()
    {
        IAsyncOperation auth = new Unity.Services.Core.AsyncOperation();
        ((Unity.Services.Core.AsyncOperation)auth).Succeed();
        if (!Authentication.IsSignedIn)
        {
            auth = Authentication.SignInAnonymously();
        }

        var task = Task.Run<ResultType>(async () =>
        {
            await auth;

            var request = new RequestType {a = 1, b = 2};
            return await CloudCode.CallEndpointAsync<RequestType, ResultType>("add", request);
        });

        StartCoroutine(UpdateTextWhenFinished(task));
    }
}
```
