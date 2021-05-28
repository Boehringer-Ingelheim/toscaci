package cli

import (
	"fmt"
	"github.com/spf13/cobra"
	"os"
	"toscactl/version"
)


var versionCmd = &cobra.Command{
		Use: `version`,
		Short: "Show Application version",
		Long:  `Show Application version`,
		Run: func(cmd *cobra.Command, args []string) {
			fmt.Printf("Version: %s (%s) Commit: %s", version.Version, version.BuildTime, version.Commit)
			os.Exit(1)
		},
	}


func init() {
	RootCmd.AddCommand(versionCmd)
}