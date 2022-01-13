package cli

import (
	"context"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path"
	"toscactl/entity"
	"toscactl/helper"
	"toscactl/tosca"

	log "github.com/sirupsen/logrus"
	"github.com/spf13/cobra"
)

const (
	success      = 0
	failedTests  = 1
	canceled     = 2
	configIssue  = 3
	unknownError = 4
)

type TestSuiteError struct {
	err           error
	testSuiteName string
}
type ConfigError struct {
	err error
}

func (c ConfigError) Error() string {
	return fmt.Sprintf("Config Error: %v", c.err)
}

var (
	reportsFlag            entity.StringArray
	testSuiteSelectorsFlag = entity.KeyValue{}
	parametersFlag         = entity.KeyValue{}
	agentSelectorsFlag     = entity.KeyValue{}
	exitCode               = success
	agentHostname          string
	runCmd                 = &cobra.Command{
		Use:   "run [agent selectors] [suite selector] [suite parameters] [testSuite]",
		Short: "Execute a Tosca Test Suite",
		Args:  cobra.MinimumNArgs(1),
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

			toscaProvider, err := tosca.NewProvider(appConfig)
			if err != nil {
				log.Panic(err)
			}

			for _, testSuiteName := range testSuites {
				//Read parameter file
				testSuitePath := path.Join(appConfig.WorkingDir, fmt.Sprintf("tosca-%s.json", testSuiteName))
				testSuiteConfig, err := tosca.LoadTestSuiteConfiguration(testSuitePath, testSuiteName)
				if err != nil {
					testSuiteErrLog(TestSuiteError{
						err:           fmt.Errorf("error when preparing Test execution %s: %w", testSuiteName, ConfigError{err: err}),
						testSuiteName: testSuiteName,
					})
					continue
				}

				//Inject cli options
				if fromDefinition != "" {
					testSuiteConfig.Project.SourcePath = fromDefinition
				}
				if fromConnectionString != "" {
					testSuiteConfig.Project.SourceConnectionStringDB = fromConnectionString
				}
				if fromConnectionUser != "" {
					testSuiteConfig.Project.WorkspaceUsername = fromConnectionUser
				}
				if fromConnectionPassword != "" {
					testSuiteConfig.Project.WorkspacePassword = fromConnectionPassword
				}
				if agentHostname != "" {
					testSuiteConfig.Agent.Hostname = agentHostname
				}
				testSuiteConfig.Agent.Selectors.AddAll(agentSelectorsFlag)
				testSuiteConfig.TestSuite.Selectors.AddAll(testSuiteSelectorsFlag)
				testSuiteConfig.TestSuite.Parameters.AddAll(parametersFlag)

				//Inject Jenkins environment vars if available
				value, present := os.LookupEnv("BUILD_NUMBER")
				if present && len(value) > 0 {
					testSuiteConfig.TestSuite.Parameters.Set("JENKINS_JOB_EXECUTION=" + value)
				}
				value, present = os.LookupEnv("JOB_URL")
				if present && len(value) > 0 {
					testSuiteConfig.TestSuite.Parameters.Set("JENKINS_JOB_URL=" + value)
				}

				//Inject git vars if available

				//Inject git vars if available

				if _, exists := exec.LookPath("git"); exists == nil {
					out, err := helper.ExecuteProcess(exec.Command("git", "config", "--get", "remote.origin.url"), appConfig.WorkingDir)
					if err == nil && len(out) > 0 {
						testSuiteConfig.TestSuite.Parameters.Set("GIT_REPO_URL=" + string(out))
					}
					out, err = helper.ExecuteProcess(exec.Command("git", "log", "-1", "--pretty=format:%H", testSuiteConfig.Project.SourcePath), appConfig.WorkingDir)
					if err == nil && len(out) > 0 {
						testSuiteConfig.TestSuite.Parameters.Set("GIT_REF=" + string(out))
					}

					out, err = helper.ExecuteProcess(exec.Command("git", "rev-parse", "--abbrev-ref", "HEAD"), appConfig.WorkingDir)
					if err == nil && len(out) > 0 {
						testSuiteConfig.TestSuite.Parameters.Set("GIT_BRANCH=" + string(out))
					}

					out, err = helper.ExecuteProcess(exec.Command("git", "log", "-1", "--pretty=format:%an", testSuiteConfig.Project.SourcePath), appConfig.WorkingDir)
					if err == nil && len(out) > 0 {
						testSuiteConfig.TestSuite.Parameters.Set("GIT_AUTHOR=" + string(out))
					}
					out, err = helper.ExecuteProcess(exec.Command("git", "log", "-1", "--pretty=format:%ci", testSuiteConfig.Project.SourcePath), appConfig.WorkingDir)
					if err == nil && len(out) > 0 {
						testSuiteConfig.TestSuite.Parameters.Set("GIT_DATE=" + string(out))
					}
				} else {
					log.Warn("git command not found in $PATH, in case you are running from a git repository we can't inject git parameters to tosca")
				}

				testSuiteConfig.TestSuite.Reports.AddAll(reportsFlag)

				if err = testSuiteConfig.Validate(); err != nil {
					testSuiteErrLog(TestSuiteError{
						err:           fmt.Errorf("wrong configuration on %s: %w", testSuiteName, ConfigError{err: err}),
						testSuiteName: testSuiteName,
					})
					continue
				}

				if err = toscaProvider.RunTestSuite(*testSuiteConfig, cmd.Context()); err != nil {
					testSuiteErrLog(TestSuiteError{
						err:           err,
						testSuiteName: testSuiteName,
					})
				}
			}

			os.Exit(exitCode)
		},
	}
)

