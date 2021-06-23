package test

import (
	"encoding/xml"
	"io/ioutil"
	"os"
)
type TestCaseError struct {
		Text    string `xml:",chardata"`
		Type    string `xml:"type,attr"`
		Message string `xml:"message,attr"`
}


type Testcase struct {
	Text      string        `xml:",chardata"`
	Name      string        `xml:"name,attr"`
	Time      string        `xml:"time,attr"`
	Classname string        `xml:"classname,attr"`
	Error     TestCaseError `xml:"error"`
	SystemOut string        `xml:"system-out"`
}

type Testsuite struct {
	Text     string `xml:",chardata"`
	Name     string `xml:"name,attr"`
	Tests    int32 `xml:"tests,attr"`
	Failures int32 `xml:"failures,attr"`
	Errors   int32 `xml:"errors,attr"`
	Time     string `xml:"time,attr"`
	Skipped  int32 `xml:"skipped,attr"`
	Hostname string `xml:"hostname,attr"`
	ID       string `xml:"id,attr"`
	Testcase []Testcase `xml:"testcase"`
}

type TestResults []*Xunit


func (t TestResults) GetNumberFailedTests() (errors int32) {
	for _,xunit := range t {
		errors+=xunit.GetNumberFailedTests()
	}
	return errors
}

func (t TestResults) GetNumberTests() (totalTest int32) {
	for _,xunit := range t {
		totalTest+=xunit.GetNumberTests()
	}
	return totalTest
}


type Xunit struct {
	XMLName   xml.Name `xml:"testsuites"`
	Text      string   `xml:",chardata"`
	Testsuite []Testsuite`xml:"testsuite"`
}

func (x Xunit) GetNumberFailedTests() (errors int32) {
	for _, testSuite := range x.Testsuite {
		errors+=testSuite.Errors
	}
	return errors
}

func (x Xunit) GetNumberTests() (tests int32) {
	for _, testSuite := range x.Testsuite {
		tests+=testSuite.Tests
	}
	return tests
}

func ReadTestResults(filePath string) (*Xunit,error){
	xmlFile, err := os.Open(filePath)
	if err!= nil{
		return nil,err
	}
	defer xmlFile.Close()
	byteValue, _ := ioutil.ReadAll(xmlFile)
	var xunit Xunit
	if err=xml.Unmarshal(byteValue, &xunit);err!=nil{
		return nil,err
	}

	return &xunit,nil
}
