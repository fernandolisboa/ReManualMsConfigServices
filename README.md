# ReManualMsConfigServices
Outline of an application to handle the status of Windows Services. Initially created to return services to the "Manual" status after disabling it via MsConfig.

Contextualizing: When disabling a service in MsConfig in order, for example, to gain performance at OS startup, the status of the service is changed to "Disabled" (in services.msc). As a result, there may be some unwanted effects, such as the inability to open an application that has a mandatory service.
