package entity

import (
	"fmt"
	"strings"
	"time"
)

type KeyValue map[string]string

type StringArray []string

type ApplicationConfig struct {
	OrchestratorURL string
	Username        string
	Password        string
	WorkingDir      string
	TestTimeout     time.Duration
}

func (a *ApplicationConfig) Initialize() {
	a.TestTimeout = 30*time.Minute
}


func (i *StringArray) String() string {
	return ""
}

func (i *StringArray) Set(value string) error {
	*i = append(*i, value)
	return nil
}
func (i *StringArray) Type() string{
	return "name"
}

func (i *StringArray) AddAll(array StringArray) {
	for _,toAdd := range array {
		exist:=false
		for index,existing := range *i {
			if existing == toAdd {
				exist=true
				(*i)[index]=toAdd
				break
			}
		}
		if !exist{
			*i = append(*i,toAdd)
		}
	}



}
func (selectorMap *KeyValue) Length() int {
	return len(*selectorMap)
}

func (selectorMap *KeyValue) String() string {
	return ""
}

func (selectorMap *KeyValue) Set(value string) error {
	keyValue:=strings.Split(value,"=")
	if len(keyValue) <= 1 {
		return fmt.Errorf("invalid format %s expected labelName=labelValue", value)
	}
	(*selectorMap)[keyValue[0]]=keyValue[1]
	return nil
}
func (selectorMap *KeyValue) Type() string{
	return "key=value"
}

func (selectorMap *KeyValue) AddAll(list KeyValue) {
	for k,v := range list {
		(*selectorMap)[k]=v
	}
}