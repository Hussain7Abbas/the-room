# The Room — local setup & run (uses dotnet + Godot 4.7 mono)
# Run `make help` for targets.

SHELL := /bin/bash
.SHELLFLAGS := -eu -o pipefail -c

ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))

BLUE := $(shell printf '\033[34m')
GREEN := $(shell printf '\033[32m')
YELLOW := $(shell printf '\033[33m')
RESET := $(shell printf '\033[0m')

.DEFAULT_GOAL := help

# .NET SDK lives here but isn't on the default shell PATH on this machine.
DOTNET_DIR := /usr/local/share/dotnet
DOTNET     := $(DOTNET_DIR)/dotnet
export PATH := $(DOTNET_DIR):$(PATH)

# Godot 4.7 mono editor/runtime (macOS app bundle).
GODOT := /Applications/Godot_mono.app/Contents/MacOS/Godot

SLN     := The Room.sln
PORT      ?= 60010
HOST      ?= 127.0.0.1
N         ?= 2
CHARACTER ?=
CHAR_FLAG := $(if $(CHARACTER),--character=$(CHARACTER),)

# VPS deploy target (see plan/phase-1-network-spike.md). SSH host is an alias from ~/.ssh/config;
# app runs isolated under its own system user/service, never as part of DEPLOY_HOST's other apps.
DEPLOY_HOST ?= kios-chat
DEPLOY_PATH ?= /opt/the-room/app
DEPLOY_USER ?= theroom
DEPLOY_SERVICE ?= the-room-server.service

.PHONY: help \
	install setup \
	build run-server run-client run-local run-bots \
	clean \
	test \
	deploy-server deploy-logs deploy-status \
	export-server export-client

## ------------------------------------------------------------------------
## help
## ------------------------------------------------------------------------

help:
	@echo "$(BLUE)The Room — Godot 4.7 (C#) knife-fight arena$(RESET)"
	@echo ""
	@echo "$(BLUE)Setup$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "install" "Restore NuGet packages ($(YELLOW)dotnet restore$(RESET))"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "setup" "install + sanity build, prints next steps"
	@echo ""
	@echo "$(BLUE)App$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "build" "Build the C# project ($(YELLOW)dotnet build$(RESET))"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-server" "Run a dedicated headless server ($(YELLOW)godot --headless --server$(RESET)). PORT=$(PORT)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-client" "Run one windowed client and connect ($(YELLOW)godot --connect$(RESET)). HOST=$(HOST) PORT=$(PORT) CHARACTER=$(CHARACTER)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-local" "Spawn 1 local server + N windowed clients. N=$(N) PORT=$(PORT) CHARACTER=$(CHARACTER)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-bots" "Connect N headless wander/stab/ability bots to a server (see run-server). N=$(N) HOST=$(HOST) PORT=$(PORT) CHARACTER=$(CHARACTER)"
	@echo ""
	@echo "$(BLUE)Quality$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "clean" "Remove build/editor caches ($(YELLOW).godot/mono, bin, obj$(RESET))"
	@echo ""
	@echo "$(BLUE)Test$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "test" "Run the C# test suite ($(YELLOW)Chickensoft.GoDotTest$(RESET), tests/)"
	@echo ""
	@echo "$(BLUE)Deploy$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "deploy-server" "rsync + rebuild + restart the dedicated server on $(DEPLOY_HOST) ($(YELLOW)systemd: $(DEPLOY_SERVICE)$(RESET))"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "deploy-status" "Show the deployed server's systemd status"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "deploy-logs" "Tail the deployed server's journal"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "export-server" "Export a standalone Linux server binary to build/server/"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "export-client" "Export a standalone Windows client .exe to build/client/"

## ------------------------------------------------------------------------
## Setup
## ------------------------------------------------------------------------

install:
	@cd "$(ROOT)" && "$(DOTNET)" restore "$(SLN)"

setup: install build
	@echo ""
	@echo "$(GREEN)Setup done.$(RESET) Next steps:"
	@echo "  - Local playtest:   make run-local N=3"
	@echo "  - Just the server:  make run-server PORT=$(PORT)"
	@echo "  - Just a client:    make run-client HOST=127.0.0.1 PORT=$(PORT)"

## ------------------------------------------------------------------------
## App
## ------------------------------------------------------------------------

build:
	@cd "$(ROOT)" && "$(DOTNET)" build "$(SLN)"

