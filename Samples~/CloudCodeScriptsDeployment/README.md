# Cloud Code Scripts

This sample shows a simple way to deploy and invoke a Cloud Code Script.

## Using the Sample

### Setting up NodeJS

In order to have full support for your scripts in the editor, you need to set up NodeJS.
Navigate to `Preferences > Cloud Code` and make sure the path to NodeJS and NPM are correct.
Once set, click on the `Initialize JS Project` button.

### Deploying your Script

In order to deploy your script to the Cloud Code service, do the following:

1. Link your unity project in `Project Settings > Services`.
2. Select your desired environment in `Project Settings > Services > Environments`.
3. Initialize the JS project to allow for proper IDE setup and in-script parameters in `Preferences > Cloud Code > Initialize JS Project`.
4. Deploy your Cloud Code Script in the [Deployment window](https://docs.unity3d.com/Packages/com.unity.services.deployment@1.7/manual/deployment_window.html).

### Play the Scene

To test out the sample, open the `CloudCodeScripts` scene in the Editor and click `Play`.
Enter a name in the input field and click `Invoke Cloud Code Script`. The script returns `Hello {name}!`, which is logged to the Console.

You can edit `HelloScript.js` and redeploy while the scene is playing to iterate. The scene calls the script by name, `HelloScript`.

## Upgrading from the `.unitypackage` sample

Earlier versions of this sample installed as an embedded package under `Packages/com.unity.samples.ccs-deployment`. This version imports under `Assets/Samples` and keeps the same asset GUIDs, so delete the old folder before importing or the two copies will conflict.

## Troubleshooting

### Empty Deployment window

If the deployment window is empty, make sure your project is linked in `Project Settings > Services` and that you have selected an environment in `Project Settings > Services > Environments`.

### Hello undefined

If you are receiving the `Hello undefined!` message, this is likely due to your JS project not being set up.
Refer to step `3` from [Deploying your Script](#deploying-your-script).
