package tosca

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"github.com/avast/retry-go"
	log "github.com/sirupsen/logrus"
	"io/ioutil"
	"math/rand"
	"net/http"
	"os"
	"path"
	"time"
	"toscactl/entity"
	"toscactl/helper"
)
const(
    APIExecution = "execution"
    APIExecutionStatus = "execution/%s"
	APIExecutionXUnit = "execution/%s/xunit"
	APIExecutionReportList = "execution/%s/report"
	APIExecutionReport = "execution/%s/report/%s"
	APIExecutionArtifactList = "execution/%s/artifact"
	APIExecutionArtifact = "execution/%s/artifact/%s"

	//Default values
	defaultResultPath="build/Test"
	defaultSourcePath="src/tosca"
	defaultTimeout=time.Duration(30)

	//Execution Status
	executionPending                ExecutionStatus = "Pending"
	executionPreparing              ExecutionStatus = "Preparing"
	executionExecuting              ExecutionStatus = "Executing"
	executionImportingResults       ExecutionStatus = "ImportingResults"
	executionWaitingToImportResults ExecutionStatus = "WaitingToImportResults"
	executionGeneratingReports      ExecutionStatus = "GeneratingReports"
	executionCompleted              ExecutionStatus = "Completed"
	executionFailed                 ExecutionStatus = "Failed"
)

var AlreadyRunningExecution = errors.New("already Running Execution on the node")
type ExecutionStatus string
type KeyValue struct {
	Key string
	Value string
}

type TestExecutorConfiguration struct {
	WorkspaceSessionID string     `json:"sessionID"`
	Selectors          []KeyValue `json:"selectors,omitempty"`
	Parameters         []KeyValue `json:"parameters,omitempty"`
	Reports            []string   `json:"reports,omitempty"`
	buildDirectory     string     `json:"-"`
	xUnitPath          string     `json:"-"`
	artifactsPath      string     `json:"-"`
	reportsPath        string     `json:"-"`
	workspace          *Workspace `json:"-"`
	hostname           string     `json:"-"`
	executionID        string     `json:"-"`
}
type ExecutionFiles struct {
	Id   string
	Path string
	Size int64
}
type TestExecutionResponse struct {
	Error       string
	ExecutionID string
	Status      ExecutionStatus
	Files       []ExecutionFiles
}

func (c *TestExecutorConfiguration) load(config TestSuiteConfiguration) {
	for k,v := range  config.TestSuite.Selectors {
		c.Selectors = append(c.Selectors,KeyValue{
			Key:   k,
			Value: v,
		})
	}

	for k,v := range  config.TestSuite.Parameters{
		c.Parameters = append(c.Parameters,KeyValue{
			Key:   k,
			Value: v,
		})
	}

	for _,report := range  config.TestSuite.Reports {
		c.Reports = append(c.Reports,report)
	}
}

// LoadTestSuiteConfiguration expect a testSuiteName file with format tosca-{testSuiteName}.json on workingDir
// parses the file and return TestSuiteConfiguration or Error
func LoadTestSuiteConfiguration(testSuitePath string,name string) (testSuite *TestSuiteConfiguration,err error)  {
	testSuite = &TestSuiteConfiguration{
		Name:         name,
		ResultFolder: defaultResultPath,
		Project:      ProjectDefinition{
			SourcePath: defaultSourcePath,
		},
		Agent: TestAgentConfiguration{},
		TestSuite:    TestExecutionConfiguration{
			Parameters: entity.KeyValue{},
			Selectors:  entity.KeyValue{},
			Timeout:    defaultTimeout,
		},
	}

	executionTypeFile, err := os.Open(testSuitePath)
	if err != nil {
		return nil,fmt.Errorf("error when opening TestExecutionConfiguration file: %s",err)
	}
	defer executionTypeFile.Close()
	executionTypeBytes, err := ioutil.ReadAll(executionTypeFile)
	if err!=nil{
		return nil,fmt.Errorf("error when reading execution type file: %s",err)
	}
	if err=json.Unmarshal(executionTypeBytes,&testSuite);err!=nil{
		return nil,fmt.Errorf("error when unmarshalling json from execution type file: %s",err)
	}
	return testSuite,nil
}




