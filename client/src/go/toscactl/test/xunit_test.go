package test

import (
	"bytes"
	"io"
	"io/ioutil"
	"os"
	"path/filepath"
	"testing"
)

func TestPatchMissingTimestamp(t *testing.T) {
	// Create temp file with original.xml contents
	tmp_file_path := filepath.Join("test_data", "tmp.xml")
	tmp_file, err := os.Create(tmp_file_path)
	if err != nil {
		t.Error("Cannot create temp file")
	}
	defer os.Remove(tmp_file_path)	
	original_file_path := filepath.Join("test_data", "original.xml")
	original_file, err := os.Open(original_file_path)
	io.Copy(tmp_file, original_file)

	// Load and patch
	var timestamp = "2011-10-17T23:05:06"
	testResult, err := ReadTestResults(original_file_path)
	PatchMissingTimestamp(timestamp, testResult, tmp_file_path)

	// Compare output with target
	f1, err := ioutil.ReadFile(tmp_file_path)
	if err != nil {
        t.Error(err)
    }
	f2, err := ioutil.ReadFile(filepath.Join("test_data", "target.xml"))
	if err != nil {
        t.Error(err)
    }
	if !bytes.Equal(f1, f2) {
		t.Errorf("Files are not equal!\nPatched:\n%s\nTarget:\n%s", string(f1), string(f2))
	}
	defer os.Remove(tmp_file.Name())

	// Check timestamp in xunit
	if testResult.Testsuite[0].Timestamp != timestamp {
		t.Error("Timestamp not set in testsuite")
	}
}