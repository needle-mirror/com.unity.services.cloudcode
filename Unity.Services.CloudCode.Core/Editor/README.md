This package provides a set of classes and interfaces for authoring a Cloud Code Module:

- Add the CloudCodeFunctionAttribute to a method to expose it via Cloud Code
- Add IExecutionContext as a parameter to get useful context information, including authentication tokens (optional)
- Implement ICloudCodeSetup to configure dependency injection (optional)

See https://docs.unity.com/cloud-code/en/manual for further documentation