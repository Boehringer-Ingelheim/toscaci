package tosca

import (
	"context"
	"net/url"
	"path"
	"toscactl/entity"
)

const (
	APIbasePath = "api/v2"

	//Create Template enum
	CreatefromDefinition CreateTemplateType = "fromDefinition"
	CreateEmpty CreateTemplateType = "empty"
	CreateFromDatabase CreateTemplateType = "fromConnectionString"

	//Tosca DB Type enum
	LocalDB DBType = "LOCAL"
	MSSQLDB DBType = "MSSQL"
)

type CreateTemplateType string

func (t CreateTemplateType) String() string {
	return string(t)
}
type DBType string

func (t DBType) String() string {
	return string(t)
}

// Workspace represents a tosca project deployed on a node.
type Workspace struct {
	Name string `json:",omitempty"`
	dbType DBType `json:",omitempty"`
	ConnectionString string `json:",omitempty"`
	DBName string `json:",omitempty"`
	OwnerRoleName string `json:",omitempty"`
	ViewerRoleName string `json:",omitempty"`
	WorkspacePath string `json:"-"`
	SessionID string `json:",omitempty"`
}

type TestProvider interface {
	RunTestSuite(suiteConfig TestSuiteConfiguration,ctx context.Context) error
}

type ProjectProvider interface {
	CreateProject(createProjectRequest ProjectCreateRequest,ctx context.Context) (*Workspace,error)
	DeleteWorkspace(workspaceSessionID string ,ctx context.Context) error
}

type Provider struct {
	config *entity.ApplicationConfig
}

func NewProvider(config *entity.ApplicationConfig) (*Provider, error) {
	toscaProvider := &Provider{
		config: config,
	}
	return toscaProvider,nil
}

func (t *Provider)  getOrchestratorURL(URLpath string) (string,error){
	return getAPIURL(t.config.OrchestratorURL,URLpath)
}

func (t *Provider)  getAgentURL(config *TestExecutorConfiguration,URLpath string) (string,error){
	return getAPIURL(config.hostname,URLpath)
}

func getAPIURL(HostURL string,URLpath string) (string,error){
	u, err := url.Parse(HostURL)
	if err!=nil{
		return "",err
	}
	u.Path = path.Join(u.Path,APIbasePath, URLpath)
	return u.String(),nil
}