func (t *Provider) RunTestSuite(suiteConfig TestSuiteConfiguration,ctx context.Context) ( err error) {
	log.Infof("Preparing to execute Test Suite %s",suiteConfig.Name)
	timeoutContext,_:=context.WithTimeout(ctx,suiteConfig.TestSuite.Timeout*time.Minute)

	buildDirPath :=path.Join(t.config.WorkingDir, suiteConfig.ResultFolder,suiteConfig.Name)
	_, err = os.Stat(buildDirPath)
	if !os.IsNotExist(err) {
		log.Warnf("Build directory %s is not empty, deleting...",buildDirPath)
		if err:=os.RemoveAll(buildDirPath);err!=nil{
			return err
		}
	}
	executorSuiteConfig:= &TestExecutorConfiguration{
		buildDirectory: buildDirPath,
		xUnitPath:      path.Join(buildDirPath, fmt.Sprintf("%s-junit.xml", suiteConfig.Name)),
		artifactsPath:  path.Join(buildDirPath,"Artifacts"),
		reportsPath:    path.Join(buildDirPath,"Reports"),
	}

	if suiteConfig.Agent.Selectors!=nil && suiteConfig.Agent.Selectors.Length()>0 {
		agentController,err:=t.deployAgentNode(suiteConfig.Agent.Selectors,timeoutContext)
		if err!=nil {
			return err
		}
		//TODO set hostname
		executorSuiteConfig.hostname=agentController.hostname
		defer t.destroyAgentNode(agentController,timeoutContext)
		//TODO get hostname and VM Controller
	}else{
		executorSuiteConfig.hostname=suiteConfig.Agent.Hostname
	}
	log.Infof("Selected Node %s",suiteConfig.Agent.Hostname)
	log.Infof("Preparing Workspace...")

	executorSuiteConfig.load(suiteConfig)
	executorSuiteConfig.workspace,err=t.prepareWorkspace(suiteConfig,timeoutContext)
	if err!=nil {
		return err
	}
	executorSuiteConfig.WorkspaceSessionID=executorSuiteConfig.workspace.SessionID
	if err!=nil{
		return err
	}
	defer t.DeleteWorkspace(executorSuiteConfig.workspace.SessionID,timeoutContext)
	log.Infof("Workspace %s ready", executorSuiteConfig.workspace.SessionID)

	log.Infof("Requesting Test %s on workspace %s",suiteConfig.Name,executorSuiteConfig.workspace.SessionID)
	if err := t.requestTestExecution(executorSuiteConfig, timeoutContext);err!=nil{
		return err
	}

	log.Infof("Test %s placed(%s) on workspace %s", suiteConfig.Name, executorSuiteConfig.executionID, executorSuiteConfig.workspace.SessionID)
	if err := t.waitUntilCompletion(timeoutContext, executorSuiteConfig); err != nil {
		return err
	}

	log.Infof("Test %s completed, downloading xunit results",suiteConfig.Name)
	if err := t.getXUnit(executorSuiteConfig,timeoutContext);err != nil {
		return err
	}

	log.Infof("downloading Reports")
	err = t.getReports(executorSuiteConfig,timeoutContext)
	if err != nil {
		return err
	}
	log.Infof("downloading artifacts")
	err = t.getArtifacts(executorSuiteConfig,timeoutContext)
	if err != nil {
		return err
	}

	//TODO Check if test fails
	log.Infof("Test Suite %s completed,results saved on %s",suiteConfig.Name,executorSuiteConfig.buildDirectory)
	return nil
}

func (t *Provider) requestTestExecution(executorSuiteConfig *TestExecutorConfiguration, timeoutContext context.Context)  error {
	err := retry.Do(func() error {
		return t.triggerExecution(executorSuiteConfig, timeoutContext)
	}, retry.RetryIf(func(err error) bool {
		if err == AlreadyRunningExecution {
			log.Warn("other execution still running, waiting for agent being free")
			return true
		}
		return false
	}), retry.Delay(10*time.Second), retry.Context(timeoutContext))
	if err != nil {
		return  err
	}
	return err
}

func (t *Provider) waitUntilCompletion(ctx context.Context, executorSuiteConfig *TestExecutorConfiguration) error {
	for {
		select {
		case <-ctx.Done():
			t.cancelTestExecution(executorSuiteConfig, ctx)
			return context.Canceled
		case <-time.After(15 * time.Second):
			status, err := t.checkStatus(executorSuiteConfig, ctx)
			if err != nil {
				log.Errorf("%v", err)
			} else {
				log.Infof("Execution %s is in %s state", executorSuiteConfig.executionID, status)
			}
			switch status {
			case executionCompleted:
				return nil
			case executionFailed:
				return err
			}
		}
	}
	return nil
}



func (t *Provider) triggerExecution(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) error {
	b,err:=json.Marshal(testExecutorConfig)
	if err!=nil{
		return err
	}
	executionURL,err:=t.getOrchestratorURL(APIExecution)
	if err!=nil{
		return err
	}
	r, err := http.NewRequestWithContext(ctx,"POST",executionURL , bytes.NewBuffer(b))
	if err!=nil{
		return err
	}
	if t.config.Username!="" && t.config.Password !=""{
		r.SetBasicAuth(t.config.Username,t.config.Password)
	}
	r.Header.Add("Content-Type", "application/json")
	client := &http.Client{}
	response,err:=client.Do(r)
	if err!=nil{
		return err
	}
	defer response.Body.Close()
	byteResponse,err :=ioutil.ReadAll(response.Body)
	if err!=nil {
		return err
	}
	if response.StatusCode==http.StatusInternalServerError {
		return fmt.Errorf(string(byteResponse))
	}
	if response.StatusCode == http.StatusConflict {
		return AlreadyRunningExecution
	}
	executionResponse:= &TestExecutionResponse{}
	if err:=json.Unmarshal(byteResponse,executionResponse);err!=nil {
		return err
	}
	if executionResponse.Error !=""{
		return fmt.Errorf(executionResponse.Error)
	}
	testExecutorConfig.executionID = executionResponse.ExecutionID
	return nil
}


