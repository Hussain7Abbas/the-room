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
PORT    ?= 60010
HOST    ?= 127.0.0.1
N       ?= 2

.PHONY: help \
	install setup \
	build run-server run-client run-local run-bots \
	clean \
	test \
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
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-client" "Run one windowed client and connect ($(YELLOW)godot --connect$(RESET)). HOST=$(HOST) PORT=$(PORT)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-local" "Spawn 1 local server + N windowed clients. N=$(N) PORT=$(PORT)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "run-bots" "Connect N headless wander/stab bots to a server (see run-server). N=$(N) HOST=$(HOST) PORT=$(PORT)"
	@echo ""
	@echo "$(BLUE)Quality$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "clean" "Remove build/editor caches ($(YELLOW).godot/mono, bin, obj$(RESET))"
	@echo ""
	@echo "$(BLUE)Test$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "test" "TODO — no test framework wired yet, see plan/phase-0-foundation.md"
	@echo ""
	@echo "$(BLUE)Deploy$(RESET)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "export-server" "TODO — needs export_presets.cfg (Linux headless server template)"
	@printf "  $(GREEN)%-14s$(RESET) %s\n" "export-client" "TODO — needs export_presets.cfg (Windows client template)"

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
	@cd "$(ROOT)" && "$(GODOT)" --path . -- --connect=$(HOST) --port=$(PORT)

run-local: build
	@cd "$(ROOT)" && \
	echo "$(GREEN)Starting server on port $(PORT)...$(RESET)" && \
	"$(GODOT)" --headless --path . -- --server --port=$(PORT) & \
	SERVER_PID=$$!; \
	trap 'echo "Stopping server ($(YELLOW)pid $$SERVER_PID$(RESET))..."; kill $$SERVER_PID 2>/dev/null || true' EXIT; \
	sleep 2; \
	for i in $$(seq 1 $(N)); do \
		echo "$(GREEN)Starting client $$i...$(RESET)"; \
		"$(GODOT)" --path . -- --connect=127.0.0.1 --port=$(PORT) --name=Player$$i & \
		sleep 0.5; \
	done; \
	wait

run-bots: build
	@cd "$(ROOT)" && \
	for i in $$(seq 1 $(N)); do \
		echo "$(GREEN)Starting bot $$i...$(RESET)"; \
		"$(GODOT)" --headless --path . -- --connect=$(HOST) --port=$(PORT) --name=Bot$$i --bot & \
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

test:
	@echo "$(YELLOW)No test framework wired yet.$(RESET) See plan/phase-0-foundation.md — deferred pending gdUnit4/GoDotTest setup."
	@exit 1

## ------------------------------------------------------------------------
## Deploy
## ------------------------------------------------------------------------

export-server:
	@echo "$(YELLOW)No export_presets.cfg yet.$(RESET) Add a 'Linux/X11' headless server preset in the Godot editor" \
		"(Project > Export...) named 'server', then this target will run:" \
		"godot --headless --export-release server build/server/the-room-server"
	@exit 1

export-client:
	@echo "$(YELLOW)No export_presets.cfg yet.$(RESET) Add a 'Windows Desktop' client preset in the Godot editor" \
		"(Project > Export...) named 'client', then this target will run:" \
		"godot --headless --export-release client build/client/the-room.exe"
	@exit 1
