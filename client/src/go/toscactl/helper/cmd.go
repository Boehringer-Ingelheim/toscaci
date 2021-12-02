package helper

import (
	"os"
	"os/exec"
)

func ExecuteProcess(cmd *exec.Cmd,workingDir string) ([]byte,error){
	cmd.Dir = workingDir
	cmd.Stderr = os.Stderr
	return cmd.Output()
}