run-server: build
	@cd "$(ROOT)" && "$(GODOT)" --headless --path . -- --server --port=$(PORT)

run-client: build
	@cd "$(ROOT)" && "$(GODOT)" --path . -- --connect=$(HOST) --port=$(PORT) $(CHAR_FLAG)

run-local: build
	@cd "$(ROOT)" && \
	echo "$(GREEN)Starting server on port $(PORT)...$(RESET)" && \
	"$(GODOT)" --headless --path . -- --server --port=$(PORT) & \
	SERVER_PID=$$!; \
	trap 'echo "Stopping server ($(YELLOW)pid $$SERVER_PID$(RESET))..."; kill $$SERVER_PID 2>/dev/null || true' EXIT; \
	sleep 2; \
	for i in $$(seq 1 $(N)); do \
		echo "$(GREEN)Starting client $$i...$(RESET)"; \
		"$(GODOT)" --path . -- --connect=127.0.0.1 --port=$(PORT) --name=Player$$i $(CHAR_FLAG) & \
		sleep 0.5; \
	done; \
	wait

run-bots: build
	@cd "$(ROOT)" && \
	for i in $$(seq 1 $(N)); do \
		echo "$(GREEN)Starting bot $$i...$(RESET)"; \
		"$(GODOT)" --headless --path . -- --connect=$(HOST) --port=$(PORT) --name=Bot$$i --bot $(CHAR_FLAG) & \
		sleep 0.3; \
	done; \
	wait

## ------------------------------------------------------------------------
## Quality
## ------------------------------------------------------------------------

clean:
	@cd "$(ROOT)" && rm -rf .godot/mono bin obj
	@echo "$(GREEN)Cleaned .godot/mono, bin/, obj/.$(RESET)"

## ------------------------------------------------------------------------
## Test
## ------------------------------------------------------------------------

test: build
	@cd "$(ROOT)" && "$(GODOT)" --headless --path . tests/TestRunner.tscn -- --run-tests --quit-on-finish

## ------------------------------------------------------------------------
## Deploy
## ------------------------------------------------------------------------

deploy-server:
	@echo "$(GREEN)Syncing to $(DEPLOY_HOST):$(DEPLOY_PATH)...$(RESET)"
	@rsync -az --delete \
		--exclude '.git' --exclude '.godot' --exclude 'bin' --exclude 'obj' \
		"$(ROOT)/" "$(DEPLOY_HOST):$(DEPLOY_PATH)/"
	@echo "$(GREEN)Rebuilding on $(DEPLOY_HOST)...$(RESET)"
	@ssh "$(DEPLOY_HOST)" '\
		chown -R $(DEPLOY_USER):$(DEPLOY_USER) "$(DEPLOY_PATH)" && \
		sudo -u $(DEPLOY_USER) bash -c "export PATH=/opt/the-room/dotnet:\$$PATH; cd $(DEPLOY_PATH) && dotnet build \"The Room.sln\""'
	@echo "$(GREEN)Restarting $(DEPLOY_SERVICE)...$(RESET)"
	@ssh "$(DEPLOY_HOST)" "systemctl restart $(DEPLOY_SERVICE) && sleep 2 && systemctl is-active $(DEPLOY_SERVICE)"
	@echo "$(GREEN)Deployed.$(RESET) make deploy-status / deploy-logs to check on it."

deploy-status:
	@ssh "$(DEPLOY_HOST)" "systemctl status $(DEPLOY_SERVICE) --no-pager -l"

deploy-logs:
	@ssh "$(DEPLOY_HOST)" "journalctl -u $(DEPLOY_SERVICE) -n 100 --no-pager"

export-server: build
	@mkdir -p "$(ROOT)/build/server"
	@cd "$(ROOT)" && "$(GODOT)" --headless --path . --export-release server build/server/the-room-server.x86_64
	@echo "$(GREEN)Exported to build/server/the-room-server.x86_64$(RESET) (Linux x86_64 — needs Godot's Linux export templates installed for this platform's Godot editor)"

export-client: build
	@mkdir -p "$(ROOT)/build/client"
	@cd "$(ROOT)" && "$(GODOT)" --headless --path . --export-release client build/client/the-room.exe
	@echo "$(GREEN)Exported to build/client/the-room.exe$(RESET) (Windows x86_64 — needs Godot's Windows export templates installed for this platform's Godot editor)"