func testSuiteErrLog(testSuiteErr TestSuiteError) {
	suiteLog := log.WithField("testSuite", testSuiteErr.testSuiteName).WithError(testSuiteErr.err)
	ec := 0
	if errors.Is(testSuiteErr.err, tosca.TestsFailed) {
		suiteLog.Warnf("Some Tests has failed on TestSuite:%s", testSuiteErr.testSuiteName)
		ec = failedTests

	} else if errors.Is(testSuiteErr.err, context.Canceled) {
		suiteLog.Errorf("Test %s graceful canceled", testSuiteErr.testSuiteName)
		ec = canceled
	} else if errors.Is(testSuiteErr.err, context.DeadlineExceeded) {
		suiteLog.Errorf("Test %s canceled due timeout", testSuiteErr.testSuiteName)
		ec = canceled
	} else if errors.Is(testSuiteErr.err, ConfigError{}) {
		suiteLog.Errorf("Config error on TestSuite %s", testSuiteErr.testSuiteName)
		ec = configIssue
	} else {
		suiteLog.Errorf("Unknown Error when executing TestSuite %s", testSuiteErr.testSuiteName)
		ec = unknownError
	}
	if ec > exitCode {
		exitCode = ec
	}
}

func init() {
	RootCmd.AddCommand(runCmd)

	//Workspace parameters
	runCmd.PersistentFlags().StringVar(&fromDefinition, "from-path", "src/tosca", "Create Workspace from file definition (Workspace definition (.tpr) and subset (.tsu) are needed in the path")
	runCmd.PersistentFlags().StringVar(&fromConnectionString, "from-connection", "", "Create Workspace from existing Tosca MSSQL Database")
	runCmd.PersistentFlags().StringVar(&fromConnectionUser, "project-user", "", "Tosca Project Username")
	runCmd.PersistentFlags().StringVar(&fromConnectionPassword, "project-password", "", "Tosca Project Password")

	//TestExecutionConfiguration parameters
	runCmd.PersistentFlags().Var(&testSuiteSelectorsFlag, "suite-selector", "Select Execution Lists  to run by properties")
	runCmd.PersistentFlags().Var(&parametersFlag, "suite-parameter", "Runtime Test parameter to inject on your Execution List")
	runCmd.PersistentFlags().Var(&reportsFlag, "suite-report", "Tosca Reports Name to generate after test execution")

	//Agent parameters
	runCmd.PersistentFlags().StringVar(&agentHostname, "agent-hostname", "", "Tosca Agent where to run the test execution")
	runCmd.PersistentFlags().Var(&agentSelectorsFlag, "agent-selector", "Tosca Agent where to run the test execution")
}
