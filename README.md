
<p align="center">

<img width="136" height="275" alt="prometheus-icon" src="https://github.com/user-attachments/assets/b8268646-a09d-460b-901e-301d0921f0e0" />
</p>

## What this platform does

Prometheus is a hybrid AI assistant platform composed of an ASP.NET server and a React/Vite client. It combines cloud-hosted LLM connectors, Microsoft Semantic Kernel integration, and local LLM support (LLamaSharp) to provide conversational and prompt-driven AI capabilities specifically for developing Solana on-chain programs & more. The system is ready for local development and production deployment scenarios.

## The platform's purpose

Prometheus is more than "an AI chat server plus a front-end" — its core purpose is to make programmatic, repeatable, and auditable code generation and prompt engineering practical for software teams. Specifically, it is designed to:

- Produce structured, production-intent code artifacts from prompt-driven workflows (for example, full Solana/Anchor program sources wrapped in a stable blueprint format). 
- Orchestrate multiple model runtimes and connectors (cloud LLMs and local LLMs) under a single API and prompt management layer. 
- Provide a reproducible pipeline for generating, validating, and curating code and prompt-output pairs so teams can quickly bootstrap projects or build datasets for model tuning.

This makes Prometheus ideal for use-cases such as developer productivity tooling (scaffolding, example generation), program synthesis for targeted platforms (Solana, Rust + Anchor in the example prompt), and synthetic dataset generation for fine-tuning or instruction-tuning LLMs.

## How generated code helps developers (practical uses)

1) Jump-starting development

- Rapid scaffolding: request a full program or module, receive multiple source files organized in the blueprint format, and drop them into a new project skeleton (Cargo/Anchor files are intentionally omitted by the system prompt so the platform can generate only source files while build metadata is created by existing build tooling).
- Working examples: the generated code is intended to compile (after adding standard Cargo/Anchor manifests and dependencies), which reduces time to first-run and helps developers focus on domain logic instead of boilerplate.
- Incremental development: use the same prompts to request targeted changes (new instructions, additional state types, or optimizations) so the assistant becomes an active development partner.

2) Building rich datasets for LLM training or fine-tuning

- Prompt-response pairs: store the exact system prompt, user prompt, and the full structured output (all files produced). These pairs are high-quality supervised examples for instruction tuning.
- File-level artifacts: each generated source file is a labeled artifact. Collect files, filenames, and contextual metadata (prompt, model parameters, timestamp, generator version) into a JSONL or CSV manifest for later ingestion.
- Variant generation: programmatically vary prompts (different constraints, edge cases, or style guides) to generate diverse examples for robust training sets.

Best practices when using generated code as training data:

- Sanity-check and sanitize: compile generated code, run linters/formatters (rustfmt, clippy), and remove any sensitive or environment-specific values before ingestion.
- Add metadata: include the system prompt text (or a version pointer), the user prompt, model id, and any post-processing steps used to validate or fix the output.
- License and provenance: retain clear provenance and licensing metadata for each example, and confirm you have the right to use generated outputs for training in your jurisdiction.

## Primary components

- Prometheus.Server (ASP.NET)
  - Location: `Prometheus.Server/`
  - Responsibilities: exposes REST API endpoints, integrates AI connectors and model runtimes, loads system and factory prompts, and orchestrates request handling.
  - Notable files: `Program.cs`, `Prometheus.cs`, `Controllers/API_Controller.cs`, `prompts/`.
  - Bundled libraries (examples found in `bin/`): Azure OpenAI, Microsoft Semantic Kernel, LLamaSharp for local LLM use.

- prometheus.client (React + Vite)
  - Location: `prometheus.client/`
  - Responsibilities: single-page frontend for interacting with the AI assistant, file browser UI, prompt form UI, and other frontend components.
  - Notable files: `src/App.tsx`, `src/components/PromptForm.tsx`, `src/services/api.tsx`.

## Key capabilities

- Conversational AI endpoints backed by local model runtimes or cloud LLMs.
- Semantic Kernel integration for chaining prompts and managing context.
- Config-driven prompts: system prompt and a set of factory prompts under `Prometheus.Server/prompts/`.
- Client UI for sending prompts and viewing responses, plus small components like `FileBrowser` and `LoadingSpinner`.
- Cross-platform development with .NET 8/9 server and a TypeScript React client.

