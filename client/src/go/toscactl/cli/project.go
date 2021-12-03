package cli

import (
	"encoding/json"
	"fmt"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/cobra"
	"os"
	"strings"
	"toscactl/tosca"
)

var (
	fromDefinition string
	fromConnectionString string
	fromConnectionUser string
	fromConnectionPassword string
	ownerRoleName string
	viewerRoleName string
	projectCmd            = &cobra.Command{
		Use:   "project [action]",
		Short: "Workspace operations",
		Long:  `Workspace operations like create or destroy`,
	}
	createCmd            = &cobra.Command{
		Use:   "create [dbtype] [name]",
		Short: "Create tosca project",
		Long:  `Create Tosca Workspace from existing project or sources`,
		Args:  cobra.ExactArgs(2),
		Run: func(cmd *cobra.Command, args []string) {


			if Verbose {
				log.SetLevel(log.DebugLevel)
				log.Debug("Verbose mode enabled")
			}

			dbtype:= tosca.DBType(strings.ToUpper(args[0]))
			projectName:= args[1]

			templateType:= tosca.CreateEmpty
			if fromConnectionString!=""{
				templateType = tosca.CreateFromDatabase
			}else if fromDefinition!=""{
				templateType= tosca.CreatefromDefinition
			}

			toscaProvider,err := tosca.NewProvider(appConfig)
			if err!=nil{
				log.Panic(err)
			}

			createRequest:= tosca.ProjectCreateRequest{
				SourcePath:               fromDefinition,
				Name:                     projectName,
				TemplateType:             templateType,
				TemplateConnectionString: fromConnectionString,
				TemplateConnectionUsername: fromConnectionUser,
				TemplateConnectionPassword: fromConnectionPassword,
				OwnerRoleName:            ownerRoleName,
				ViewerRoleName:           viewerRoleName,
				DBType:                   dbtype,
			}
			project,err :=toscaProvider.CreateProject(createRequest,cmd.Context())
			if err!=nil{
				log.Error(err)
				os.Exit(1)
			}
			log.Infof("Workspace %s has been created",project.Name)
			b,err :=json.MarshalIndent(project,"","\t")
			if err!=nil{
				log.Panic(err)
			}
			fmt.Println(string(b))
			os.Exit(0)
		},
	}

)

func init() {
	RootCmd.AddCommand(projectCmd)
	projectCmd.AddCommand(createCmd)

	createCmd.PersistentFlags().StringVar(&fromDefinition, "from-definition","", "Create Workspace from file definition (Workspace definition (.tpr) and subset (.tsu) are needed in the path")
	createCmd.PersistentFlags().StringVar(&fromConnectionString, "from-connection","", "Create Workspace from existing Tosca MSSQL Database")
	createCmd.PersistentFlags().StringVar(&fromConnectionUser, "project-user","", "Tosca Project Username")
	createCmd.PersistentFlags().StringVar(&fromConnectionPassword, "project-password","", "Tosca Project Password")
	createCmd.PersistentFlags().StringVar(&ownerRoleName, "owner-group","", "Owner Group AD Name")
	createCmd.PersistentFlags().StringVar(&viewerRoleName, "viewer-group","", "Viewer Group Ad Name")
}