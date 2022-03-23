# toscaci
Tosca CLI tool and a rest API service that improve the interaction between CI Tools and Tosca in a clean and scalable way.

[![Build](https://github.com/Boehringer-Ingelheim/toscaci/actions/workflows/build.yml/badge.svg)](https://github.com/Boehringer-Ingelheim/toscaci/actions/workflows/build.yml)

## Table of content
- [History](#History)
- [Features](#Features)
- [Requirements](#Requirements)
- [Architecture](#Architecture)
- [Getting started](#Getting-started)
  - [CLI tool](#Using-CLI-tool)
  - [CIService](#Configure-CIService)
- Development
  - [CLI tool](./client/README.md)
  - [CIService](./server/Readme.md)
- Known Issues
  - [Usage Issues](#Usage-Issues)


## History
If you have ever tried to integrate Tosca with a modern CI enterprise platform you will notice lot of limitations on how that can be done, for instance:
- There is no easy way to create Projects via CPI or API.
- Test definitions are kept in a DB, dificulting the portability of tosca projects.
- There is no support for test runtime parameters like the url of the application under testing.
- It is not git friendly.
- The Tosca concurrency model and locking system causes CI fails if users forget to check in tosca project.
- The Tosca VM agent pinned is with a User Project, hard test infrastructure resources scalability.

This project has been conceived to fix all this issues and make the integration of Tosca with your Continuous Integration pipelines as easy as possible.

## Features
- [x] Inject runtime parameters to tosca, no need to define test parameters on tosca test definition, just use a config parameter on tosca.
- [x] Execute multiple execution list based on filtering by tosca object properties
- [x] Execute tests from files (subset/project definition) or from connection string
- [ ] Select free tosca executor to run the test
- [x] Generate Junit file from test results
- [x] Generate Tosca Reports
- [x] Get all the artifacts generated on the test process   

## Requirements
- Tosca Commander 15.0 installed

## Architecture
This product is based in three components:
* Windows Rest API Service (CIService)
* Tosca Commander Addon
* Multiplatform CLI Tool (toscactl)


![toscaci architecture](./architecture/architecture_toscaci.png)


## Getting started
To be able to use this tool you first need to understand how to use the toscactl and configure the CIService:

## Using CLI tool
Get the cli tool from https://github.com/Boehringer-Ingelheim/toscaci/releases

### tosca-suite file configuration
A test suite is defined creating a file with the given format: ```tosca-<suiteName>.json```
```
{
  "agent": {
    "hostname": "http://toscaCIServiceURL:8080"
  },
  "testSuite": {
    "parameters": {      
        "myParam":"myValue",
        "myParam2":"myValue"
    },
    "selectors": {
      "TestType": "installation",
      "TestBranch": "master"
    },
    "timeout": 120,
    "videoRecord": true,
    "reports": [ "ToscaIntegrationReport","myOtherReport" ]
  }
}
```
| property | value | description |
|---|---|---|
| agent.hostname  | http://url:8080  |  this parameter indicates to which tosca ciservice node tests will be executed |
| testSuite.parameters | "myparam":"myvalue"  | you can inject runtime parameters that will be seen as config parameters in tosca, typical usage is for URL of application under test. |
| testSuite.timeout | 120 | Max time allowed for the test suite complete, this timeout includes wait for node being free, value count in minutes. |
| testSuite.videoRecord | true | A video of the agent screen will be recorded, archived as artifact. |
| testSuite.selectors | "mySelector":"myValue" | you can filter which execution lists will be triggered as part of your testSuite execution |
| testSuite.reports | "myToscaReport" | Name of the tosca report design to execute to render a report in PDF |





### toscactl test run action

```
Usage:
  toscactl run [agent selectors] [suite selector] [suite parameters] [testSuite] [flags]

Flags:
      --agent-hostname string       Tosca Agent where to run the test execution
      --agent-selector key=value    Tosca Agent where to run the test execution
      --from-connection string      Create Workspace from existing Tosca MSSQL Database
      --from-path string            Create Workspace from file definition (Workspace definition (.tpr) and subset (.tsu) are needed in the path (default "src/tosca")
  -h, --help                        Help for run
      --suite-parameter key=value   Runtime Test parameter to inject on your Execution List
      --suite-report name           Tosca Reports Name to generate after test execution
      --suite-selector key=value    Select Execution Lists  to run by properties

Global Flags:
      --config string            config file path, accept Environment Variable TOSCA_CONFIG (default is $HOME/.tosca-config.yaml)
  -s, --orchestratorURL string   Tosca Orchestrator URL (default "http://localhost:8080")
      --password string          Tosca Server password
      --username string          Tosca Server username
  -v, --verbose                  verbose output
  -w, --workingDir string        Working Directory where entities and results are expected
```

an example could be ```toscactl run --suite-parameter URL=http://google.com acceptance```


## Configure CIService
To install CIService you need to follow the following steps:
1. Get the CIService from from https://github.com/Boehringer-Ingelheim/toscaci/releases files ```toscaci-service-windows-amd64.zip``` and ```toscacommander-addon-windows-amd64.zip```
2. Unzip ```toscacommander-addon-windows-amd64.zip``` on ```C:\Program Files (x86)\TRICENTIS\Tosca Testsuite\ToscaCommander```
3. Unzip ```toscaci-service-windows-amd64.zip``` in your prefered place
4. Open the unziped folder and run CIService.exe

5. If you want the application to run on Windows startup we recommend to create a Windows Scheduled Task.
6. In addition, enable a Windows user autologon on startup.

Unfortunately, tosca requires full UI interaction. Because of this it cannot be run as a Windows service.

## Usage Issues
Depending of the Windows Security Policies could be that the CIAddin.dll is not properly loaded, ensure the DLL is not blocked, otherwise the custom Addin won't be loaded on Tosca Commander and make the CIService fails.