func (t *Provider) getArtifacts(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) error {
	return t.getToscaFiles(testExecutorConfig,APIExecutionArtifactList,APIExecutionArtifact,testExecutorConfig.artifactsPath,ctx)
}

func (t *Provider) getReports(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) error {
	return t.getToscaFiles(testExecutorConfig,APIExecutionReportList,APIExecutionReport,testExecutorConfig.reportsPath,ctx)
}
func (t *Provider) getXUnit(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) error {
	xunitURL,err:=t.getOrchestratorURL(fmt.Sprintf(APIExecutionXUnit,testExecutorConfig.executionID))
	if err!=nil{
		return err
	}
	if err:=helper.DownloadFile(xunitURL,testExecutorConfig.xUnitPath,ctx);err!=nil{
		return err
	}
	return nil
}
func (t *Provider) getToscaFiles(testExecutorConfig *TestExecutorConfiguration,urlBase string,urldownload string,outputPath string,ctx context.Context) error {
	reportListURL,err:=t.getOrchestratorURL(fmt.Sprintf(urlBase,testExecutorConfig.executionID))
	if err!=nil{
		return err
	}
	r, err := http.NewRequestWithContext(ctx,"GET",reportListURL,nil)
	r.Header.Add("Content-Type", "application/json")
	client := &http.Client{}
	resp,err:=client.Do(r)
	defer resp.Body.Close()
	if resp.StatusCode!=http.StatusOK {
		return fmt.Errorf("error when recovering Reports %s",resp.Status)
	}
	byteResponse,err:=ioutil.ReadAll(resp.Body)
	if err!=nil{
		return err
	}
	executionResponse:= &TestExecutionResponse{}
	if err:=json.Unmarshal(byteResponse,executionResponse);err!=nil {
		return err
	}
	if executionResponse.Error !=""{
		return fmt.Errorf(executionResponse.Error)
	}
	log.Infof("%d files found",len(executionResponse.Files))
	for _,file := range executionResponse.Files {
		log.Infof("Downloading %s",file.Path)
		downloadURL,err:=t.getOrchestratorURL(fmt.Sprintf(urldownload,testExecutorConfig.executionID,file.Id))
		if err!=nil{
			return err
		}
		err=helper.DownloadFile(downloadURL,path.Join(outputPath,file.Path),ctx)
	}
	return nil
}

type agentController struct {
	hostname string
}

func (t *Provider) deployAgentNode(selector entity.KeyValue,ctx context.Context) (agentController,error) {
	//Tod@
	return agentController{},nil
}

func (t *Provider) prepareWorkspace(testSuiteConfig TestSuiteConfiguration,ctx context.Context) (*Workspace,error) {
	templateType:= CreatefromDefinition
	if testSuiteConfig.Project.SourceConnectionStringDB!=""{
		templateType = CreateFromDatabase
	}
	rndNumber := rand.Intn(1000 - 1) + 1
	workspace,err :=t.CreateProject(ProjectCreateRequest{
		SourcePath:               testSuiteConfig.Project.SourcePath,
		Name:                     fmt.Sprintf("%s%d",testSuiteConfig.Name,rndNumber),
		TemplateType:             templateType,
		TemplateConnectionString: testSuiteConfig.Project.SourceConnectionStringDB,
		DBType:                   LocalDB,
	},ctx)
	if err!=nil {
		return nil,err
	}
	return workspace,nil
}

func (t *Provider) checkStatus(config *TestExecutorConfiguration,ctx context.Context) (ExecutionStatus,error) {
	executionStatusURL,err:=t.getOrchestratorURL(fmt.Sprintf(APIExecutionStatus,config.executionID))
	if err!=nil{
		return "",err
	}
	r, err := http.NewRequestWithContext(ctx,"GET", executionStatusURL,nil)
	if err!=nil{
		return "",err
	}

	r.Header.Add("Content-Type", "application/json")
	client := &http.Client{}
	response,err:=client.Do(r)
	if err!=nil{
		return "",err
	}
	defer response.Body.Close()
	byteResponse,err :=ioutil.ReadAll(response.Body)
	if err!=nil {
		return "",err
	}
	if response.StatusCode==http.StatusInternalServerError {
		return "",fmt.Errorf(string(byteResponse))
	}
	if response.StatusCode == http.StatusNotFound {
		return "",fmt.Errorf("execution not found, agent probably dead")
	}
	executionResponse:= &TestExecutionResponse{}
	if err:=json.Unmarshal(byteResponse,executionResponse);err!=nil {
		return "",err
	}
	if executionResponse.Error !=""{
		return "",fmt.Errorf(executionResponse.Error)
	}

	return executionResponse.Status,nil
}


func (t *Provider) destroyAgentNode(controller agentController,ctx context.Context) {
	//TODO
}

func (t *Provider) cancelTestExecution(config *TestExecutorConfiguration,ctx context.Context) {
	//TOD@
}