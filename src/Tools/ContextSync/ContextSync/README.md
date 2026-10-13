# ContextSync

Sync source documents to Jira, Confluence Wiki, and generate HTML files — with a Rich CLI powered by Spectre.Console.

## Architecture

Vertical Slice Architecture (VSA) + Command-Handler Pattern:

```
ContextSync/
  Program.cs                    # Spectre.Console.Cli entry point
  appsettings.json              # Configuration (Jira, Confluence, DB, HTML)
  Abstractions/                 # Interfaces (ISourceReader, IJiraService, etc.)
  Models/                       # SourceDocument, SyncResults
  Infrastructure/
    Settings/AppSettings.cs     # Strongly-typed config
    DependencyInjection.cs      # DI container setup
  Services/
    JiraService.cs              # Jira REST API client
    ConfluenceService.cs        # Confluence REST API client
    HtmlGeneratorService.cs     # HTML template rendering (default/minimal/report)
    FileSourceReader.cs         # File reader with YAML-like front matter
    DatabaseSourceReader.cs     # SQL query reader
    SyncOrchestrator.cs         # Full pipeline orchestrator
  Features/
    SyncJira/                   # jira command
    SyncWiki/                   # wiki command
    GenerateHtml/               # html command
    SyncAll/                    # all command
ContextSync.dal/                # Data Access Layer (EF Core, SqlClient)
ContextSync.Tests/              # xUnit tests with Moq
```

## Commands

| Command | Description |
|---------|-------------|
| `ContextSync jira` | Sync source documents to Jira issues |
| `ContextSync wiki` | Sync source documents to Confluence wiki pages |
| `ContextSync html` | Generate HTML files from source documents |
| `ContextSync all` | Full pipeline: Jira + Confluence + HTML |

## Usage Examples

```bash
# Sync files to Jira
ContextSync jira -i docs -p PROJ --type Task

# Sync from database to Jira
ContextSync jira --from-db -s localhost -d Tasks -q "SELECT * FROM Tasks"

# Sync files to Confluence
ContextSync wiki -i docs -s SPACE -p "Parent Page"

# Generate HTML with report template
ContextSync html -i docs -o output -t report

# Full pipeline, skip HTML
ContextSync all -i docs --no-html

# Full pipeline from database
ContextSync all --from-db -q "SELECT * FROM SyncSource"
```

## Source Document Format

Text files with optional YAML-like front matter:

```
---
title: My Task
jira-project: PROJ
jira-issuetype: Task
jira-assignee: user
jira-epic: PROJ-123
jira-priority: High
jira-labels: backend, urgent
wiki-space: DOC
wiki-parent: Parent Page
html-template: report
html-output: output/custom
---

Task content goes here.
This becomes the Jira description and wiki page body.
```

## Configuration

`appsettings.json`:

```json
{
  "Jira": {
    "Url": "https://your-jira.atlassian.net",
    "Username": "email@example.com",
    "ApiToken": "your-token",
    "DefaultProjectKey": "PROJ",
    "DefaultIssueType": "Task",
    "DefaultPriority": "Medium"
  },
  "Confluence": {
    "Url": "https://your-confluence.atlassian.net",
    "Username": "email@example.com",
    "ApiToken": "your-token",
    "DefaultSpaceKey": "DOC"
  },
  "Database": {
    "Server": "localhost",
    "Database": "Tasks",
    "IntegratedSecurity": true,
    "QueryText": "SELECT * FROM SyncSource"
  },
  "Html": {
    "OutputDirectory": "output/html",
    "Template": "default"
  },
  "FileSource": {
    "InputDirectory": "input",
    "FilePattern": "*.txt",
    "Recursive": true
  }
}
```

## HTML Templates

| Template | Description |
|----------|-------------|
| `default` | Full page with metadata panel and styling |
| `minimal` | Bare-bones HTML, no CSS |
| `report` | Styled report with gradient header and meta grid |

## Build

```bash
dotnet build ContextSync.slnx
```
