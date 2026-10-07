# Build and publish the GRID image to Docker Hub.
#   make push                   tag = current branch name (branches are named by version, e.g. 2.5.0)
#   make push VERSION=2.5.1     explicit tag
# Every push also moves :latest. Run `docker login` once beforehand.

IMAGE    ?= trevorkoenig/grid
VERSION  ?= $(shell git rev-parse --abbrev-ref HEAD)
PLATFORM ?= linux/amd64

.PHONY: help test build push

help:
	@echo "make test    run the test suite"
	@echo "make build   build $(IMAGE):$(VERSION) for $(PLATFORM)"
	@echo "make push    test, build, and push $(IMAGE):$(VERSION) and :latest"

# The project, not GRID.slnx — the solution excludes GRID.Tests from its build, so
# `dotnet test GRID.slnx` runs zero tests and still succeeds.
test:
	dotnet test GRID.Tests/GRID.Tests.csproj

build:
	docker build --platform $(PLATFORM) -f GRID/Dockerfile \
		-t $(IMAGE):$(VERSION) -t $(IMAGE):latest .

push: test build
	docker push $(IMAGE):$(VERSION)
	docker push $(IMAGE):latest
