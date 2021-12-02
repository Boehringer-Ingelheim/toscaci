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
	VideoRecord        bool       `json:"videoRecord,omitempty"`
	buildDirectory     string     `json:"-"`
	xUnitPath          string     `json:"-"`
	artifactsPath      string     `json:"-"`
	reportsPath        string     `json:"-"`
	workspace          *Workspace `json:"-"`
	hostname           string     `json:"-"`
	executionID        string     `json:"-"`
	executionLists     []ExecutionList
}
type ExecutionFiles struct {
	Id   string
	Path string
	Size int64
}
type ExecutionList struct {
	Name string
	Entries []string
}
type TestExecutionResponse struct {
	Error       string
	ExecutionID string
	ExecutionLists []ExecutionList
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

	c.VideoRecord = config.TestSuite.VideoRecord
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
			VideoRecord: true,
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



var logTestSuite *log.Entry
var logTestSuiteExecution *log.Entry
func (t *Provider) RunTestSuite(suiteConfig TestSuiteConfiguration,ctx context.Context) ( err error) {
	logTestSuite=log.WithField("testSuite",suiteConfig.Name)
	logTestSuite.Info("Preparing to execute Test Suite")
	timeoutContext,_:=context.WithTimeout(ctx,suiteConfig.TestSuite.Timeout*time.Minute)

	buildDirPath :=path.Join(t.config.WorkingDir, suiteConfig.ResultFolder,suiteConfig.Name)
	_, err = os.Stat(buildDirPath)
	if !os.IsNotExist(err) {
		logTestSuite.Warnf("Build directory %s is not empty, deleting...",buildDirPath)
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
	logTestSuite.Infof("Selected Node %s",suiteConfig.Agent.Hostname)
	logTestSuite.Infof("Preparing Workspace...")

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
		logTestSuite.Infof("Deleting Workspace")
		if err2:=t.DeleteWorkspace(executorSuiteConfig,context.Background());err2!=nil{
			err=fmt.Errorf("error when deleting workspace: please remove it by hand, %v",err2)
		}
	}()
	logTestSuite.Infof("Workspace %s ready", executorSuiteConfig.workspace.SessionID)

	logTestSuite.Infof("Requesting Test %s on workspace %s",suiteConfig.Name,executorSuiteConfig.workspace.SessionID)
	if err := t.requestTestExecution(executorSuiteConfig, timeoutContext);err!=nil{
		return err
	}
	logTestSuite.Debugf("Execution Summary")
	for _,executionList := range executorSuiteConfig.executionLists {
		for _,entry := range executionList.Entries {
			logTestSuite.WithFields(log.Fields{
				"executionList":executionList.Name,
				"entry":entry}).
				Debugf("Execution Entry %s",entry)
		}

	}

	logTestSuiteExecution = logTestSuite.WithField("executionID",executorSuiteConfig.executionID)
	logTestSuiteExecution.Info("Test placed")
	if err := t.waitUntilCompletion(timeoutContext, executorSuiteConfig); err != nil {
		return err
	}

	logTestSuiteExecution.Infof("Test %s completed",suiteConfig.Name)
	logTestSuiteExecution.Infof("Downloading xunit results")
	testReports,errTestReport := t.getTestReports(executorSuiteConfig,timeoutContext)
	logTestSuiteExecution.Infof("Downloading artifacts")
	_,errArtifacts := t.getArtifacts(executorSuiteConfig,timeoutContext)
	logTestSuiteExecution.Infof("Downloading Reports")
	_,errReports := t.getReports(executorSuiteConfig,timeoutContext)
	if errTestReport != nil {
		return errTestReport
	}
	if errArtifacts != nil {
		return errArtifacts
	}
	if errReports != nil {
		return errReports
	}

	failedTests := testReports.GetNumberFailedTests()
	testsWithErrors := testReports.GetNumberErrorsTests()
	totalTests :=testReports.GetNumberTests()
	logTestSuiteExecution.Infof("%d test failed",failedTests)
	logTestSuiteExecution.Infof("%d tests with errors",testsWithErrors)
	logTestSuiteExecution.Infof("%d tests executed",totalTests)
	logTestSuiteExecution.Infof("results saved on %s",executorSuiteConfig.buildDirectory)
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
			logTestSuite.Warn("other execution still running, waiting for agent being free")
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
			logTestSuiteExecution.Warnf("Canceling Test execution %s",executorSuiteConfig.executionID)
			if err:=t.cancelTestExecution(executorSuiteConfig);err!=nil{
				return err
			}
			return ctx.Err()
		case <-time.After(15 * time.Second):
			status, err := t.checkStatus(executorSuiteConfig, ctx)
			logTestSuiteExecution.Infof("%s state",  status)

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
	testExecutorConfig.executionLists = executionResponse.ExecutionLists
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
	logTestSuite.Infof("%d files found",len(executionResponse.Files))
	for _,file := range executionResponse.Files {
		logTestSuite.WithField("file",file.Path).Info("Downloading...")
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