## Architecture (high level)

- Client (React/Vite) → Server (ASP.NET controllers) → AI connectors / local model runtime
- Prompts and request types are stored on the server and used to control behavior and system messages.
- Server may use Microsoft Semantic Kernel and provider connectors to route calls to Azure OpenAI or local LLMs (LLamaSharp).

## Prometheus system prompt — what it does

The server ships with a configurable "system prompt" (`Prometheus.Server/prompts/prometheus-system-prompt.txt`) that is used as the assistant's instruction-level policy for generated responses. That file:

- Declares the assistant identity and expertise (for example: an expert in Rust and Solana/Anchor). 
- Imposes strict output rules and formatting (for example: requiring generated Solana program files to be wrapped in a specific <Blueprint> / <Files> tag structure). 
- Enforces production-ready constraints (no placeholders, complete implementations, Anchor idioms) and a deterministic extraction format so downstream tooling can reliably parse and compile generated code.

In short: the system prompt is both the behavioral policy (what the assistant should know and prioritize) and the structural contract (how the assistant must format outputs) used by the platform's code-generation features.

Because the platform treats the system prompt as data, teams can swap, version, and A/B test different prompts to change assistant behavior without changing server code.

## Practical checklist to turn generated outputs into a reusable dataset

1. Store each generation as a single record containing:
  - system_prompt_id (or full text), user_prompt, model_id, timestamp
  - an array of { filename, contents } for each file produced by the blueprint
  - validation flags (compiled: true/false, lint_passed: true/false)
2. Run automated validation: rustfmt + cargo check (or the equivalent for other languages)
3. Normalize: apply formatting, remove environment-specific paths/keys, and canonicalize timestamps
4. Export JSONL lines; each line is one training example for supervised fine-tuning or instruction tuning

## Developer guidance: verify and adopt generated code

- Always run `cargo fmt` and `cargo clippy` on generated Rust code and run `cargo check` before merging generated sources.
- Use the platform's blueprint tags and the server's parsing utilities (if present) to import code in a deterministic way.
- Treat generated code as a first-class artifact: add unit tests or quick integration checks to ensure semantics match expectations.

## Example workflow: generate → validate → integrate

1. From the client or API, send a user prompt and choose the `prometheus-system-prompt.txt` policy.
2. Receive a blueprint-wrapped response containing multiple source files.
3. Parse the blueprint, write the files to a preview project, run `cargo check` and `rustfmt`.
4. If validated, commit to a feature branch and run CI; if not, request a follow-up prompt to fix issues.

## IDE Friendly
- Visual Studio 2022
- VS Code

## How to run locally (Windows PowerShell)

Run the server (from repository root):

```powershell
# Build and run the ASP.NET server
cd .\Prometheus.Server
dotnet build
dotnet run --project .\Prometheus.Server.csproj
```

Run the client (in another shell):

```powershell
cd .\prometheus.client
npm install
npm run dev
```

Notes:
- The server reads configuration from `appsettings.json` and `appsettings.Development.json`.
- If using cloud AI services, ensure relevant API keys and connection strings are set in environment variables or `appsettings`.

## Where prompts and test data live

- System prompt: `Prometheus.Server/prompts/prometheus-system-prompt.txt`.
- Factory prompts: `Prometheus.Server/prompts/factory_prompts/` (numbered text files).

These can be edited to adjust assistant behavior or to add new canned prompts for testing.

## Development notes & tips

- API controller(s) are in `Prometheus.Server/Controllers/` — inspect `API_Controller.cs` to see available endpoints and request/response contracts.
- Client-side services live in `prometheus.client/src/services/` (look at `api.tsx`) and show how the frontend calls the server.
- The project already includes several NuGet/JS dependencies. View package manifests for reproducible builds (`Prometheus.Server.csproj`, `prometheus.client/package.json`).
- For local LLM experiments, review LLamaSharp usage, and be cautious about model files/weights (not included in repo) and memory/permissions.

## Security & configuration

- Do not commit secrets to the repo. Use `appsettings.Development.json` locally or environment variables for API keys.
- Validate CORS and frontend origins when deploying to production.

## Contributing

- Add features or fixes via branches targeted at `main`.
- Run server and client locally, add tests where appropriate, and keep changes small and focused.
