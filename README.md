# tosca-integration
Tosca cli tool and a rest API service that improve interaction of CI Tools with Tosca in a clean and escalable way.

[![Build](https://github.com/segator/toscaci/actions/workflows/build.yml/badge.svg)](https://github.com/segator/toscaci/actions/workflows/build.yml)

## History
if you tried to integrate Tosca on a modern CI enterprise platform you will notice lot of limitations on how Tosca can be integrated, some of the issues:
- No easy way to create Projects via cli or api.
- Test definitions are keep on a DB, so dificult portability of the tosca projects
- No support for test runtime parameters like url of application under test
- No git friendly
- Tosca concurrency model and locking system causes CI fails if users forgot to check in tosca project.
- Tosca VM Agent pinned with User Project, hard test infrastructure resources escalability.

This project has been created to fix all this issues and make the integration with your Continuous Integration pipelines as easy as possible.

## Client Usage
```
This application Allow you to manage Tosca remotely

Usage:
  toscactl [command]

Available Commands:
  help        Help about any command
  project     Workspace operations
  run         Execute a Tosca Test Suite
  version     Show Application version

Flags:
      --config string            config file path, accept Environment Variable TOSCA_CONFIG (default is $HOME/.tosca-config.yaml)
  -h, --help                     help for toscactl
  -s, --orchestratorURL string   Tosca Orchestrator URL (default "http://localhost:8080")
      --password string          Tosca Server password
      --username string          Tosca Server username
  -v, --verbose                  verbose output
  -w, --workingDir string        Working Directory where entities and results are expected

Use "toscactl [command] --help" for more information about a command.
```

### Project actions
```
Workspace operations like create or destroy

Usage:
  toscactl project [command]

Available Commands:
  create      Create tosca project

Flags:
  -h, --help   help for project

Global Flags:
      --config string            config file path, accept Environment Variable TOSCA_CONFIG (default is $HOME/.tosca-config.yaml)
  -s, --orchestratorURL string   Tosca Orchestrator URL (default "http://localhost:8080")
      --password string          Tosca Server password
      --username string          Tosca Server username
  -v, --verbose                  verbose output
  -w, --workingDir string        Working Directory where entities and results are expected
```

### Run actions
```
Usage:
  toscactl run [agent selectors] [suite selector] [suite parameters] [testSuite] [flags]

Flags:
      --agent-hostname string                  Tosca Agent where to run the test execution
      --agent-selector key=value    Tosca Agent where to run the test execution
      --from-connection string                 Create Workspace from existing Tosca MSSQL Database
      --from-path string                       Create Workspace from file definition (Workspace definition (.tpr) and subset (.tsu) are needed in the path (default "src/tosca")
  -h, --help                                   help for run
      --suite-parameter key=value   Runtime Test parameter to inject on your Execution List
      --suite-report name                 Tosca Reports Name to generate after test execution
      --suite-selector key=value    Select Execution Lists  to run by properties

Global Flags:
      --config string            config file path, accept Environment Variable TOSCA_CONFIG (default is $HOME/.tosca-config.yaml)
  -s, --orchestratorURL string   Tosca Orchestrator URL (default "http://localhost:8080")
      --password string          Tosca Server password
      --username string          Tosca Server username
  -v, --verbose                  verbose output
  -w, --workingDir string        Working Directory where entities and results are expected
```