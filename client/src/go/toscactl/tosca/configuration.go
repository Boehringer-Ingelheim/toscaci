package tosca

import (
	"fmt"
	"os"
	"time"
	"toscactl/entity"
)

// TestSuiteConfiguration represents Test Configuration Object for full triggerExecution execution
type TestSuiteConfiguration struct {
	Name         string                     //name of the TestSuiteConfiguration
	Project      ProjectDefinition          //Project definition
	Agent        TestAgentConfiguration     //Test Agent Configuration
	TestSuite    TestExecutionConfiguration //Tosca TestSuite Configuration
	ResultFolder string                     //Result Folder Path
}

// ProjectDefinition represents project definition sources and how to connect
type ProjectDefinition struct {
	SourcePath string                //Source Path
	SourceConnectionStringDB string  //Source Connection String
	WorkspaceUsername string         //Authentication workspace username (Only applicable when operating with Tosca external projects)
	WorkspacePassword string         //Authentication workspace password (Only applicable when operating with Tosca external projects)
}

// TestAgentConfiguration represents a selection of agent attributes or specific agent hostname
type TestAgentConfiguration struct {
	Selectors entity.KeyValue //Label Key Value to filter which agent to select
	Hostname  string          //Hostname of the agent
}

// TestExecutionConfiguration define which tests to execute in a project, which Parameters to inject on the triggerExecution runtime and which Reports to generate.
type TestExecutionConfiguration struct {
	Parameters entity.KeyValue    //runtime triggerExecution Parameters k/v
	Selectors  entity.KeyValue    //Key Value to filter which tests to execute based on Tosca Properties on an execution List
	Reports    entity.StringArray //Reports to generate
	Timeout    time.Duration      //time in Minutes to allow triggerExecution to run until mark it as failed
}

// Validate TestSuiteConfiguration values are correct.
func (s TestSuiteConfiguration) Validate() error {
	if s.Agent.Hostname == ""|| (s.Agent.Selectors != nil && s.Agent.Selectors.Length()==0) {
		return fmt.Errorf("agent-hostname or agent-selector option is mandatory")
	}

	if s.Agent.Selectors.Length()>0 && s.Agent.Hostname != ""  {
		return fmt.Errorf("agent-hostname can not be defined if agent-selector is defined, both options are exclusive")
	}

	if s.Project.SourcePath == "" && s.Project.SourceConnectionStringDB == "" {
		return fmt.Errorf("sources Path or connection String DB is mandatory")
	}

	if s.Project.SourcePath!=""   {
		if _, err := os.Stat(s.Project.SourcePath); os.IsNotExist(err) {
			return fmt.Errorf("source Path %s doesn't exists",s.Project.SourcePath)
		}
	}

	if s.Project.SourceConnectionStringDB != "" && (s.Project.WorkspaceUsername =="" || s.Project.WorkspacePassword ==""){
		return fmt.Errorf("if connection String is defined workspace username and password are mandatory")
	}

	if s.TestSuite.Selectors!=nil && s.TestSuite.Selectors.Length()==0{
		return fmt.Errorf("at least one triggerExecution selector is mandatory")
	}
	return nil
}