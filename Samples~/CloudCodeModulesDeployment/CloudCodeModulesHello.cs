using System;
using System.Threading.Tasks;
using HelloSample.Client;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UIElements;

// Client side of the sample. It signs a player in and calls the module through the generated
// HelloSampleClient. All game logic (the count, the greeting text) lives in the module.
class CloudCodeModulesHello : MonoBehaviour
{
    [SerializeField]
    UIDocument m_Document;

    Button m_Button;
    TextField m_InputField;
    Label m_ResultLabel;
    Button m_ScheduleButton;
    IntegerField m_DelayField;
    Label m_StatusLabel;

    // Module calls need a signed-in player, so the buttons stay disabled until sign-in is done.
    async void Start()
    {
        InitializeUi();

        m_Button.SetEnabled(false);
        m_ScheduleButton.SetEnabled(false);

        await InitializeServices();
        await SignInAnonymously();

        m_Button.SetEnabled(true);
        m_ScheduleButton.SetEnabled(true);
    }

    void InitializeUi()
    {
        m_Button = m_Document.rootVisualElement.Q<Button>("InvokeCloudCodeButton");
        m_InputField = m_Document.rootVisualElement.Q<TextField>("NameInput");
        m_ResultLabel = m_Document.rootVisualElement.Q<Label>("ResultLabel");
        m_Button.clickable.clicked += async () => await CallModuleEndpoint();
        m_ScheduleButton = m_Document.rootVisualElement.Q<Button>("ScheduleGreetingButton");
        m_DelayField = m_Document.rootVisualElement.Q<IntegerField>("DelaySeconds");
        m_StatusLabel = m_Document.rootVisualElement.Q<Label>("StatusLabel");
        m_ScheduleButton.clickable.clicked += async () => await ScheduleGreeting();
    }

    static async Task InitializeServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }
    }

    // Anonymous sign-in gives this device a stable player ID, which is the key Cloud Code stores the
    // module's per-player state under. A real game would usually link it to a platform account.
    static async Task SignInAnonymously()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    async Task CallModuleEndpoint()
    {
        m_Button.SetEnabled(false);

        try
        {
            // The player ID becomes the request's scope, so the module runs against this player's state.
            var client = new HelloSampleClient(AuthenticationService.Instance.PlayerId);
            // Typed by the source generator from the module's (string name) parameter.
            var result = await client.SayHello(m_InputField.text);
            m_ResultLabel.text = $"Cloud Response: {result}";
            Debug.Log(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not call module endpoint. Please make sure the module has been deployed from the Deployment window. Original error: {e}");
        }
        finally
        {
            m_Button.SetEnabled(true);
        }
    }

    // A module cannot push to the client, so the scene polls until the timer has fired.
    async Task ScheduleGreeting()
    {
        m_ScheduleButton.SetEnabled(false);

        try
        {
            var client = new HelloSampleClient(AuthenticationService.Instance.PlayerId);
            var delay = Mathf.Clamp(m_DelayField.value, 1, 3600);
            var timerId = await client.ScheduleGreeting(m_InputField.text, delay);
            m_StatusLabel.text = $"Timer Status: scheduled ({timerId}), waiting {delay} s...";

            string status;
            do
            {
                await Task.Delay(1000);
                // Stop if the object was destroyed (e.g. play mode exited) while awaiting.
                if (this == null)
                {
                    return;
                }

                status = await client.GetGreetingStatus();
                m_StatusLabel.text = $"Timer Status: {status}";
            }
            while (status.StartsWith("Pending", StringComparison.Ordinal));
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not schedule the greeting. Please make sure the module has been deployed from the Deployment window. Original error: {e}");
        }
        finally
        {
            m_ScheduleButton.SetEnabled(true);
        }
    }
}
