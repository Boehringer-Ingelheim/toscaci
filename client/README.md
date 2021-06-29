# toscactl

## Build
### 1. Prerequisites
- go 1.16
- make
- git

Ideally you should build on a unix machine, will be easier, but you still can on windows WSL2 or using cygwin

### 2. Build procedure
```bash
#build toscactl for all the platforms
make build 

#build toscactl per each os
make build_linux
make build_windows
make build_darwin

#build in release mode
make build VERSION=v1.0 COMMIT=$(git rev-parse HEAD)
```