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
	"toscactl/test"
)
const(
    APICreateExecution    = "execution"
    APIExecution          = "execution/%s"
	APIExecutionXUnitList = "execution/%s/xunit"
	APIExecutionXUnit = "execution/%s/xunit/%s"
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
	executionCanceled               ExecutionStatus = "Canceled"

)

var AlreadyRunningExecution = errors.New("already Running Execution on the node")
var TestsFailed = errors.New("some tests has been failed")
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
// parses the file and return TestSuiteConfiguration or TestCaseError
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
		xUnitPath:      path.Join(buildDirPath,"junit"),
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
		defer func(){
			if err2:=t.destroyAgentNode(agentController);err2!=nil{
				err=fmt.Errorf("error when destroying tosca agent: please remove it by hand, %v",err2)
			}
		}()
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
	defer func(){
		log.Infof("Deleting Workspace")
		if err2:=t.DeleteWorkspace(executorSuiteConfig,context.Background());err2!=nil{
			err=fmt.Errorf("error when deleting workspace: please remove it by hand, %v",err2)
		}
	}()
	log.Infof("Workspace %s ready", executorSuiteConfig.workspace.SessionID)

	log.Infof("Requesting Test %s on workspace %s",suiteConfig.Name,executorSuiteConfig.workspace.SessionID)
	if err := t.requestTestExecution(executorSuiteConfig, timeoutContext);err!=nil{
		return err
	}

	log.Infof("Test %s placed(%s) on workspace %s", suiteConfig.Name, executorSuiteConfig.executionID, executorSuiteConfig.workspace.SessionID)
	if err := t.waitUntilCompletion(timeoutContext, executorSuiteConfig); err != nil {
		return err
	}

	log.Infof("Test %s completed",suiteConfig.Name)
	log.Infof("Downloading xunit results")
	testReports,err := t.getTestReports(executorSuiteConfig,timeoutContext)
	if err != nil {
		return err
	}

	log.Infof("Downloading Reports")
	_,err = t.getReports(executorSuiteConfig,timeoutContext)
	if err != nil {
		return err
	}

	log.Infof("Downloading artifacts")
	_,err = t.getArtifacts(executorSuiteConfig,timeoutContext)
	if err != nil {
		return err
	}
	failedTests := testReports.GetNumberFailedTests()
	testsWithErrors := testReports.GetNumberErrorsTests()
	totalTests :=testReports.GetNumberTests()
	log.Infof("Test Suite %s %d test failed, %d tests with errors, %d tests executed, results saved on %s",suiteConfig.Name,failedTests,testsWithErrors,totalTests,executorSuiteConfig.buildDirectory)
	if testReports.GetNumberFailedTests() > 0 {
		return TestsFailed
	}
	return nil
}

func (t *Provider) requestTestExecution(executorSuiteConfig *TestExecutorConfiguration, timeoutContext context.Context)  error {
	err := retry.Do(func() error {
		return t.triggerExecution(executorSuiteConfig, timeoutContext)
	}, retry.RetryIf(func(err error) bool {
		if errors.Is(err,AlreadyRunningExecution) {
			log.Warn("other execution still running, waiting for agent being free")
			return true
		}
		return false
	}), retry.Attempts(10000),retry.DelayType(retry.FixedDelay),retry.Delay(15*time.Second),retry.Context(timeoutContext))
	if err != nil {
		return  err
	}
	return err
}

