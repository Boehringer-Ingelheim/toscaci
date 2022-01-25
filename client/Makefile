.ONESHELL:
ROOT_DIR:=$(shell dirname $(realpath $(firstword $(MAKEFILE_LIST))))
BUILD_DIR=$(ROOT_DIR)/build
SRC_DIR=$(ROOT_DIR)/src/go/toscactl
VERSION=v0.1
COMMIT=`git rev-parse --short=4 HEAD`
BUILD_TIME=`date +"%m/%d/%Y %H:%M:%S"`
BUILD_ARGS=-ldflags="-X 'toscactl/version.BuildTime=$(BUILD_TIME)' -X 'toscactl/version.Commit=$(COMMIT)' -X 'toscactl/version.Version=$(VERSION)'"
GOARCH=amd64

build: get build_linux build_darwin build_windows

build_linux: build_prepare
	cd $(SRC_DIR)
	GOOS=linux go build -v $(BUILD_ARGS) -o $(BUILD_DIR)/toscactl-linux-amd64 .

build_windows: build_prepare
	cd $(SRC_DIR)
	GOOS=windows go build -v $(BUILD_ARGS) -o $(BUILD_DIR)/toscactl-windows-amd64.exe .
build_darwin: build_prepare
	cd $(SRC_DIR)
	GOOS=darwin go build -v $(BUILD_ARGS) -o $(BUILD_DIR)/toscactl-darwin-amd64 .

test: 
	cd $(SRC_DIR)
	go test -v ./...

clean:
	rm -rf "$(BUILD_DIR)"

get:
	cd $(SRC_DIR)
	go get

build_prepare:
	mkdir -p "$(BUILD_DIR)"