# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [2.9.0] - 2024-10-29
### Changed
- Updated the minimum supported Editor version to 2021.3.

### Fixed
- Fixed Help URLs for Cloud Code Module and Cloud Code Script
- Fixed inspector loading for service assets, below Unity 6
- Fixed an issue that might cause the CloudCode scripts inspector to spam calls to the admin API
- Fixed an issue that causes the progress bar to revert during Cloud Code Module deployment

## [2.8.1] - 2024-10-29

### Fixed
- Fixed compatibility wih Deployment 1.3

## [2.8.0] - 2024-10-18

### Added
- View in Deployment Window button in `.ccmr` and `.js` files, dependent on Deployment package version 1.4.0.
- View in Dashboard button in inspector for `.ccmr` and `.js` files.
- View in Dashboard context menu in Deployment Window for `.ccmr` and `.js` files.
- Add `Open Solution` button to `.ccmr` inspector.
- Add Enum support for Cloud Code Bindings generation.

### Fixed
- Fixed support for various primitive types in Cloud Code Modules binding generation
- In-script parameters analysis throws an exception in Unity 6
- `Browse...` button in `.ccmr` inspector now opens the current solution folder properly.
- Fixed Cloud Code Binding generation of primitive types
- Binding Generation will attempt to run in the latest available runtime.
  - This can be disabled with CLOUD_CODE_AUTHORING_DISABLE_VERSION_DETECT flag

## [2.7.1] - 2024-06-10

### Added
- A MessageBytesReceived callback has been added to the available subscription event callbacks
- Adding service registration to the core services registry
- Adding service access through the core services registry (`UnityServices.Instance.GetCloudCodeService()`)
- Added a button to browse your files when choosing a path for a Cloud Code Module

### Changed
- The MessageReceived callback will no longer be fired upon receiving bytes via the event subscription

### Fixed
- Bindings generation is broken when ILogger dependency injection is used
- Cloud Code modules now cleans up compilation artifacts after deploying or generating bindings
- Cloud Code runtime timeout increased to 30 seconds
- Moved create Cloud Code Asset menu items under "Services"

## [2.6.2] - 2024-05-03

### Added
- Added privacy manifest

### Fixed
- An issue that would cache Npm and Node path at startup instead of reading them from the settings

## [2.6.1] - 2024-03-25

### Fixed
- Fixed JS script import when Node project is not initialized

## [2.6.0] - 2024-03-21

### Added
- Improved in-script parameter parsing error feedback
- Added references of the latest javascript services SDKs for autocompletion
- Cloud Code bindings generation

### Fixed
- Fixed error when selecting CloudCodeModuleReference assets in the Project window

## [2.5.1] - 2023-10-19

### Fixed
- Fixed Cloud Code C# modules authoring support for solutions with multiple projects.

## [2.5.0] - 2023-09-21

### Added
- Editor support for Cloud Code C# Modules deploy.

## [2.4.0] - 2023-05-12

### Added
- Added subscription methods for player-specific and project-wide push messages from Cloud Code C# Modules.

## [2.3.2] - 2023-03-24

### Changed
- Increased timeout from 10 seconds to 25 seconds.
- Scripts are no longer cached, which would previously prevent deployments without a local change.

### Fixed
- When using JS Bundling, modifying an imported file will enable re-deployment for the main script.
- Selecting multiple .js files using in-script parameters, the inspector will now remain disabled for editing.
- When selecting multiple .js files or deployment definitions, the inspector will now properly refer to their actual types.
- Deployable assets (.js) not appearing on load in the Deployment Window with Unity 2022+.

## [2.3.1] - 2023-03-21

### Fixed
- Fixed an issue with `null` paths on cloud code scripts.

## [2.3.0] - 2023-03-14

### Added
- Added the ability to bundle JS scripts that are deployed from the editor.
- Added CallModuleEndpointAsync to the Cloud Code Service for calling C# Modules

## [2.2.4] - 2023-02-07

### Fixed
- Fixed corrupted npm libraries used for services. 

## [2.2.2] - 2022-12-07

### Fixed
- Missing logs in some failure cases are now handled
- Added more verbose logging for diagnostics behind a preprocessor directive 

## [2.2.1] - 2022-12-07

### Fixed
- Duplicate file in the deployment window now appear as a warning instead of an error
- Updated the com.unity.services.deployment.api version to be used for config as code

## [2.1.2] - 2022-10-27

### Fixed
- Rate limiting triggered in some cases

## [2.1.1] - 2022-09-27

### Fixed
- Void type now allowed as return type for CloudCode scripts
- Removed requirement for function arguments when calling an endpoint. Now, it's possible to provide either null or omit them

### Added
- Integration with the `Deployment`  package for config-as-code which allows to edit and configure
CloudCode scripts directly from the editor

## [2.0.1] - 2022-06-13

### Fixed
- Missing XmlDoc on public ICloudCodeService interface

## [2.0.0] - 2022-06-01

- Moving out of Beta!

## [2.0.0-pre.4] - 2022-04-16

### **Breaking Changes**:
- The interface provided by CloudCode has been replaced by CloudCodeService.Instance, and should be accessed from there instead. The old API will be removed in an upcoming release
- Cloud Code methods now take a Dictionary<string, object> containing the script parameters instead of an object with named fields (the dictionary can still be null if the script does not have any parameters). The old API will be removed in an upcoming release
- When a rate limit error occurs, a specific CloudCodeRateLimitedException will now be thrown which includes the RetryAfter value (in seconds)
- Clarity and structure of some error messages has been improved
- Some classes that were accidentally made public are now internal

### Fixed
- Installation and Analytics IDs not being forwarded to Cloud Code server (causing incorrect tracking downstream)

### Added
- Project Settings tab with link to Cloud Code dashboard
- Cloud Code exceptions now include a Reason enum which is machine-readable

## [1.0.0-pre.7] - 2021-12-07

### Fixed
- NullReferenceException being thrown instead of some service errors
- Documentation URL in package manifest
- Deprecated some elements that should not have been public, these will be deleted in a later release

## [1.0.0-pre.6] - 2021-09-22
- Fixes a crash that could occur with certain exceptions returned from the API

### Known Issues
- When a cloud code function that hasn't been published yet is called from the SDK, the SDK will throw a Null Reference Exception rather than a normal CloudCodeException

## [1.0.0-pre.5] - 2021-09-17
- No longer throws on null function parameter values
- No longer throws on null api return values
- Corrected exception types
- Removed tests from public package
- Fixed code examples in documentation

## [1.0.0-pre.4] - 2021-08-19
- Updated readme and changelog to be more descriptive.
- Updated package description to better highlight the usages of Cloud Code.

## [1.0.0-pre.1] - 2021-08-10

- Updated documentation in preperation for release.
- Updated dependencies (Core and Authentication) to latest versions.
- Updated internals for more stability.
- Added a new API that returns string, in order to support custom user serialization of return values.

## [1.0.0-pre.1] - 2021-08-10

- Updated documentation in preperation for release.
- Updated dependencies (Core and Authentication) to latest versions.
- Updated internals for more stability.
- Added a new API that returns string, in order to support custom user serialization of return values.

## [0.0.3-preview] - 2021-06-17

- Updated depedencies of Core and Authentication to latest versions.

## [0.0.2-preview] - 2021-05-27

- Update documentation and license

## [0.0.1-preview] - 2021-05-10

### Package Setup for Cloud Code.

- Creating the package skeleton.
