# Tosca Continuous Integration Service


## Architecture

## Build
### 1. Prerequisites
- Visual Studio at least 2019
- .Net Framework 4.8
- Tosca 14.0
### 2. Tosca Commander addon and service

### 3. Agent orchestrator


## Installing Service

## API Definition

## Client usage

### examples


## Checklist


### Orchestrator
- [ ] Create Tosca Agent on demand or at least clean environment.
- [ ] Prepare Windows Tosca Agent VM Template.
- [ ] Governance of Tosca Agents.


### Project Provisioning Service
- [ ] SSL Support
- [ ] Create SQL Database
- [x] Create Project from defintion and subset
- [x] Create Temporal Project from defintion and subset
- [ ] Create Temporal Project from Database.
- [x] assign Group permissions to viewer/admin role. 
- [ ] Auth control project creation
- [x] Auth control test execution
- [ ] Document installation and configuration.
- [ ] Document build process

### Execute Test
- [x] Support for realtime test parameters.
- [X] Import results back to project.
- [x] support multiple execution list
- [x] Crash on generate Report: ```An attempt was made to call a function which is not covered by the license. ``` it works when run without Visual studio.
- [x] Ability to download reports and artifacts like screenshots.


### Client
- [ ] Request Tosca Agent clean environment
- [ ] Implement Tosca CI Service API to create project and execute tests.
- [ ] Document client usage.
- [ ] Document building.
- [ ] Tosca Project example QuickStarter Template.