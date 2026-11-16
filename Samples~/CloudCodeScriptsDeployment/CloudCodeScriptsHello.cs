using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UIElements;

class CloudCodeScriptsHello : MonoBehaviour
{
    [SerializeField]
    UIDocument m_Document;

    Button m_Button;
    TextField m_InputField;

    async void Start()
    {
        InitializeUi();

        m_Button.SetEnabled(false);

        await InitializeServices();
        await SignInAnonymously();

        m_Button.SetEnabled(true);
    }

    void InitializeUi()
    {
        m_Button = m_Document.rootVisualElement.Q<Button>("InvokeCloudCodeButton");
        m_InputField = m_Document.rootVisualElement.Q<TextField>("NameInput");
        m_Button.clicked += async () => await CallScriptEndpoint();
    }

    static async Task InitializeServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }
    }

    static async Task SignInAnonymously()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    async Task CallScriptEndpoint()
    {
        m_Button.SetEnabled(false);

        try
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("name", m_InputField.text);
            var result = await CloudCodeService.Instance.CallEndpointAsync<string>("HelloScript", arguments);
            Debug.Log(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not call the script. Please make sure HelloScript has been deployed from the Deployment window. Original error: {e}");
        }
        finally
        {
            m_Button.SetEnabled(true);
        }
    }
}
