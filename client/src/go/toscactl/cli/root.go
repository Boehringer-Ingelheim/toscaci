package cli

import (
	"context"
	"fmt"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/cobra"
	"github.com/spf13/viper"
	"os"
	"path"
	"path/filepath"
	"runtime"
	"toscactl/entity"

)


var (
	cfgFile string
	Verbose bool
	appConfig = &entity.ApplicationConfig{}
	RootCmd = &cobra.Command{
		Use:   "toscactl",
		Long: `This application Allow you to manage Tosca remotely`,
		Short:`This application Allow you to manage Tosca remotely`,
	}
)

func Execute(ctx context.Context) {
	if err := RootCmd.ExecuteContext(ctx); err != nil {
		fmt.Println(err)
		os.Exit(-1)
	}
}

func init() {
	cobra.OnInitialize(initConfig)
	RootCmd.PersistentFlags().StringP("orchestratorURL","s",  "http://localhost:8080", "Tosca Orchestrator URL")
	RootCmd.PersistentFlags().String("username",  "", "Tosca Server username")
	RootCmd.PersistentFlags().String("password",  "", "Tosca Server password")
	RootCmd.PersistentFlags().StringP("workingDir", "w","","Working Directory where entities and results are expected")
	RootCmd.PersistentFlags().StringVar(&cfgFile, "config", "", "config file path, accept Environment Variable TOSCA_CONFIG (default is $HOME/.tosca-config.yaml) ")
	RootCmd.PersistentFlags().BoolVarP(&Verbose, "verbose", "v", false, "verbose output")
}

// initConfig reads in config file and ENV variables if set.
func initConfig() {
	dir, err := filepath.Abs(filepath.Dir(os.Args[0]))
	if err != nil {
		log.Fatal(err)
	}

	viper.SetConfigName("config") // name of config file (without extension)
	if cfgFile != "" {                         // enable ability to specify config file via flag
		log.Debug("cfgFile: ", cfgFile)
		viper.SetConfigFile(cfgFile)
		configDir := path.Dir(cfgFile)
		if configDir != "." && configDir != dir {
			viper.AddConfigPath(configDir)
		}
	}

	viper.AddConfigPath(dir)
	if runtime.GOOS != "windows" {
		viper.AddConfigPath("/etc/tosca")
	}else{
		viper.AddConfigPath("C:\\Program Files\\Tricentis\\Tosca\\")
	}
	viper.AddConfigPath("$HOME/.tosca")
	viper.AutomaticEnv() // read in environment variables that match
	viper.SetEnvPrefix("tosca")
	viper.AddConfigPath(".")
	viper.BindPFlags(RootCmd.PersistentFlags())


	// If a config file is found, read it in.
	if err := viper.ReadInConfig(); err == nil {
		log.Infof("Using config file: %s", viper.ConfigFileUsed())
	}
	appConfig.Initialize()
	if err:=viper.Unmarshal(&appConfig); err!=nil{
		log.Errorf("Error when unmarshalling configuration %v",err)
		os.Exit(1)
	}

	workingPath,err:=calculateWorkingPath(appConfig.WorkingDir)
	if err!=nil{
		log.Fatal(err)
	}
	appConfig.WorkingDir=workingPath

}

// calculateWorkingPath uses workingPath passed by argument or if nil uses process workingPath
func calculateWorkingPath(workingPath string) (workingPathReturn string,err error){
	if workingPath==""{
		workingPathReturn, err = os.Getwd()
		if err != nil {
			return "",fmt.Errorf("error when getting working directory: %s",err)
		}
		workingPath = workingPathReturn
		log.Debugf("no working Directory especified, selecting system working dir %s",workingPath)
	}else{
		log.Debugf("selected working DIrectory %s",workingPath)
	}
	return workingPathReturn,nil

}
