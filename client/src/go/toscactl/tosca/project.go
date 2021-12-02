package tosca

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"io/fs"
	"io/ioutil"
	"mime/multipart"
	"net/http"
	"os"
	"path/filepath"
)

const (
	APICreateWorkspace = "workspace"
	APIworkspace = "workspace/%s"
)


type ProjectCreateRequest struct {
	SourcePath               string
	Name                     string
	TemplateType             CreateTemplateType
	TemplateConnectionString string
	OwnerRoleName            string
	ViewerRoleName           string
	DBType                   DBType
}

type ProjectCreateResponse struct {
	Error   string
	Project Workspace
}

func (t *Provider) DeleteWorkspace(testExecutorConfig *TestExecutorConfiguration ,ctx context.Context) error {
	deleteWorkspaceURL, err := t.getAgentURL(testExecutorConfig, fmt.Sprintf(APIworkspace, testExecutorConfig.workspace.SessionID))
	if err != nil {
		return err
	}

	logTestSuite.Debugf("Delete workspace: %s",deleteWorkspaceURL)
	req, err := http.NewRequestWithContext(ctx, "DELETE", deleteWorkspaceURL, nil)
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
		return fmt.Errorf("error: %d (%s) when deleting workspace %s",response.StatusCode,response.Status,testExecutorConfig.workspace.SessionID)
	}

	return nil
}

func (t *Provider) CreateProject(createProjectRequest ProjectCreateRequest,ctx context.Context) (*Workspace,error) {
	return t.createProject(createProjectRequest, t.config.OrchestratorURL,ctx)
}

func (t *Provider) createProject(createProjectRequest ProjectCreateRequest,hostURL string,ctx context.Context) (*Workspace,error) {
	provisionerURL,err:=getAPIURL(hostURL, APICreateWorkspace)
	if err!=nil{
		return nil,err
	}
	if err:=checkProjectRequest(createProjectRequest);err!=nil{
		return nil,err
	}
	logTestSuite.Debugf("Create Project Payload: %+v",createProjectRequest)
	body := &bytes.Buffer{}
	writer := multipart.NewWriter(body)
	if err:=multipartField(writer,"name",createProjectRequest.Name);err!=nil{
		return nil,err
	}
	if err:=multipartField(writer,"templateType",createProjectRequest.TemplateType.String());err!=nil{
		return nil,err
	}
	if err:=multipartField(writer,"ownerRoleName",createProjectRequest.OwnerRoleName);err!=nil{
		return nil,err
	}
	if err:=multipartField(writer,"viewerRoleName",createProjectRequest.ViewerRoleName);err!=nil{
		return nil,err
	}
	if err:=multipartField(writer,"dbType",createProjectRequest.DBType.String());err!=nil{
		return nil,err
	}


	switch createProjectRequest.TemplateType {
	case CreatefromDefinition:
		if err:=multipartFileUpload(writer,"importFiles",createProjectRequest);err!=nil{
			return nil,err
		}
	case CreateFromDatabase:
		if err:=multipartField(writer,"templateConnectionString",createProjectRequest.TemplateConnectionString);err!=nil{
			return nil,err
		}
	}
	writer.Close()

	req, err := http.NewRequestWithContext(ctx,"POST",provisionerURL , body)
	if err!=nil{
		return nil,err
	}
	if t.config.Username!="" && t.config.Password !=""{
		req.SetBasicAuth(t.config.Username,t.config.Password)
	}
	req.Header.Add("Content-Type", writer.FormDataContentType())
	client := &http.Client{}
	response,err:=client.Do(req)
	if err!=nil{
		return nil,err
	}
	defer response.Body.Close()
	bodyBytes, err := ioutil.ReadAll(response.Body)
	if err != nil {
		return nil, err
	}
	if response.StatusCode != http.StatusOK {
		bodyString := string(bodyBytes)
		return nil, fmt.Errorf("unknown Error %d (%s) when creating project: %s", response.StatusCode,response.Status,bodyString)
	}

	projectCreateResponse := &ProjectCreateResponse{}
	if err:=json.Unmarshal(bodyBytes,projectCreateResponse);err!=nil{
		return nil,err
	}

	if projectCreateResponse.Error!=""{
		return nil,fmt.Errorf(projectCreateResponse.Error)
	}
	return &projectCreateResponse.Project, nil
}

func multipartField(writer *multipart.Writer, fieldName string, value string) error {
	if value ==""{
		return nil
	}
	w,err:=writer.CreateFormField(fieldName)
	if err!=nil{
		return err
	}
	_,err=w.Write([]byte(value))
	if err!=nil{
		return err
	}
	return nil
}

func multipartFileUpload(writer *multipart.Writer, fieldName string, request ProjectCreateRequest) error{
	return filepath.WalkDir(request.SourcePath, func(path string, dirEntry fs.DirEntry, e error) error {
		if filepath.Ext(dirEntry.Name()) == ".tpr" || filepath.Ext(dirEntry.Name()) == ".tsu" {
			file, err := os.Open(path)
			if err!=nil {
				return err
			}
			defer file.Close()
			part, err := writer.CreateFormFile(fieldName, filepath.Base(file.Name()))
			if err!=nil {
				return err
			}
			_,err=io.Copy(part, file)
			if err!=nil{
				return err
			}
		}
		return nil
	})
}

func checkProjectRequest(request ProjectCreateRequest) error {
	if request.Name==""{
		return fmt.Errorf("project name must not be empty")
	}

	switch request.TemplateType {
	case CreateFromDatabase:
		if request.TemplateConnectionString==""{
			return fmt.Errorf("template connection string is mandatory")
			//TODO validate connection string?
		}
	case CreatefromDefinition:
		dir, err := os.Stat(request.SourcePath)
		if err != nil {
			return fmt.Errorf("invalid sources dir %s", request.SourcePath)
		}
		if !dir.IsDir() {
			return fmt.Errorf("%q is not a directory", request.SourcePath)
		}
		haveDefinition:=false
		haveSubset:=false
		filepath.WalkDir(request.SourcePath, func(s string, d fs.DirEntry, e error) error {
			if e != nil { return e }
			if filepath.Ext(d.Name()) == ".tpr" {
				if haveDefinition{
					return fmt.Errorf("multiple Workspace definition found, only one is allowed")
				}
				haveDefinition=true
			}
			if filepath.Ext(d.Name()) == ".tsu" {
				haveSubset=true
			}
			return nil
		})

		if !haveDefinition {
			return fmt.Errorf("no project definition file (*.tpr) found")
		}
		if !haveSubset{
			return fmt.Errorf("at least one subset file (*.tsu) is needed, any not found")
		}
	}
	return nil
}
