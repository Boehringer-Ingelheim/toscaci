package cli

import (
	"context"
	"errors"
	"fmt"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/cobra"
	"os"
	"os/exec"
	"path"
	"toscactl/entity"
	"toscactl/tosca"
)
const(

	success=0
	failedTests=1
	canceled=2
	configIssue=3
	unknownError=4

)
type TestSuiteError struct {
	err error
	testSuiteName string
}
type ConfigError struct {
	err error
}

func (c ConfigError) Error() string {
	return fmt.Sprintf("Config Error: %v",c.err)
}

var (
	reportsFlag            entity.StringArray
	testSuiteSelectorsFlag = entity.KeyValue{}
	parametersFlag         = entity.KeyValue{}
	agentSelectorsFlag     = entity.KeyValue{}
	agentHostname string
	runCmd                 = &cobra.Command{
		Use:   "run [agent selectors] [suite selector] [suite parameters] [testSuite]",
		Short: "Execute a Tosca Test Suite",
		Args: cobra.MinimumNArgs(1),
		Long:  `Execute a Tosca Test Suite, you can run selected test suites with selectors on a tosca agent pool or fixed node.`,
		Run: func(cmd *cobra.Command, testSuites []string) {
			if Verbose {
				log.SetLevel(log.DebugLevel)
				log.Debug("Verbose mode enabled")
			}
			if len(testSuites) == 0 {
				log.Error("at least one test suite is mandatory, use -t [testName]")
				os.Exit(configIssue)
			}

			toscaProvider,err := tosca.NewProvider(appConfig)
			if err!=nil{
				log.Panic(err)
			}
			var testSuitesErrors []TestSuiteError
			for _, testSuiteName := range testSuites {
				//Read parameter file
				testSuitePath:=path.Join(appConfig.WorkingDir,fmt.Sprintf("tosca-%s.json", testSuiteName))
				testSuiteConfig,err := tosca.LoadTestSuiteConfiguration(testSuitePath,testSuiteName)
				if err!=nil{
					testSuitesErrors = append(testSuitesErrors,TestSuiteError{
						err:           fmt.Errorf("error when preparing Test execution %s: %w",testSuiteName,ConfigError{err:err}),
						testSuiteName: testSuiteName,
					})
					continue
				}

				//Inject cli options
				if fromDefinition!=""{
					testSuiteConfig.Project.SourcePath =fromDefinition
				}
				if fromConnectionString != ""{
					testSuiteConfig.Project.SourceConnectionStringDB=fromConnectionString
				}
				if agentHostname!="" {
					testSuiteConfig.Agent.Hostname=agentHostname
				}
				testSuiteConfig.Agent.Selectors.AddAll(agentSelectorsFlag)
				testSuiteConfig.TestSuite.Selectors.AddAll(testSuiteSelectorsFlag)
				testSuiteConfig.TestSuite.Parameters.AddAll(parametersFlag)

				//Inject Jenkins environment vars if available
				value, present := os.LookupEnv("BUILD_NUMBER")
				if present && len(value) > 0 {
					testSuiteConfig.TestSuite.Parameters.Set("JENKINS_JOB_EXECUTION="+value)
				}
				value, present = os.LookupEnv("JOB_URL")
				if present && len(value) > 0 {
					testSuiteConfig.TestSuite.Parameters.Set("JENKINS_JOB_URL="+value)
				}
				
				//Inject git vars if available				
				out, err := exec.Command("git", "config", "--get", "remote.origin.url").Output()
				if err == nil  && len(out) > 0 {
					testSuiteConfig.TestSuite.Parameters.Set("GIT_REPO_URL="+string(out))
				}
				out, err = exec.Command("git", "rev-parse", "--HEAD").Output()
				if err == nil  && len(out) > 0 {
					testSuiteConfig.TestSuite.Parameters.Set("GIT_REF="+string(out))
				}

				testSuiteConfig.TestSuite.Reports.AddAll(reportsFlag)

				if err=testSuiteConfig.Validate();err!=nil{
					testSuitesErrors = append(testSuitesErrors,TestSuiteError{
						err:           fmt.Errorf("wrong configuration on %s: %w",testSuiteName,ConfigError{err:err}),
						testSuiteName: testSuiteName,
					})
					continue
				}

				if err=toscaProvider.RunTestSuite(*testSuiteConfig,cmd.Context());err!=nil{
					testSuitesErrors = append(testSuitesErrors,TestSuiteError{
						err:           err,
						testSuiteName: testSuiteName,
					})
					continue
				}
			}


			var exitCode=success
			for _,testSuiteErr:=range testSuitesErrors{
				if errors.Is(testSuiteErr.err,tosca.TestsFailed) {
					log.Warnf("Some Tests has failed on TestSuite:%s", testSuiteErr.testSuiteName)
					if exitCode<failedTests {
						exitCode=failedTests
					}
				}else if errors.Is(testSuiteErr.err, context.Canceled) {
					log.Errorf("Test %s graceful canceled",testSuiteErr.testSuiteName)
					if exitCode<canceled {
						exitCode=canceled
					}
				}else if  errors.Is(testSuiteErr.err, context.DeadlineExceeded){
					log.Errorf("Test %s canceled due timeout",testSuiteErr.testSuiteName)
					if exitCode<canceled {
						exitCode=canceled
					}
				}else if  errors.Is(testSuiteErr.err, ConfigError{}){
					log.Errorf("Config error on TestSuite %s, %v",testSuiteErr.testSuiteName,testSuiteErr.err)
					if exitCode<configIssue {
						exitCode=configIssue
					}
				}else{
					log.Errorf("Unknown Error when executing TestSuite %s, error: %v", testSuiteErr.testSuiteName,testSuiteErr.err)
					if exitCode<unknownError {
						exitCode=unknownError
					}
				}
			}

			os.Exit(exitCode)
		},
	}
)

func init() {
	RootCmd.AddCommand(runCmd)

	//Workspace parameters
	runCmd.PersistentFlags().StringVar(&fromDefinition, "from-path","src/tosca", "Create Workspace from file definition (Workspace definition (.tpr) and subset (.tsu) are needed in the path")
	runCmd.PersistentFlags().StringVar(&fromConnectionString, "from-connection","", "Create Workspace from existing Tosca MSSQL Database")

	//TestExecutionConfiguration parameters
	runCmd.PersistentFlags().Var(&testSuiteSelectorsFlag, "suite-selector", "Select Execution Lists  to run by properties")
	runCmd.PersistentFlags().Var(&parametersFlag, "suite-parameter", "Runtime Test parameter to inject on your Execution List")
	runCmd.PersistentFlags().Var(&reportsFlag, "suite-report","Tosca Reports Name to generate after test execution")

	//Agent parameters
	runCmd.PersistentFlags().StringVar(&agentHostname, "agent-hostname","","Tosca Agent where to run the test execution")
	runCmd.PersistentFlags().Var(&agentSelectorsFlag, "agent-selector","Tosca Agent where to run the test execution")
}