func (t *Provider) waitUntilCompletion(ctx context.Context, executorSuiteConfig *TestExecutorConfiguration) error {
	for {
		select {
		case <-ctx.Done():
			log.Warnf("Canceling Test execution %s",executorSuiteConfig.executionID)
			if err:=t.cancelTestExecution(executorSuiteConfig);err!=nil{
				return err
			}
			return context.Canceled
		case <-time.After(15 * time.Second):
			status, err := t.checkStatus(executorSuiteConfig, ctx)
			log.Infof("Execution %s is in %s state", executorSuiteConfig.executionID, status)

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
	executionURL,err:=t.getAgentURL(testExecutorConfig, APICreateExecution)
	if err!=nil{
		return err
	}
	req, err := http.NewRequestWithContext(ctx,"POST",executionURL , bytes.NewBuffer(b))
	if err!=nil{
		return err
	}
	if t.config.Username!="" && t.config.Password !=""{
		req.SetBasicAuth(t.config.Username,t.config.Password)
	}
	req.Header.Add("Content-Type", "application/json")
	client := &http.Client{}
	response,err:=client.Do(req)
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


func (t *Provider) getArtifacts(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) ([]string,error) {
	return t.getToscaFiles(testExecutorConfig,APIExecutionArtifactList,APIExecutionArtifact,testExecutorConfig.artifactsPath,ctx)
}

func (t *Provider) getReports(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) ([]string,error) {
	return t.getToscaFiles(testExecutorConfig,APIExecutionReportList,APIExecutionReport,testExecutorConfig.reportsPath,ctx)
}
func (t *Provider) getTestReports(testExecutorConfig *TestExecutorConfiguration,ctx context.Context) (test.TestResults,error) {
	testResults := test.TestResults{}
	testResultFiles,err:=t.getToscaFiles(testExecutorConfig,APIExecutionXUnitList,APIExecutionXUnit,testExecutorConfig.xUnitPath,ctx)
	if err!=nil {
		return nil,err
	}
	for _, testResultFile := range testResultFiles{
		testResult, err := test.ReadTestResults(testResultFile)
		if err!=nil {
			return nil,err
		}
		testResults = append(testResults,testResult)
	}
	return testResults,nil
}

func (t *Provider) getToscaFiles(testExecutorConfig *TestExecutorConfiguration,urlBase string,urldownload string,outputPath string,ctx context.Context) ([]string,error) {
	var toscaFiles []string
	reportListURL,err:=t.getAgentURL(testExecutorConfig,fmt.Sprintf(urlBase,testExecutorConfig.executionID))
	if err!=nil{
		return nil,err
	}
	req, err := http.NewRequestWithContext(ctx,"GET",reportListURL,nil)
	if err!=nil{
		return nil,err
	}
	req.Header.Add("Content-Type", "application/json")
	client := &http.Client{}
	resp,err:=client.Do(req)
	if err!=nil{
		return nil,err
	}
	defer resp.Body.Close()
	if resp.StatusCode!=http.StatusOK {
		return nil,fmt.Errorf("error when recovering Reports %s",resp.Status)
	}
	byteResponse,err:=ioutil.ReadAll(resp.Body)
	if err!=nil{
		return nil,err
	}
	executionResponse:= &TestExecutionResponse{}
	if err:=json.Unmarshal(byteResponse,executionResponse);err!=nil {
		return nil,err
	}
	if executionResponse.Error !=""{
		return nil,fmt.Errorf(executionResponse.Error)
	}
	log.Infof("%d files found",len(executionResponse.Files))
	for _,file := range executionResponse.Files {
		log.Infof("Downloading %s",file.Path)
		downloadURL,err:=t.getAgentURL(testExecutorConfig,fmt.Sprintf(urldownload,testExecutorConfig.executionID,file.Id))
		if err!=nil{
			return nil,err
		}
		filePath := path.Join(outputPath,file.Path)
		err=helper.DownloadFile(downloadURL,filePath,ctx)
		toscaFiles = append(toscaFiles,filePath)
	}
	return toscaFiles,nil
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
	rand.Seed(time.Now().UnixNano())
	rndNumber := rand.Intn(1000 - 1) + 1
	workspace,err :=t.createProject(ProjectCreateRequest{
		SourcePath:               testSuiteConfig.Project.SourcePath,
		Name:                     fmt.Sprintf("%s%d",testSuiteConfig.Name,rndNumber),
		TemplateType:             templateType,
		TemplateConnectionString: testSuiteConfig.Project.SourceConnectionStringDB,
		DBType:                   LocalDB,
	},testSuiteConfig.Agent.Hostname,ctx)
	if err!=nil {
		return nil,err
	}
	return workspace,nil
}

func (t *Provider) checkStatus(config *TestExecutorConfiguration,ctx context.Context) (ExecutionStatus,error) {
	executionStatusURL,err:=t.getAgentURL(config,fmt.Sprintf(APIExecution,config.executionID))
	if err!=nil{
		return executionFailed,err
	}
	req, err := http.NewRequestWithContext(ctx,"GET", executionStatusURL,nil)
	if err!=nil{
		return executionFailed,err
	}

	req.Header.Add("Content-Type", "application/json")
	client := &http.Client{}
	response,err:=client.Do(req)
	if err!=nil{
		return executionFailed,err
	}
	defer response.Body.Close()
	byteResponse,err :=ioutil.ReadAll(response.Body)
	if err!=nil {
		return executionFailed,err
	}
	if response.StatusCode==http.StatusInternalServerError {
		return "",fmt.Errorf(string(byteResponse))
	}
	if response.StatusCode == http.StatusNotFound {
		return executionFailed,fmt.Errorf("execution not found, agent probably dead")
	}
	executionResponse:= &TestExecutionResponse{}
	if err:=json.Unmarshal(byteResponse,executionResponse);err!=nil {
		return "",err
	}
	if executionResponse.Error !=""{
		return executionFailed,fmt.Errorf(executionResponse.Error)
	}

	return executionResponse.Status,nil
}


func (t *Provider) destroyAgentNode(controller agentController) error {
	//TODO
	return nil
}

func (t *Provider) cancelTestExecution(config *TestExecutorConfiguration) error {
	cancelExecutionURL, err := t.getAgentURL(config, fmt.Sprintf(APIExecution, config.executionID))
	if err != nil {
		return err
	}
	req, err := http.NewRequest("DELETE", cancelExecutionURL, nil)
	if err != nil {
		return err
	}

	client := &http.Client{}
	response, err := client.Do(req)
	if err != nil {
		return err
	}
	defer response.Body.Close()

	if response.StatusCode != http.StatusOK {
		return fmt.Errorf("error: %d (%s) when canceling execution %s",response.StatusCode,response.Status,config.executionID)
	}

	return nil
}