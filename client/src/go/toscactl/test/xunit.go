package test

import (
	"encoding/xml"
	"io/ioutil"
	"os"
	"strings"
	"time"
)
type TestCaseError struct {
	//Text    string `xml:",chardata"`
	Type    string `xml:"type,attr"`
	Message string `xml:"message,attr"`
}


type Testcase struct {
	//Text      string        `xml:",chardata"`
	Name      string        `xml:"name,attr"`
	Time      string        `xml:"time,attr"`
	Classname string        `xml:"classname,attr"`
	Error     TestCaseError `xml:"error"`
	Failure   TestCaseError `xml:"failure"`
	SystemOut string        `xml:"system-out"`
}

type Testsuite struct {
	//Text     string `xml:",chardata"`
	Name     string `xml:"name,attr"`
	Tests    int32 `xml:"tests,attr"`
	Failures int32 `xml:"failures,attr"`
	Errors   int32 `xml:"errors,attr"`
	Time     string `xml:"time,attr"`
	Timestamp string `xml:"timestamp,attr"`
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

func (t TestResults) GetNumberErrorsTests() (errors int32) {
	for _,xunit := range t {
		errors+=xunit.GetNumberTestsWithErrors()
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
		errors+=testSuite.Failures
	}
	return errors
}

func (x Xunit) GetNumberTestsWithErrors() (errors int32) {
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

func PatchMissingFields(xunit *Xunit, filePath string) {
	// Add timestamp information when not available as a workaround (implemented in Tosca 15.0+)
	var now = time.Now()
	var now_s = now.Format(time.RFC3339)	
	for idx := range xunit.Testsuite {
		if xunit.Testsuite[idx].Timestamp == "" {
			xunit.Testsuite[idx].Timestamp = now_s
		}
	}

	// Reconstruct XML file from marshalled contents
	byteOutput,_ := xml.MarshalIndent(&xunit, "", "  ")
	out_s := string(byteOutput)
	// Handle html newlines and tabs in <system-out>
	out_s = strings.ReplaceAll(out_s, "&#xA;", "\n")
	out_s = strings.ReplaceAll(out_s, "&#x9;", "\t")
	out_s = "<?xml version=\"1.0\"?>\n" + out_s  // Add generic header
	
	// Update the original file
	ioutil.WriteFile(filePath, []byte(out_s), 0644)
}
