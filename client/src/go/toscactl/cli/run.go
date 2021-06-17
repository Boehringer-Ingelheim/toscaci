package cli

import (
	"fmt"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/cobra"
	"os"
	"path"
	"toscactl/entity"
	"toscactl/tosca"
)

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
				os.Exit(1)
			}

			toscaProvider,err := tosca.NewProvider(appConfig)
			if err!=nil{
				log.Panic(err)
			}

			for _, testSuiteName := range testSuites {
				//Read parameter file
				testSuitePath:=path.Join(appConfig.WorkingDir,fmt.Sprintf("tosca-%s.json", testSuiteName))
				testSuiteConfig,err := tosca.LoadTestSuiteConfiguration(testSuitePath,testSuiteName)
				if err!=nil{
					log.Fatalf("Error when preparing Test execution:%s %v", testSuiteName,err)
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
				testSuiteConfig.TestSuite.Reports.AddAll(reportsFlag)

				if err=testSuiteConfig.Validate();err!=nil{
					log.Fatalf("Wrong configuration on %s: %v", testSuiteName,err)
					os.Exit(1)
				}

				if err=toscaProvider.RunTestSuite(*testSuiteConfig,cmd.Context());err!=nil{
					log.Errorf("Error when executing TestSuite:%s, error: %v", testSuiteName,err)
					os.Exit(2)
				}
			}

			os.Exit(0)
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

