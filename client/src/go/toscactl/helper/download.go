package helper

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"os"
	"path"
)

func DownloadFile(url string , targetFilePath string,ctx context.Context) error {
	if err:=os.MkdirAll(path.Dir(targetFilePath),os.ModePerm);err!=nil{
		return err
	}

	out, err := os.Create(targetFilePath)
	if err!=nil{
		return err
	}
	defer out.Close()
	req, err := http.NewRequestWithContext(ctx,"GET",url,nil)
	client := &http.Client{}
	resp,err:=client.Do(req)
	if err!=nil{
		return err
	}
	defer resp.Body.Close()
	if resp.StatusCode!=http.StatusOK {
		return fmt.Errorf("error when downloading file %s",resp.Status)
	}
	_, err = io.Copy(out, resp.Body)
	if err!=nil{
		return err
	}
	return nil
}
